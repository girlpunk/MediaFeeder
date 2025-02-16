using System.Xml;
using Google.Apis.YouTube.v3;
using JetBrains.Annotations;
using MassTransit;
using MediaFeeder.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MediaFeeder.Providers.Youtube;

[UsedImplicitly]
public class YoutubeActualVideoSynchroniseConsumer(
    ILogger<YoutubeActualVideoSynchroniseConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    YouTubeService youTubeService,
    Utils utils,
    IHttpClientFactory httpClientFactory
) : IConsumer<YoutubeActualVideoSynchroniseContract>
{
    public async Task Consume(ConsumeContext<YoutubeActualVideoSynchroniseContract> context)
    {
        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos
            .Include(static v => v.Subscription)
            .SingleAsync(v => v.Id == context.Message.VideoId, context.CancellationToken);

        logger.LogInformation("Starting synchronize video {}", video.Id);

        if (video.DownloadedPath != null)
        {
            if (!File.Exists(video.DownloadedPath))
            {
                // Video not found, we can safely assume that the video was deleted.

                logger.LogInformation($"File for {video.Id} not found, assuming deleted");
                video.DownloadedPath = null;

                // Mark watched?
                // user = video.subscription.user
                // if user.preferences['mark_deleted_as_watched']:
                //     video.watched = True
            }
            else
            {
                if (video.Watched && (video.Subscription!.AutomaticallyDeleteWatched ?? false))
                {
                    // Video is watched and subscription is set to automatically delete watched videos
                    logger.LogInformation($"Deleting file {video.DownloadedPath} for {video.Id}, as video has been watched");

                    File.Delete(video.DownloadedPath);
                    video.DownloadedPath = null;
                }
            }

            await db.SaveChangesAsync(context.CancellationToken);
        }

        if (video.Duration == 0 || string.IsNullOrWhiteSpace(video.Thumb))
        {
            var videoRequest = youTubeService.Videos.List("id,statistics,contentDetails,snippet");
            videoRequest.Id = video.VideoId;
            var videoResponse = await videoRequest.ExecuteAsync(context.CancellationToken);
            var videoStats = videoResponse.Items.SingleOrDefault();

            if (videoStats != null)
            {
                if (videoStats.Statistics.LikeCount + videoStats.Statistics.DislikeCount > 0)
                    video.Rating = (videoStats.Statistics.LikeCount / (videoStats.Statistics.LikeCount + videoStats.Statistics.DislikeCount));

                video.Views = (int?)videoStats.Statistics.ViewCount;

                // This can be null if the video "Premieres" in the future
                if (!string.IsNullOrWhiteSpace(videoStats.ContentDetails.Duration))
                    video.DurationSpan = XmlConvert.ToTimeSpan(videoStats.ContentDetails.Duration);
            }

            if (videoStats?.Snippet != null)
            {
                video.Description = videoStats.Snippet.Description;
                video.Name = videoStats.Snippet.Title;
            }

            if (videoStats?.Snippet?.Thumbnails != null)
            {
                var newThumb = await utils.LoadResourceThumbnail(video.VideoId, "video", videoStats.Snippet.Thumbnails, logger, context.CancellationToken);
                if (!string.IsNullOrWhiteSpace(newThumb))
                    video.Thumb = newThumb;
            }
            else
            {
                logger.LogWarning("Thumbnails are null while syncing {}", video.Id);
            }

            await db.SaveChangesAsync(context.CancellationToken);
        }

        if (video.Duration == 0 || string.IsNullOrWhiteSpace(video.Thumb))
        {
            // Still no duration or thumbnail, see if dearrow has any metadata
            var httpClient = httpClientFactory.CreateClient("retry");

            var dearrowInfo = await httpClient.GetFromJsonAsync<DearrowBrandingResponse>($"https://sponsor.ajay.app/api/branding?videoID={video.VideoId}");

            if (dearrowInfo == null)
                return;

            if (video.Duration == 0 && dearrowInfo.VideoDuration != null)
                video.Duration = Convert.ToInt32(dearrowInfo.VideoDuration);

            if (string.IsNullOrWhiteSpace(video.Thumb) &&
                dearrowInfo.Thumbnails.Any(static t => t.Locked || t.Votes > 0))
                video.Thumb = $"https://dearrow-thumb.ajay.app/api/v1/getThumbnail?videoID={video.VideoId}";

            await db.SaveChangesAsync(context.CancellationToken);
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }

    private sealed record DearrowBrandingResponse
    {
        [JsonProperty("titles")] public List<DearrowBrandingTitle> Titles { get; set; } = [];

        [JsonProperty("titles")] public List<DearrowBrandingThumbnail> Thumbnails { get; set; } = [];

        [JsonProperty("randomTime")]
        public decimal RandomTime { get; set; }

        [JsonProperty("videoDuration")]
        public decimal? VideoDuration { get; set; }
    }

    private sealed record DearrowBrandingTitle
    {
        // Note: Titles will sometimes contain > before a word. This tells the auto-formatter to not format a word. If you have no auto-formatter, you can ignore this and replace it with an empty string
        [JsonProperty("title")]
        public required string Title { get; set; }

        [JsonProperty("original")]
        public bool Original { get; set; }

        [JsonProperty("votes")]
        public int Votes { get; set; }

        [JsonProperty("locked")]
        public bool Locked { get; set; }

        [JsonProperty("UUID")]
        public required Guid Uuid { get; set; }

        [JsonProperty("userID")]
        public string? UserId { get; set; }
    }

    private sealed record DearrowBrandingThumbnail
    {
        [JsonProperty("timestamp")]
        public decimal? Timestamp { get; set; }

        [JsonProperty("original")] public bool Original { get; set; }

        [JsonProperty("votes")]
        public int Votes { get; set; }

        [JsonProperty("locked")] public bool Locked { get; set; }

        [JsonProperty("UUID")]
        public Guid Uuid { get; set; }

        [JsonProperty("userID")]
        public string? UserId { get; set; }
    }
}

using System.Net;
using System.Xml;
using Google.Apis.YouTube.v3;
using MassTransit;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MediaFeeder.Providers.Youtube;

public sealed class YoutubeVideoSynchroniseConsumer(
    ILogger<YoutubeVideoSynchroniseConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    YouTubeService youTubeService,
    Utils utils,
    IHttpClientFactory httpClientFactory
) : IConsumer<YoutubeVideoSynchroniseContract>
{
    public async Task Consume(ConsumeContext<YoutubeVideoSynchroniseContract> context)
    {
        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var video = await db.Videos
            .Include(static v => v.Subscription)
            .SingleAsync(v => v.Id == context.Message.VideoId, context.CancellationToken);

        if (string.IsNullOrWhiteSpace(video.DownloadedPath) && video.Duration is not (0 or null) &&
            !string.IsNullOrWhiteSpace(video.Thumb))
        {
            logger.LogInformation("Skipping synchronize video {}", video.Id);
            return;
        }

        logger.LogInformation("Starting synchronize video {}", video.Id);

        if (video.DownloadedPath != null)
            await SynchroniseDownloaded(video, db, context.CancellationToken);

        if (video.Duration is 0 or null || string.IsNullOrWhiteSpace(video.Thumb))
            await GetDetailsFromYouTube(video, db, context.CancellationToken);

        if (video.Duration is 0 or null || string.IsNullOrWhiteSpace(video.Thumb))
            await GetDetailsFromDeArrow(video, db, context.CancellationToken);

        await db.SaveChangesAsync(context.CancellationToken);
    }

    private async Task GetDetailsFromDeArrow(Video video, MediaFeederDataContext db,
        CancellationToken cancellationToken)
    {
        // Still no duration or thumbnail, see if dearrow has any metadata
        using var httpClient = httpClientFactory.CreateClient("retry");

        try
        {
            var dearrowInfo =
                await httpClient.GetFromJsonAsync<DearrowBrandingResponse>(
                    $"https://sponsor.ajay.app/api/branding?videoID={video.VideoId}", cancellationToken);

            if (dearrowInfo == null) return;

            if (video.Duration is 0 or null && dearrowInfo.VideoDuration != null)
                video.Duration = Convert.ToInt32(dearrowInfo.VideoDuration);

            if (string.IsNullOrWhiteSpace(video.Thumb) &&
                dearrowInfo.Thumbnails.Any(static t => t.Locked || t.Votes > 0))
                video.Thumb = $"https://dearrow-thumb.ajay.app/api/v1/getThumbnail?videoID={video.VideoId}";

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (HttpRequestException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
                logger.LogError(e, "Error while loading fallback metadata from dearrow");
            else
                throw;
        }
    }

    private async Task GetDetailsFromYouTube(Video video, MediaFeederDataContext db, CancellationToken cancellationToken)
    {
        var videoRequest = youTubeService.Videos.List("id,statistics,contentDetails,snippet");
        videoRequest.Id = video.VideoId;
        var videoResponse = await videoRequest.ExecuteAsync(cancellationToken);
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
            var newThumb = await utils.LoadResourceThumbnail(video.VideoId, "video", videoStats.Snippet.Thumbnails, logger, cancellationToken);
            if (!string.IsNullOrWhiteSpace(newThumb))
                video.Thumb = newThumb;
        }
        else
        {
            logger.LogWarning("Thumbnails are null while syncing {}", video.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SynchroniseDownloaded(Video video, MediaFeederDataContext db, CancellationToken cancellationToken)
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
            if (video.Watched && (video.Subscription!.AutomaticallyDeleteWatched))
            {
                // Video is watched and subscription is set to automatically delete watched videos
                logger.LogInformation("Deleting file {} for {}, as video has been watched", video.DownloadedPath, video.Id);

                File.Delete(video.DownloadedPath);
                video.DownloadedPath = null;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
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

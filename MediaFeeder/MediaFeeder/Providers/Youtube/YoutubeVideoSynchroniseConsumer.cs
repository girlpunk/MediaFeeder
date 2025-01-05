using Google.Apis.YouTube.v3;
using MassTransit;
using MediaFeeder.Data;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Providers.Youtube;

public class YoutubeActualVideoSynchroniseConsumer(
    ILogger<YoutubeActualVideoSynchroniseConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    YouTubeService youTubeService
) : IConsumer<YoutubeActualVideoSynchroniseContract>
{
    public async Task Consume(ConsumeContext<YoutubeActualVideoSynchroniseContract> context)
    {
        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos.SingleAsync(v => v.Id == context.Message.VideoId, context.CancellationToken);

        logger.LogInformation("Starting synchronize video {}", video.Id);

        if (video.DownloadedPath != null)
        {
            var files = new List<string>();
            try
            {
                var directory = Path.GetDirectoryName(video.DownloadedPath);
                var filePattern = Path.GetFileNameWithoutExtension(video.DownloadedPath);

                if (directory != null)
                    files.AddRange(Directory.EnumerateFiles(directory)
                        .Where(file => file.StartsWith(filePattern, StringComparison.Ordinal)));
            }
            catch (FileNotFoundException)
            {
            }

            // Try to find a valid video file
            var foundVideo = false;
            foreach (var file in files)
            {
                new FileExtensionContentTypeProvider().TryGetContentType(file, out var mime);
                if (mime != null && mime.StartsWith("video", StringComparison.Ordinal))
                    foundVideo = true;
            }

            // Video not found, we can safely assume that the video was deleted.
            if (!foundVideo)
            {
                // Clean up
                foreach (var file in files)
                    File.Delete(file);

                video.DownloadedPath = null;

                // Mark watched?
                // user = video.subscription.user
                // if user.preferences['mark_deleted_as_watched']:
                //     video.watched = True
            }
        }

        if (video.Duration == 0)
        {
            var videoRequest = youTubeService.Videos.List("id,statistics,contentDetails");
            videoRequest.Id = video.VideoId;
            var videoResponse = await videoRequest.ExecuteAsync(context.CancellationToken);
            var videoStats = videoResponse.Items.SingleOrDefault();

            if (videoStats != null)
            {
                if (videoStats.Statistics.LikeCount + videoStats.Statistics.DislikeCount > 0)
                    video.Rating = (double?)(videoStats.Statistics.LikeCount / (videoStats.Statistics.LikeCount + videoStats.Statistics.DislikeCount)) ?? 0;

                video.Views = (int?)videoStats.Statistics.ViewCount ?? 0;
                video.DurationSpan = TimeSpan.Parse(videoStats.ContentDetails.Duration);
                video.Description = videoStats.Snippet.Description;
            }
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }
}

public class YoutubeVideoSynchroniseConsumer(
    ILogger<YoutubeVideoSynchroniseConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    IBus bus,
    Utils utils,
    YouTubeService youTubeService
) : IConsumer<YoutubeVideoSynchroniseContract>
{
    public async Task Consume(ConsumeContext<YoutubeVideoSynchroniseContract> context)
    {
        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var video = await db.Videos.SingleAsync(v => v.Id == context.Message.VideoId, context.CancellationToken);

        if (video.DownloadedPath != null || video.Duration == 0)
            await bus.Publish(new YoutubeActualVideoSynchroniseContract(video.Id), context.CancellationToken);

        if (video.Thumb == null)
        {
            var videoRequest = youTubeService.Videos.List("snippet");
            videoRequest.Id = video.VideoId;
            var videoResponse = await videoRequest.ExecuteAsync(context.CancellationToken);
            var videoStats = videoResponse.Items.SingleOrDefault();

            if (videoStats != null)
                video.Thumb = await utils.LoadResourceThumbnail(video.VideoId, "video", videoStats.Snippet.Thumbnails, logger, context.CancellationToken);
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }
}

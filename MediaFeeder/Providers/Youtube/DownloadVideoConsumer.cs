using Mediafeeder;
using MediaFeeder.Data;
using MediaFeeder.Tasks;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Base;

namespace MediaFeeder.Providers.Youtube;

public sealed class YouTubeDownloadVideoConsumer(
    ILogger<YouTubeDownloadVideoConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    IConfiguration configuration,
    YTDownloader.YTDownloaderClient downloaderClient
) : IDownloadVideo<YoutubeProvider>
{
    public async Task ExecuteAsync(
        TickerFunctionContext<DownloadVideoContract<YoutubeProvider>> context,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Starting video download for {}", context.Request.VideoId);
        await using var dbContext = await contextFactory.CreateDbContextAsync();
        var video = await dbContext
            .Videos.Include(static v => v.Subscription)
            .SingleAsync(v => v.Id == context.Request.VideoId);

        if (video.IsDownloaded)
        {
            logger.LogInformation(
                "Cancelling download of {Video}: Video is already downloaded ({Path})",
                video.Id,
                video.DownloadedPath
            );
            return;
        }

        var root =
            configuration.GetValue<string>("MediaRoot") ?? throw new InvalidOperationException();
        var path = Path.Join(
            root,
            "downloads",
            string.Join("", (video.Subscription?.Name ?? "").Split(Path.GetInvalidFileNameChars()))
        );
        Directory.CreateDirectory(path);
        path = Path.Join(
            path,
            $"{string.Join("", video.Name.Split(Path.GetInvalidFileNameChars()))} [{video.Id}]"
        );
        logger.LogInformation("Will be saved to {}", path);

        var downloadResponse = await downloaderClient.DownloadAsync(
            new Mediafeeder.DownloadRequest
            {
                VideoUrl = $"https://www.youtube.com/watch?v={video.VideoId}",
                OutputPath = path,
            },
            cancellationToken: cancellationToken
        );

        if (downloadResponse.Status == Status.Done)
        {
            logger.LogInformation(
                "Successfully downloaded {Video} to {Path}",
                context.Request.VideoId,
                downloadResponse.Filename
            );
            video.DownloadedPath = downloadResponse.Filename;
            await dbContext.SaveChangesAsync();
        }
        else
        {
            logger.LogError(
                "Problem while downloading {Video}:  {Error}",
                context.Request.VideoId,
                downloadResponse.ExitCode
            );
        }

        // New task library can't do this, and I don't think anything uses it anyway
        //await context.RespondAsync(downloadResponse);
    }
}

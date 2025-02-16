using MassTransit;
using Mediafeeder;
using MediaFeeder.Data;
using MediaFeeder.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Providers.Youtube;

public sealed class YouTubeDownloadVideoConsumer(
    ILogger<YouTubeDownloadVideoConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    IConfiguration configuration,
    YTDownloader.YTDownloaderClient downloaderClient
) : IConsumer<DownloadVideoContract<YoutubeProvider>>
{
    public async Task Consume(ConsumeContext<DownloadVideoContract<YoutubeProvider>> context)
    {
        logger.LogInformation("Starting video download for {}", context.Message.VideoId);
        await using var dbContext = await contextFactory.CreateDbContextAsync();
        var video = await dbContext.Videos
            .Include(static v => v.Subscription)
            .SingleAsync(v => v.Id == context.Message.VideoId);

        var root = configuration.GetValue<string>("MediaRoot") ?? throw new InvalidOperationException();
        var path = Path.Join(root, "downloads", string.Join("", (video.Subscription?.Name ?? "").Split(Path.GetInvalidFileNameChars())));
        Directory.CreateDirectory(path);
        path = Path.Join(path, $"{string.Join("", video.Name.Split(Path.GetInvalidFileNameChars()))} [{video.Id}]");
        logger.LogInformation("Will be saved to {}", path);

        var downloadResponse = await downloaderClient.DownloadAsync(new Mediafeeder.DownloadRequest
        {
            VideoUrl = $"https://www.youtube.com/watch?v={video.VideoId}",
            OutputPath = path
        }, cancellationToken: context.CancellationToken);

        if (downloadResponse.Status == Status.Done)
        {
            logger.LogInformation("Successfully downloaded {} to {}", context.Message.VideoId, downloadResponse.Filename);
            video.DownloadedPath = downloadResponse.Filename;
            await dbContext.SaveChangesAsync();
        }
        else
        {
            logger.LogError("Problem while downloading {}:  {}", context.Message.VideoId, downloadResponse.ExitCode);
        }

        await context.RespondAsync(downloadResponse);
    }
}

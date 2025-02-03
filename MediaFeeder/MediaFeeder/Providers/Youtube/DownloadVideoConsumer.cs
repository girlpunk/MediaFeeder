using MassTransit;
using MediaFeeder.Data;
using MediaFeeder.Tasks;
using Microsoft.EntityFrameworkCore;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace MediaFeeder.Providers.Youtube;

public sealed class YouTubeDownloadVideoConsumer(
    ILogger<YouTubeDownloadVideoConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    IConfiguration configuration
) : IConsumer<DownloadVideoContract<YoutubeProvider>>
{
    public async Task Consume(ConsumeContext<DownloadVideoContract<YoutubeProvider>> context)
    {
        logger.LogInformation("Starting video download for {}", context.Message.VideoId);
        await using var dbContext = await contextFactory.CreateDbContextAsync();
        var video = await dbContext.Videos.SingleAsync(v => v.Id == context.Message.VideoId);

        await YoutubeDLSharp.Utils.DownloadYtDlp("/tmp");
        await YoutubeDLSharp.Utils.DownloadFFmpeg("/tmp");

        var root = configuration.GetValue<string>("MediaRoot") ?? throw new InvalidOperationException();
        var path = Path.Join(root, "downloads", video.UploaderName);
        Directory.CreateDirectory(path);

        logger.LogInformation("Will be saved to {}", path);
        var ytdl = new YoutubeDL
        {
            OutputFolder = path,
            YoutubeDLPath = "/tmp/yt-dlp",
            FFmpegPath = "/tmp/ffmpeg",
            RestrictFilenames = true
        };

        var res = await ytdl.RunVideoDownload(
            $"https://www.youtube.com/watch?v={video.VideoId}",
            mergeFormat: DownloadMergeFormat.Mp4,
            recodeFormat: VideoRecodeFormat.Mp4,
            ct: context.CancellationToken,
            progress: new ProgressReporter(logger),
            overrideOptions: new OptionSet()
            {
                FfmpegLocation = "/tmp",

                SponsorblockMark = "chapter,filler,intro,music_offtopic,outro,poi_highlight,preview",
                SponsorblockRemove = "interaction,selfpromo,sponsor",
                EmbedSubs = true,
                EmbedChapters = true,
                EmbedThumbnail = true,
                EmbedMetadata = true,
                RestrictFilenames = true,
                SubLangs = "en.*",
                WriteAutoSubs = true
            });

        if (res.Success)
        {
            logger.LogInformation("Successfully downloaded {} to {}", context.Message.VideoId, res.Data);
            video.DownloadedPath = res.Data;
            await dbContext.SaveChangesAsync();
        }
        else
        {
            logger.LogError("Problem while downloading {}:  {}", context.Message.VideoId, res.Data);
        }
    }

    private sealed class ProgressReporter(ILogger logger) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value)
        {
            logger.LogInformation("{}: {}% {}", value.State, value.Progress, value.Data);
        }
    }
}
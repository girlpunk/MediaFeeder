using Mediafeeder;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using TickerQ.Utilities.Base;

namespace MediaFeeder.Providers.Youtube;

// Verifies bug low-07 (bugs/low/low-07-download-consumer-robustness.md):
// a failed download is only logged and Video.DownloadError is never written; a name that
// sanitises to empty collapses the output file name to " [id]"; and the DbContext is created
// without the job's CancellationToken.
public sealed class DownloadVideoConsumerTests : IDisposable
{
    private readonly DirectoryInfo _mediaRoot = Directory.CreateTempSubdirectory();

    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions =
        new DbContextOptionsBuilder<MediaFeederDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    public void Dispose() => _mediaRoot.Delete(true);

    [Fact]
    public async Task ExecuteAsync_FailedDownload_RecordsDownloadErrorOnVideo()
    {
        var videoId = await SeedVideoAsync("vid1", "Test Video");
        var downloader = new CannedYTDownloaderClient(new Mediafeeder.DownloadReply
        {
            Status = Mediafeeder.Status.PermanentError,
            ExitCode = -1,
        });
        var (consumer, _) = CreateConsumer(downloader);

        var syncContext = new TickerFunctionContext<DownloadVideoContract>(
            new TickerFunctionContext(),
            new DownloadVideoContract(videoId));

        await consumer.ExecuteAsync(syncContext, CancellationToken.None);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var video = await db.Videos.SingleAsync(v => v.Id == videoId);

        video.DownloadError.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_NameEmptyAfterSanitisation_StillYieldsMeaningfulFileName()
    {
        var videoId = await SeedVideoAsync("vid2", "///");
        var downloader = new CannedYTDownloaderClient(new Mediafeeder.DownloadReply { Status = Mediafeeder.Status.Done });
        var (consumer, _) = CreateConsumer(downloader);

        var syncContext = new TickerFunctionContext<DownloadVideoContract>(
            new TickerFunctionContext(),
            new DownloadVideoContract(videoId));

        await consumer.ExecuteAsync(syncContext, CancellationToken.None);

        var request = downloader.Requests.Single();
        var fileName = Path.GetFileName(request.OutputPath);

        fileName.Replace($"[{videoId}]", "", StringComparison.Ordinal).Trim().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ThreadsCancellationTokenIntoDbContextCreation()
    {
        var videoId = await SeedVideoAsync("vid3", "Test Video");
        var downloader = new CannedYTDownloaderClient(new Mediafeeder.DownloadReply { Status = Mediafeeder.Status.Done });
        var (consumer, contextFactory) = CreateConsumer(downloader);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var syncContext = new TickerFunctionContext<DownloadVideoContract>(
            new TickerFunctionContext(),
            new DownloadVideoContract(videoId));

        await consumer.ExecuteAsync(syncContext, cancellationTokenSource.Token);

        contextFactory.CreateDbContextAsyncCalls.Count.ShouldBe(1);
        contextFactory.CreateDbContextAsyncCalls.Single().ShouldBe(cancellationTokenSource.Token);
    }

    private async Task<int> SeedVideoAsync(string videoId, string name)
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = new Subscription
        {
            Name = "MySub",
            ChannelName = "MySub Channel",
            PlaylistId = "PL1",
            ChannelId = "UC1",
            Description = "A test sub",
            Provider = "YouTube",
            UserId = 1,
            ParentFolderId = 1,
        };
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();

        var video = new Video
        {
            VideoId = videoId,
            Name = name,
            Description = "A video",
            UploaderName = "Uploader",
            SubscriptionId = subscription.Id,
            PublishDate = DateTimeOffset.UtcNow.AddDays(-10),
            New = false,
        };
        db.Videos.Add(video);
        await db.SaveChangesAsync();
        return video.Id;
    }

    private (YouTubeDownloadVideoConsumer Consumer, TestDbContextFactory ContextFactory) CreateConsumer(CannedYTDownloaderClient downloader)
    {
        var contextFactory = new TestDbContextFactory(_contextOptions);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["MediaRoot"] = _mediaRoot.FullName,
            })
            .Build();

        var consumer = new YouTubeDownloadVideoConsumer(
            Substitute.For<ILogger<YouTubeDownloadVideoConsumer>>(),
            contextFactory,
            configuration,
            downloader);

        return (consumer, contextFactory);
    }

    private sealed class CannedYTDownloaderClient(Mediafeeder.DownloadReply reply) : YTDownloader.YTDownloaderClient
    {
        public List<Mediafeeder.DownloadRequest> Requests { get; } = [];

        public override Grpc.Core.AsyncUnaryCall<Mediafeeder.DownloadReply> DownloadAsync(
            Mediafeeder.DownloadRequest request,
            Grpc.Core.Metadata headers = null,
            System.DateTime? deadline = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            return new Grpc.Core.AsyncUnaryCall<Mediafeeder.DownloadReply>(
                Task.FromResult(reply),
                Task.FromResult(new Grpc.Core.Metadata()),
                () => new Grpc.Core.Status(Grpc.Core.StatusCode.OK, string.Empty),
                () => new Grpc.Core.Metadata(),
                () => { });
        }
    }
}

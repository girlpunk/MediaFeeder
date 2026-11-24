using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Text;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace MediaFeeder.Providers.Youtube;

// Verifies bug med-07 (bugs/medium/med-07-youtube-subsync-overflow-timezone-requeue.md):
// 7a - a feed entry with more than int.MaxValue views makes int.Parse throw, which aborts
//      the RSS import and falls back to the full (expensive) playlist sync;
// 7d - every fully downloaded video is requeued for a per-video sync job on each subscription sync.
// 7b/7c (local Now vs UtcNow, DayOfYear refresh) are timezone-dependent and cannot be pinned
// in a unit test.
public sealed class YoutubeSubscriptionSynchroniseConsumerTests : IDisposable
{
    private readonly DirectoryInfo _mediaRoot = Directory.CreateTempSubdirectory();

    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions =
        new DbContextOptionsBuilder<MediaFeederDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    public void Dispose() => _mediaRoot.Delete(true);

    [Fact]
    public async Task ExecuteAsync_RssFeedWithOverflowingViews_ImportsVideoWithoutFullPlaylistSync()
    {
        var subscriptionId = await SeedSubscriptionAsync(DateTimeOffset.UtcNow.AddHours(-25));
        await SeedVideoAsync(subscriptionId, "vidA", "Existing Video", null, 60, "thumbnail");

        var googleHandler = new CannedGoogleHandler();
        var consumer = CreateConsumer(
            googleHandler,
            new FixedBodyHandler(RssFeedWithOverflowingViews, "application/atom+xml"));

        var syncContext = new TickerFunctionContext<SynchroniseSubscriptionContract>(
            new TickerFunctionContext(),
            new SynchroniseSubscriptionContract(subscriptionId));

        await consumer.ExecuteAsync(syncContext, CancellationToken.None);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var importedViaRss = await db.Videos.AnyAsync(static v => v.VideoId == "vidB");

        importedViaRss.ShouldBeTrue();
        googleHandler.PlaylistItemsCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_FullyDownloadedVideo_IsNotRequeuedForVideoSync()
    {
        var subscriptionId = await SeedSubscriptionAsync(DateTimeOffset.UtcNow.AddHours(-25));
        var downloadedVideoId = await SeedVideoAsync(subscriptionId, "vidA", "Done Video", "/data/a.mp4", 60, "thumbnail");
        var incompleteVideoId = await SeedVideoAsync(subscriptionId, "vidB", "Incomplete Video", null, null, null);

        var googleHandler = new CannedGoogleHandler();
        var timeTicker = Substitute.For<ITimeTickerManager<TimeTickerEntity>>();
        var consumer = CreateConsumer(
            googleHandler,
            new FixedBodyHandler(RssFeedWithExistingVideoOnly, "application/atom+xml"),
            timeTicker);

        var syncContext = new TickerFunctionContext<SynchroniseSubscriptionContract>(
            new TickerFunctionContext(),
            new SynchroniseSubscriptionContract(subscriptionId));

        await consumer.ExecuteAsync(syncContext, CancellationToken.None);

        timeTicker.DidNotReceive().AddAsync<YoutubeVideoSynchroniseConsumer, YoutubeVideoSynchroniseContract>(
            Arg.Any<DateTime>(),
            Arg.Is<YoutubeVideoSynchroniseContract>(c => c.VideoId == downloadedVideoId),
            Arg.Any<CancellationToken>());

        var incompleteRequeues = timeTicker
            .ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "AddAsync"
                && c.GetArguments()[1] is YoutubeVideoSynchroniseContract requeue
                && requeue.VideoId == incompleteVideoId)
            .ToList();
        incompleteRequeues.Any().ShouldBeTrue();
    }

    private async Task<int> SeedSubscriptionAsync(DateTimeOffset lastSynchronised)
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = new Subscription
        {
            Name = "Test Channel",
            ChannelName = "Original Channel Name",
            PlaylistId = "PL123",
            ChannelId = "UC123",
            Description = "A test channel",
            Provider = "YouTube",
            UserId = 1,
            ParentFolderId = 1,
            LastSynchronised = lastSynchronised,
        };
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription.Id;
    }

    private async Task<int> SeedVideoAsync(
        int subscriptionId,
        string videoId,
        string name,
        string? downloadedPath,
        int? duration,
        string? thumb)
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var video = new Video
        {
            VideoId = videoId,
            Name = name,
            Description = "A video",
            UploaderName = "Uploader",
            SubscriptionId = subscriptionId,
            DownloadedPath = downloadedPath,
            Duration = duration,
            Thumb = thumb,
            Thumbnail = thumb,
            PublishDate = DateTimeOffset.UtcNow.AddDays(-10),
            New = false,
        };
        db.Videos.Add(video);
        await db.SaveChangesAsync();
        return video.Id;
    }

    private YoutubeSubscriptionSynchroniseConsumer CreateConsumer(
        CannedGoogleHandler googleHandler,
        HttpMessageHandler rssHandler,
        ITimeTickerManager<TimeTickerEntity>? timeTicker = null)
    {
        var contextFactory = new TestDbContextFactory(_contextOptions);

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("retry").Returns(new HttpClient(rssHandler));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["MediaRoot"] = _mediaRoot.FullName,
            })
            .Build();
        var utils = new Utils(configuration, httpClientFactory);

        var youTubeService = new YouTubeService(new BaseClientService.Initializer
        {
            ApplicationName = "MediaFeeder-tests",
            ApiKey = "test-key",
            HttpClientFactory = new CannedGoogleHttpClientFactory(googleHandler),
        });

        var meterFactory = Substitute.For<System.Diagnostics.Metrics.IMeterFactory>();
        meterFactory.Create(Arg.Any<System.Diagnostics.Metrics.MeterOptions>()).Returns(new System.Diagnostics.Metrics.Meter("MediaFeeder-tests"));
        var metrics = new Metrics(meterFactory, contextFactory);

        return new YoutubeSubscriptionSynchroniseConsumer(
            Substitute.For<ILogger<YoutubeSubscriptionSynchroniseConsumer>>(),
            contextFactory,
            httpClientFactory,
            timeTicker ?? Substitute.For<ITimeTickerManager<TimeTickerEntity>>(),
            utils,
            youTubeService,
            metrics);
    }

    private const string RssFeedWithOverflowingViews = """
        <feed xmlns="http://www.w3.org/2005/Atom"
              xmlns:yt="http://www.youtube.com/xml/schemas/2015"
              xmlns:media="http://search.yahoo.com/mrss/">
          <title>Test Channel</title>
          <entry>
            <yt:videoId>vidA</yt:videoId>
            <title>Existing Video</title>
            <published>2026-01-01T00:00:00+00:00</published>
            <media:group/>
            <author><name>Test Channel</name></author>
          </entry>
          <entry>
            <yt:videoId>vidB</yt:videoId>
            <title>Viral Video</title>
            <published>2026-01-02T00:00:00+00:00</published>
            <media:group>
              <media:community>
                <media:statistics views="3000000000"/>
              </media:community>
            </media:group>
            <author><name>Test Channel</name></author>
          </entry>
        </feed>
        """;

    private const string RssFeedWithExistingVideoOnly = """
        <feed xmlns="http://www.w3.org/2005/Atom"
              xmlns:yt="http://www.youtube.com/xml/schemas/2015"
              xmlns:media="http://search.yahoo.com/mrss/">
          <title>Test Channel</title>
          <entry>
            <yt:videoId>vidA</yt:videoId>
            <title>Existing Video</title>
            <published>2026-01-01T00:00:00+00:00</published>
            <media:group/>
            <author><name>Test Channel</name></author>
          </entry>
        </feed>
        """;

    private sealed class CannedGoogleHandler : HttpMessageHandler
    {
        public int PlaylistItemsCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;

            if (url.Contains("playlistItems", StringComparison.OrdinalIgnoreCase))
            {
                PlaylistItemsCalls++;
                return Task.FromResult(Respond("""{"kind":"youtube#playlistItemListResponse","items":[]}"""));
            }

            if (url.Contains("channels", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Respond("""{"kind":"youtube#channelListResponse","items":[]}"""));

            return Task.FromResult(Respond("""{"kind":"youtube#videoListResponse","items":[]}"""));
        }

        private static HttpResponseMessage Respond(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class CannedGoogleHttpClientFactory(HttpMessageHandler handler) : Google.Apis.Http.HttpClientFactory
    {
        protected override HttpMessageHandler CreateHandler(Google.Apis.Http.CreateHttpClientArgs args) => handler;
    }

    private sealed class FixedBodyHandler(string body, string mediaType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        });
    }
}

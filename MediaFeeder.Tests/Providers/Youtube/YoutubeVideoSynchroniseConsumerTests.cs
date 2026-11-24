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

namespace MediaFeeder.Providers.Youtube;

// Verifies bug med-14 (bugs/medium/med-14-youtube-video-sync-stats.md):
// 14a - a video without a statistics block NREs the whole sync task;
// 14b - integer long division makes Rating always 0 or 1 instead of a 0..1 ratio;
// 14c - casting a >int.MaxValue ViewCount to (int?) wraps into a negative view count.
// 14d (unguarded File.Delete) is a file-lock/permissions race and 14e is latent dead code
// (the DeArrow call site is commented out) - neither is unit-testable here.
public sealed class YoutubeVideoSynchroniseConsumerTests : IDisposable
{
    private readonly DirectoryInfo _mediaRoot = Directory.CreateTempSubdirectory();

    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions =
        new DbContextOptionsBuilder<MediaFeederDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    public void Dispose() => _mediaRoot.Delete(true);

    [Fact]
    public async Task ExecuteAsync_VideoWithoutStatistics_SyncsBasicDetailsWithoutThrowing()
    {
        var subscriptionId = await SeedSubscriptionAsync();
        var videoId = await SeedVideoAsync(subscriptionId);
        var consumer = CreateConsumer(VideoWithoutStatisticsJson);

        var syncContext = new TickerFunctionContext<YoutubeVideoSynchroniseContract>(
            new TickerFunctionContext(),
            new YoutubeVideoSynchroniseContract(videoId));

        await Should.NotThrowAsync(() => consumer.ExecuteAsync(syncContext, CancellationToken.None));

        await using var db = new MediaFeederDataContext(_contextOptions);
        var video = await db.Videos.SingleAsync(v => v.Id == videoId);

        video.Name.ShouldBe("Synced Title");
    }

    [Fact]
    public async Task ExecuteAsync_MixedLikeCounts_CalculatesProportionalRating()
    {
        var subscriptionId = await SeedSubscriptionAsync();
        var videoId = await SeedVideoAsync(subscriptionId);
        var consumer = CreateConsumer(VideoWithMixedLikeCountsJson);

        var syncContext = new TickerFunctionContext<YoutubeVideoSynchroniseContract>(
            new TickerFunctionContext(),
            new YoutubeVideoSynchroniseContract(videoId));

        await consumer.ExecuteAsync(syncContext, CancellationToken.None);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var video = await db.Videos.SingleAsync(v => v.Id == videoId);

        video.Rating.ShouldBe(0.8);
    }

    [Fact]
    public async Task ExecuteAsync_OverflowingViewCount_StoresNonNegativeViewCount()
    {
        var subscriptionId = await SeedSubscriptionAsync();
        var videoId = await SeedVideoAsync(subscriptionId);
        var consumer = CreateConsumer(VideoWithOverflowingViewCountJson);

        var syncContext = new TickerFunctionContext<YoutubeVideoSynchroniseContract>(
            new TickerFunctionContext(),
            new YoutubeVideoSynchroniseContract(videoId));

        await consumer.ExecuteAsync(syncContext, CancellationToken.None);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var video = await db.Videos.SingleAsync(v => v.Id == videoId);

        video.Views!.Value.ShouldBeGreaterThanOrEqualTo(0);
    }

    private async Task<int> SeedSubscriptionAsync()
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
            LastSynchronised = DateTimeOffset.UtcNow.AddHours(-25),
        };
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription.Id;
    }

    private async Task<int> SeedVideoAsync(int subscriptionId)
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var video = new Video
        {
            VideoId = "vid1",
            Name = "Old Name",
            Description = "old description",
            UploaderName = "Uploader",
            SubscriptionId = subscriptionId,
            PublishDate = DateTimeOffset.UtcNow.AddDays(-10),
            New = false,
        };
        db.Videos.Add(video);
        await db.SaveChangesAsync();
        return video.Id;
    }

    private YoutubeVideoSynchroniseConsumer CreateConsumer(string cannedVideoJson)
    {
        var contextFactory = new TestDbContextFactory(_contextOptions);

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("retry").Returns(new HttpClient(new NotFoundHandler()));

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
            HttpClientFactory = new CannedGoogleHttpClientFactory(new FixedBodyHandler(cannedVideoJson)),
        });

        return new YoutubeVideoSynchroniseConsumer(
            Substitute.For<ILogger<YoutubeVideoSynchroniseConsumer>>(),
            contextFactory,
            youTubeService,
            utils,
            httpClientFactory);
    }

    private const string VideoWithoutStatisticsJson =
        """{"kind":"youtube#videoListResponse","items":[{"id":"vid1","snippet":{"title":"Synced Title","description":"Synced description"},"contentDetails":{"duration":"PT4M"}}]}""";

    private const string VideoWithMixedLikeCountsJson =
        """{"kind":"youtube#videoListResponse","items":[{"id":"vid1","snippet":{"title":"Synced Title","description":"Synced description"},"contentDetails":{"duration":"PT4M"},"statistics":{"viewCount":"1000","likeCount":"800","dislikeCount":"200"}}]}""";

    private const string VideoWithOverflowingViewCountJson =
        """{"kind":"youtube#videoListResponse","items":[{"id":"vid1","snippet":{"title":"Synced Title","description":"Synced description"},"contentDetails":{"duration":"PT4M"},"statistics":{"viewCount":"3000000000","likeCount":"0","dislikeCount":"0"}}]}""";

    private sealed class CannedGoogleHttpClientFactory(HttpMessageHandler handler) : Google.Apis.Http.HttpClientFactory
    {
        protected override HttpMessageHandler CreateHandler(Google.Apis.Http.CreateHttpClientArgs args) => handler;
    }

    private sealed class FixedBodyHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

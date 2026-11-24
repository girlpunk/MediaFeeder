using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Text;
using TickerQ.Utilities.Base;

namespace MediaFeeder.Providers.RSS;

// Verifies bug med-11 (bugs/medium/med-11-rss-provider-persistent-failures.md):
// 11a - an ISO 8601 <itunes:duration> makes TimeSpan.Parse throw FormatException, aborting
//       the whole feed sync on that item, every run;
// 11b - an empty feed makes SyndicationFeed.Load return null and the feed title access NRE;
// 11c - the absolute feed-image URL is stored into the 100-char Subscription.Thumb column.
public sealed class RssSubscriptionSynchroniseConsumerTests
{
    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions =
        new DbContextOptionsBuilder<MediaFeederDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task ExecuteAsync_Iso8601Duration_ItemImportedWithoutAbortingFeed()
    {
        var handler = new FixedBodyHandler(Iso8601DurationFeed, "application/rss+xml");
        var subscriptionId = await SeedSubscriptionAsync();
        var consumer = CreateConsumer(handler);

        var syncContext = new TickerFunctionContext<SynchroniseSubscriptionContract>(
            new TickerFunctionContext(),
            new SynchroniseSubscriptionContract(subscriptionId));

        await Should.NotThrowAsync(() => consumer.ExecuteAsync(syncContext, CancellationToken.None));

        await using var db = new MediaFeederDataContext(_contextOptions);
        var episode = await db.Videos.SingleOrDefaultAsync(static v => v.VideoId == "https://media.example.com/p/ep1");

        episode.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_EmptyFeed_DoesNotThrowAndAdvancesWatermark()
    {
        var handler = new FixedBodyHandler(string.Empty, "application/rss+xml");
        var subscriptionId = await SeedSubscriptionAsync();
        var consumer = CreateConsumer(handler);

        var syncContext = new TickerFunctionContext<SynchroniseSubscriptionContract>(
            new TickerFunctionContext(),
            new SynchroniseSubscriptionContract(subscriptionId));

        await Should.NotThrowAsync(() => consumer.ExecuteAsync(syncContext, CancellationToken.None));

        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = await db.Subscriptions.SingleAsync(s => s.Id == subscriptionId);

        subscription.LastSynchronised.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_OverlongFeedImageUrl_ThumbRespectsColumnLimit()
    {
        var handler = new FixedBodyHandler(OverlongFeedImageUrlFeed, "application/rss+xml");
        var subscriptionId = await SeedSubscriptionAsync();
        var consumer = CreateConsumer(handler);

        var syncContext = new TickerFunctionContext<SynchroniseSubscriptionContract>(
            new TickerFunctionContext(),
            new SynchroniseSubscriptionContract(subscriptionId));

        await consumer.ExecuteAsync(syncContext, CancellationToken.None);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = await db.Subscriptions.SingleAsync(s => s.Id == subscriptionId);

        if (subscription.Thumb != null)
            subscription.Thumb.Length.ShouldBeLessThanOrEqualTo(100);
    }

    private async Task<int> SeedSubscriptionAsync()
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = new Subscription
        {
            Name = "My Podcast",
            ChannelName = "Original Podcast Name",
            PlaylistId = "https://feeds.example.com/feed1",
            ChannelId = "https://feeds.example.com/feed1",
            Description = "A test feed",
            Provider = "RSS",
            UserId = 1,
            ParentFolderId = 1,
            LastSynchronised = DateTimeOffset.UtcNow.AddHours(-25),
        };
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription.Id;
    }

    private RSSSubscriptionSynchroniseConsumer CreateConsumer(HttpMessageHandler feedHandler)
    {
        var contextFactory = new TestDbContextFactory(_contextOptions);

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("retry").Returns(new HttpClient(feedHandler));

        var meterFactory = Substitute.For<System.Diagnostics.Metrics.IMeterFactory>();
        meterFactory.Create(Arg.Any<System.Diagnostics.Metrics.MeterOptions>()).Returns(new System.Diagnostics.Metrics.Meter("MediaFeeder-tests"));
        var metrics = new Metrics(meterFactory, contextFactory);

        return new RSSSubscriptionSynchroniseConsumer(
            Substitute.For<ILogger<RSSSubscriptionSynchroniseConsumer>>(),
            contextFactory,
            httpClientFactory,
            metrics);
    }

    private const string Iso8601DurationFeed = """
        <rss version="2.0" xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd">
          <channel>
            <title>Test Feed</title>
            <link>https://media.example.com/</link>
            <description>A test feed</description>
            <item>
              <title>Episode One</title>
              <link>https://media.example.com/p/ep1</link>
              <guid>https://media.example.com/p/ep1</guid>
              <description>First episode</description>
              <pubDate>Mon, 02 Jun 2025 10:00:00 +0000</pubDate>
              <itunes:duration>PT1H2M3S</itunes:duration>
            </item>
          </channel>
        </rss>
        """;

    private const string OverlongFeedImageUrlFeed = """
        <rss version="2.0">
          <channel>
            <title>Test Feed</title>
            <link>https://media.example.com/</link>
            <description>A test feed</description>
            <image>
              <url>https://img.example.com/cdn/thumbnail/assets/abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.png</url>
              <title>Test Feed</title>
              <link>https://media.example.com/</link>
            </image>
          </channel>
        </rss>
        """;

    private sealed class FixedBodyHandler(string body, string mediaType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        });
    }
}

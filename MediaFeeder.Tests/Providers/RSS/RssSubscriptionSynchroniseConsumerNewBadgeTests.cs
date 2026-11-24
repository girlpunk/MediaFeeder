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

// Verifies bug med-12 (bugs/medium/med-12-new-badge-inverted-ccc-rss.md):
// the "New" badge must stay on videos published within the last day and be cleared once a
// video is at least a day old. The RSS consumer's badge-clearing predicate is inverted:
// it clears fresh videos and keeps the badge on stale ones. The same inversion in the CCC
// consumer is covered by CccSubscriptionSynchroniseConsumerNewBadgeTests.
public sealed class RssSubscriptionSynchroniseConsumerNewBadgeTests
{
    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions =
        new DbContextOptionsBuilder<MediaFeederDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task ExecuteAsync_MixedBadgeAges_KeepsBadgeOnFreshVideoAndClearsBadgeOnVideoOlderThanOneDay()
    {
        var handler = new FixedFeedHandler();
        var subscriptionId = await SeedAsync();
        var consumer = CreateConsumer(handler);

        var syncContext = new TickerFunctionContext<SynchroniseSubscriptionContract>(
            new TickerFunctionContext(),
            new SynchroniseSubscriptionContract(subscriptionId));

        await consumer.ExecuteAsync(syncContext, CancellationToken.None);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var fresh = await db.Videos.SingleAsync(static v => v.VideoId == "fresh-video");
        var stale = await db.Videos.SingleAsync(static v => v.VideoId == "stale-video");

        fresh.New.ShouldBeTrue();
        stale.New.ShouldBeFalse();
    }

    private async Task<int> SeedAsync()
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

        db.Videos.Add(new Video
        {
            VideoId = "fresh-video",
            Name = "Fresh",
            Description = "A fresh video",
            UploaderName = "Speaker",
            SubscriptionId = subscription.Id,
            PublishDate = DateTimeOffset.UtcNow.AddHours(-2),
            New = true,
        });
        db.Videos.Add(new Video
        {
            VideoId = "stale-video",
            Name = "Stale",
            Description = "A stale video",
            UploaderName = "Speaker",
            SubscriptionId = subscription.Id,
            PublishDate = DateTimeOffset.UtcNow.AddDays(-5),
            New = true,
        });
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

    private const string FeedXml = """
        <rss version="2.0">
          <channel>
            <title>Test Feed</title>
            <link>https://media.example.com/</link>
            <description>A test feed</description>
          </channel>
        </rss>
        """;

    private sealed class FixedFeedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(FeedXml, Encoding.UTF8, "application/rss+xml"),
        });
    }
}

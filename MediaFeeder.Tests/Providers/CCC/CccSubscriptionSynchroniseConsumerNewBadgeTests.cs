using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using TickerQ.Utilities.Base;

namespace MediaFeeder.Providers.CCC;

// Verifies bug med-12 (bugs/medium/med-12-new-badge-inverted-ccc-rss.md)
// The "New" badge must be kept on freshly published videos and cleared only once a
// video is at least a day old (as the YouTube consumer already does). Today the CCC
// predicate is inverted: fresh videos lose the badge immediately while 2-7 day old
// videos keep it forever.
public sealed class CccSubscriptionSynchroniseConsumerNewBadgeTests
{
    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions = new DbContextOptionsBuilder<MediaFeederDataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task ExecuteAsync_MixedBadgeAges_KeepsBadgeOnFreshVideoAndClearsBadgeOnVideoOlderThanOneDay()
    {
        var watermark = DateTimeOffset.UtcNow.AddHours(-2);
        var freshPublish = DateTimeOffset.UtcNow.AddHours(-2);
        var stalePublish = DateTimeOffset.UtcNow.AddDays(-5);

        var handler = new EmptyEventsPageHandler();
        var subscriptionId = await SeedAsync(watermark, freshPublish, stalePublish);
        var consumer = CreateConsumer(handler);

        var context = new TickerFunctionContext<SynchroniseSubscriptionContract>(
            new TickerFunctionContext(),
            new SynchroniseSubscriptionContract(subscriptionId)
        );

        await consumer.ExecuteAsync(context, CancellationToken.None);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var fresh = await db.Videos.SingleAsync(v => v.VideoId == "fresh-video");
        var stale = await db.Videos.SingleAsync(v => v.VideoId == "stale-video");

        fresh.New.ShouldBeTrue();
        stale.New.ShouldBeFalse();
    }

    private async Task<int> SeedAsync(DateTimeOffset lastSynchronised, DateTimeOffset freshPublish, DateTimeOffset stalePublish)
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = new Subscription
        {
            Name = "Conference",
            ChannelName = "Conference",
            PlaylistId = "https://api.media.ccc.de/public/events/",
            ChannelId = "https://api.media.ccc.de/public/events/",
            Description = "",
            Provider = "CCC",
            UserId = 1,
            ParentFolderId = 1,
            LastSynchronised = lastSynchronised,
        };
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();

        db.Videos.Add(new Video
        {
            VideoId = "fresh-video",
            Name = "Fresh",
            Description = "",
            UploaderName = "Speaker",
            SubscriptionId = subscription.Id,
            PublishDate = freshPublish,
            New = true,
        });
        db.Videos.Add(new Video
        {
            VideoId = "stale-video",
            Name = "Stale",
            Description = "",
            UploaderName = "Speaker",
            SubscriptionId = subscription.Id,
            PublishDate = stalePublish,
            New = true,
        });
        await db.SaveChangesAsync();
        return subscription.Id;
    }

    private CCCSubscriptionSynchroniseConsumer CreateConsumer(HttpMessageHandler handler)
    {
        var contextFactory = new TestDbContextFactory(_contextOptions);

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("retry").Returns(new HttpClient(handler));

        var meterFactory = Substitute.For<System.Diagnostics.Metrics.IMeterFactory>();
        meterFactory
            .Create(Arg.Any<System.Diagnostics.Metrics.MeterOptions>())
            .Returns(new System.Diagnostics.Metrics.Meter("MediaFeeder-test"));
        var metrics = new Metrics(meterFactory, contextFactory);

        return new CCCSubscriptionSynchroniseConsumer(
            Substitute.For<ILogger<CCCSubscriptionSynchroniseConsumer>>(),
            contextFactory,
            httpClientFactory,
            metrics
        );
    }

    private sealed class EmptyEventsPageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var content = new StringContent("""{"events":[]}""");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}

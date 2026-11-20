using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using TickerQ.Utilities.Base;

namespace MediaFeeder.Providers.CCC;

// Verifies bug func-02 (bugs/functional/func-02-ccc-full-archive-walk.md)
// An "All Videos" sync must stop paginating once a page holds no events newer than
// the watermark. Today the stop condition is inverted: any page containing an event
// OLDER than the watermark keeps the walk going, so the entire archive is re-fetched
// on every sync (until a completely empty page).
public sealed class CccSubscriptionSynchroniseConsumerPaginationTests
{
    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions = new DbContextOptionsBuilder<MediaFeederDataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task ExecuteAsync_AllVideosSyncWithStaleEvents_StopsAfterFirstPageWithoutNewerEvents()
    {
        var watermark = DateTimeOffset.UtcNow.AddHours(-2);
        var newEventAt = DateTimeOffset.UtcNow.AddHours(-1);
        var oldEventAt = DateTimeOffset.UtcNow.AddHours(-10);

        var handler = new CccEventsArchiveHandler(
            pageZero: [
                Spec("evt-new", newEventAt, "New talk"),
                Spec("evt-old", oldEventAt, "Old talk"),
            ],
            pageOne: [Spec("evt-old2", oldEventAt, "Older talk")]
        );

        var subscriptionId = await SeedSubscriptionAsync(watermark);
        var consumer = CreateConsumer(handler);

        var context = new TickerFunctionContext<SynchroniseSubscriptionContract>(
            new TickerFunctionContext(),
            new SynchroniseSubscriptionContract(subscriptionId)
        );

        await consumer.ExecuteAsync(context, CancellationToken.None);

        // The corrected stop condition ends the walk as soon as a page contains no events
        // newer than the watermark: page 0 (has a newer event) is followed by page 1 (no
        // newer events) and nothing beyond, so only two archive pages are fetched. The
        // buggy inverted condition keeps walking until an empty page is returned, fetching
        // a third archive page (page 2) here. Counted archive pages only — the per-event
        // recordings detail fetch for evt-new is not a paginated archive request.
        handler.PageRequestCount.ShouldBe(2);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = await db.Subscriptions.SingleAsync(s => s.Id == subscriptionId);

        (await db.Videos.AnyAsync(v => v.VideoId == "evt-new")).ShouldBeTrue();
        (await db.Videos.AnyAsync(v => v.VideoId == "evt-old")).ShouldBeFalse();
        (await db.Videos.AnyAsync(v => v.VideoId == "evt-old2")).ShouldBeFalse();
        subscription.LastSynchronised!.Value.ShouldBeGreaterThan(watermark);
    }

    private async Task<int> SeedSubscriptionAsync(DateTimeOffset lastSynchronised)
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = new Subscription
        {
            Name = "CCC Media - All Videos - HD English",
            ChannelName = "CCC Media - All Videos - HD English",
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
        return subscription.Id;
    }

    private CCCSubscriptionSynchroniseConsumer CreateConsumer(CccEventsArchiveHandler handler)
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

    private static EventSpec Spec(string guid, DateTimeOffset updatedAt, string title) => new(guid, updatedAt, title);

    private sealed record EventSpec(string Guid, DateTimeOffset UpdatedAt, string Title)
    {
        public string Json => JsonSerializer.Serialize(
            new
            {
                guid = Guid,
                title = Title,
                subtitle = "Subtitle",
                conference_title = "38C3",
                date = UpdatedAt.ToString("O"),
                updated_at = UpdatedAt.ToString("O"),
                url = "https://api.media.ccc.de/events/" + Guid + "/",
            }
        );
    }

    private sealed class CccEventsArchiveHandler(
        IReadOnlyList<EventSpec> pageZero,
        IReadOnlyList<EventSpec> pageOne
    ) : HttpMessageHandler
    {
        private readonly List<string> _requests = [];

        private int _pageRequests;

        public int RequestCount => _requests.Count;

        // Number of paginated archive page fetches (query carries "page="). This is what the
        // stop condition under test controls; the per-event recordings lookup is separate.
        public int PageRequestCount => _pageRequests;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            _requests.Add(request.RequestUri!.ToString());

            if (request.RequestUri!.AbsolutePath.StartsWith("/events/", StringComparison.Ordinal))
                return Json("""{"recordings":[]}""");

            _pageRequests++;
            var pageValue = int.Parse(
                request.RequestUri!.Query.Split("page=", 2)[1],
                System.Globalization.CultureInfo.InvariantCulture
            );
            var page = pageValue switch
            {
                0 => pageZero,
                1 => pageOne,
                _ => Array.Empty<EventSpec>(),
            };
            var events = "[" + string.Join(", ", page.Select(static e => e.Json)) + "]";
            return Json("""{"events":""" + events + "}");
        }

        private static Task<HttpResponseMessage> Json(string body)
        {
            var content = new StringContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}

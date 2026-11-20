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

// Verifies bug med-13 (bugs/medium/med-13-ccc-single-event-stall.md)
// A single event whose recording lookup fails (non-JSON 5xx here) must not abort
// the whole sync: the remaining new events must still be imported and the
// watermark must advance, otherwise the subscription re-fetches the same page,
// re-hits the same bad event on every run, and is permanently stuck.
public sealed class CCCSingleEventStallTests
{
    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions = new DbContextOptionsBuilder<MediaFeederDataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task ExecuteAsync_OneEventWithUnreadableRecordings_OtherEventsStillImportedAndWatermarkAdvances()
    {
        var watermark = DateTimeOffset.UtcNow.AddHours(-2);
        var eventAt = DateTimeOffset.UtcNow.AddHours(-1);

        var handler = new EventsWithBrokenDetailHandler(
            [
                Spec("evt-bad", eventAt),
                Spec("evt-good", eventAt),
            ],
            brokenGuid: "evt-bad"
        );

        var subscriptionId = await SeedSubscriptionAsync(watermark);
        var consumer = CreateConsumer(handler);
        var context = new TickerFunctionContext<SynchroniseSubscriptionContract>(
            new TickerFunctionContext(),
            new SynchroniseSubscriptionContract(subscriptionId)
        );

        // The per-item lookup for evt-bad must be contained (logged and skipped);
        // a single-event failure must not throw out of ExecuteAsync. Today the
        // failure is rethrown as ApplicationException and aborts the whole run.
        await Should.NotThrowAsync(() => consumer.ExecuteAsync(context, CancellationToken.None));

        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = await db.Subscriptions.SingleAsync(s => s.Id == subscriptionId);

        (await db.Videos.AnyAsync(static v => v.VideoId == "evt-good")).ShouldBeTrue();
        subscription.LastSynchronised!.Value.ShouldBeGreaterThan(watermark);
    }

    private async Task<int> SeedSubscriptionAsync(DateTimeOffset lastSynchronised)
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
        return subscription.Id;
    }

    private CCCSubscriptionSynchroniseConsumer CreateConsumer(EventsWithBrokenDetailHandler handler)
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

    private static EventSpec Spec(string guid, DateTimeOffset updatedAt) => new(guid, updatedAt);

    private sealed record EventSpec(string Guid, DateTimeOffset UpdatedAt)
    {
        public string Json => JsonSerializer.Serialize(
            new
            {
                guid = Guid,
                title = "Title",
                subtitle = "Subtitle",
                conference_title = "38C3",
                date = UpdatedAt.ToString("O"),
                updated_at = UpdatedAt.ToString("O"),
                url = "https://api.media.ccc.de/events/" + Guid + "/",
            }
        );
    }

    private sealed class EventsWithBrokenDetailHandler(EventSpec[] events, string brokenGuid) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.StartsWith("/events/", StringComparison.Ordinal))
                return path.Contains(brokenGuid, StringComparison.Ordinal)
                    ? Broken()
                    : Json("""{"recordings":[]}""");

            var eventsJson = "[" + string.Join(", ", events.Select(static e => e.Json)) + "]";
            return Json("""{"events":""" + eventsJson + "}");
        }

        private static Task<HttpResponseMessage> Broken()
        {
            var content = new StringContent("internal error, not json");
            content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = content });
        }

        private static Task<HttpResponseMessage> Json(string body)
        {
            var content = new StringContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}

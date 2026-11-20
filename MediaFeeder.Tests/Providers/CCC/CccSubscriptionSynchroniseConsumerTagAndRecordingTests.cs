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

// Verifies bug low-09 (bugs/low/low-09-ccc-duplicate-tags-fallbacks.md)
// (a) repeated keywords in one event must produce a single VideoTag row, and
// (b) the "best recording" fallback chain must fall through to non-MP4 containers
// instead of leaving DownloadedPath null when only non-MP4 recordings exist.
public sealed class CccSubscriptionSynchroniseConsumerTagAndRecordingTests
{
    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions = new DbContextOptionsBuilder<MediaFeederDataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task ExecuteAsync_EventWithRepeatedKeywordStoresSingleTagRowPerKeyword()
    {
        var handler = new CccPageHandler(
            events: [
                Spec("dup-tags", DateTimeOffset.UtcNow.AddHours(-1), "Talk with tags", ["linux, linux", "dotnet"]),
            ],
            recordings: "[]"
        );

        await RunSyncAsync(handler);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var video = await db
            .Videos.Include(static v => v.Tags)
            .SingleAsync(static v => v.VideoId == "dup-tags");

        video.Tags.Count(static t => t.Tag == "linux").ShouldBe(1);
        video.Tags.Count(static t => t.Tag == "dotnet").ShouldBe(1);
        video.Tags.Select(static t => t.Tag)
            .ShouldBeEquivalentTo(new[] { "linux", "dotnet" });
    }

    [Fact]
    public async Task ExecuteAsync_EventWithOnlyNonMp4Recordings_SelectsBestQualityRecording()
    {
        var handler = new CccPageHandler(
            events: [
                Spec("non-mp4", DateTimeOffset.UtcNow.AddHours(-1), "Talk with ogg recordings", []),
            ],
            recordings:
                """
                [
                    {"language":"deu","mime_type":"video/ogg","width":1920,"height":1080,"recording_url":"https://downloads.example.com/1080.ogv"},
                    {"language":"deu","mime_type":"video/ogg","width":1280,"height":720,"recording_url":"https://downloads.example.com/720.ogv"}
                ]
                """
        );

        await RunSyncAsync(handler);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var video = await db.Videos.SingleAsync(static v => v.VideoId == "non-mp4");

        // With only non-MP4 recordings available, the best (highest resolution)
        // recording must still be selected instead of DownloadedPath staying null.
        video.DownloadedPath.ShouldBe("https://downloads.example.com/1080.ogv");
    }

    private async Task RunSyncAsync(CccPageHandler handler)
    {
        int subscriptionId;
        await using (var db = new MediaFeederDataContext(_contextOptions))
        {
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
                LastSynchronised = DateTimeOffset.UtcNow.AddHours(-2),
            };
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();
            subscriptionId = subscription.Id;
        }

        var consumer = CreateConsumer(handler);
        var context = new TickerFunctionContext<SynchroniseSubscriptionContract>(
            new TickerFunctionContext(),
            new SynchroniseSubscriptionContract(subscriptionId)
        );
        await consumer.ExecuteAsync(context, CancellationToken.None);
    }

    private CCCSubscriptionSynchroniseConsumer CreateConsumer(CccPageHandler handler)
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

    private static EventSpec Spec(string guid, DateTimeOffset updatedAt, string title, string[] tags)
        => new(guid, updatedAt, title, tags);

    private sealed record EventSpec(string Guid, DateTimeOffset UpdatedAt, string Title, string[] Tags)
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
                tags = Tags,
            }
        );
    }

    private sealed class CccPageHandler(EventSpec[] events, string recordings) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (request.RequestUri!.AbsolutePath.StartsWith("/events/", StringComparison.Ordinal))
                return Json("{" + recordings + "}");

            return Json("""{"events":""" + "[" + string.Join(", ", events.Select(static e => e.Json)) + "]}");
        }

        private static Task<HttpResponseMessage> Json(string body)
        {
            var content = new StringContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}

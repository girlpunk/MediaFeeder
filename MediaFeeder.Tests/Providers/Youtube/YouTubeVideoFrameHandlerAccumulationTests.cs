using System.Reflection;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using MediaFeeder;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.PlaybackManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Shouldly;

namespace MediaFeeder.Providers.Youtube;

// Verifies bug func-05 (bugs/functional/func-05-youtube-frame-handler-accumulation.md).
//
// The /video/{id} page reuses the same YouTubeVideoFrame instance (DynamicComponent) for every
// "next" video, so OnParametersSetAsync re-runs for each video. Its guard
// (Video?.Id != _playingVideo) is exactly the new-video condition, and the body re-subscribes
// the four control handlers (PlayPause/Seek/ChangeRate/ChangeVolume) to the session reference's
// events without ever removing the old ones; Dispose never unsubscribes either. After N videos
// in a session, one key press / seek / rate / volume change fires N stacked handlers (with an
// even N the play/pause toggle cancels itself out), and the try/catch-less async-void handlers
// turn any JS fault (including one after the player was torn down) into an unobserved exception
// that typically drops the Blazor circuit.
//
// The tests drive OnParametersSetAsync directly - it is the method the router triggers when the
// video parameter changes - against a real PlaybackSessionReference obtained from a real
// PlaybackSessionManager (InMemory DbContext), and count the handlers attached to the reference's
// public events via their compiler-generated backing fields (same technique as the func-04 tests).
// They are expected to FAIL against the current code:
//
//   1. Advancing through 3 videos must leave exactly one play/pause, one seek, one rate and one
//      volume handler on the session reference (currently: one per video, forever).
//   2. Disposing the frame after advancing must remove its handlers from the session reference's
//      events (currently: they are never unsubscribed).
//
// The "SameVideoReused" test passes today and must keep passing: it locks in the existing guard
// so the fix (subscribe once per session/reference) cannot regress into an unconditional
// re-subscription. The unobserved-fault aspect (async-void handlers without try/catch) is not
// unit-observable here: on a detached component the fault becomes an unobserved exception, so
// it is deliberately not covered.

public sealed class YouTubeVideoFrameHandlerAccumulationTests
{
    private readonly IFixture _fixture = new Fixture().Customize(new AutoNSubstituteCustomization());

    [Fact]
    public async Task OnParametersSetAsync_SameVideoReused_ShouldSubscribePlayPauseHandlerOnce()
    {
        using var harness = CreateHarness();

        await harness.DriveVideoAsync(1);
        await harness.DriveVideoAsync(1); // a router re-render with the same video

        CountEventHandlers(harness.Reference, "PlayPauseEvent")
            .ShouldBe(1, "re-driving the same video must not subscribe the control handlers a second time");
    }

    [Fact]
    public async Task OnParametersSetAsync_AdvancingThroughVideos_ShouldNotStackPlayPauseHandlers()
    {
        using var harness = CreateHarness();

        for (var videoId = 1; videoId <= 3; videoId++)
            await harness.DriveVideoAsync(videoId);

        CountEventHandlers(harness.Reference, "PlayPauseEvent")
            .ShouldBe(
                1,
                "advancing through videos must not stack the play/pause handler on the session reference; one key press must not fire N playVideo/pauseVideo JS calls");
    }

    [Fact]
    public async Task OnParametersSetAsync_AdvancingThroughVideos_ShouldNotStackSeekHandlers()
    {
        using var harness = CreateHarness();

        for (var videoId = 1; videoId <= 3; videoId++)
            await harness.DriveVideoAsync(videoId);

        CountEventHandlers(harness.Reference, "SeekRelativeEvent")
            .ShouldBe(
                1,
                "advancing through videos must not stack the seek handler on the session reference; one seek must not be applied N times");
    }

    [Fact]
    public async Task OnParametersSetAsync_AdvancingThroughVideos_ShouldNotStackChangeRateHandlers()
    {
        using var harness = CreateHarness();

        for (var videoId = 1; videoId <= 3; videoId++)
            await harness.DriveVideoAsync(videoId);

        CountEventHandlers(harness.Reference, "ChangeRateEvent")
            .ShouldBe(
                1,
                "advancing through videos must not stack the rate-change handler on the session reference");
    }

    [Fact]
    public async Task OnParametersSetAsync_AdvancingThroughVideos_ShouldNotStackChangeVolumeHandlers()
    {
        using var harness = CreateHarness();

        for (var videoId = 1; videoId <= 3; videoId++)
            await harness.DriveVideoAsync(videoId);

        CountEventHandlers(harness.Reference, "ChangeVolumeEvent")
            .ShouldBe(
                1,
                "advancing through videos must not stack the volume-change handler on the session reference");
    }

    [Fact]
    public async Task Dispose_AfterAdvancingThroughVideos_ShouldUnsubscribeAllControlHandlers()
    {
        using var harness = CreateHarness();

        await harness.DriveVideoAsync(1);
        await harness.DriveVideoAsync(2);
        // Blazor unmount runs both IDisposable and IAsyncDisposable; the bug report names the sync
        // Dispose as the one that never unsubscribes, so drive it explicitly, then the async path.
#pragma warning disable CA1849 // Intentionally exercising the sync Dispose() contract.
        harness.Frame.Dispose();
#pragma warning restore CA1849
        await harness.Frame.DisposeAsync();

        foreach (var eventName in new[] { "PlayPauseEvent", "SeekRelativeEvent", "ChangeRateEvent", "ChangeVolumeEvent" })
        {
            CountEventHandlers(harness.Reference, eventName)
                .ShouldBe(0, $"disposing the frame must remove its {eventName} subscription; the handlers must not keep firing after the component is gone");
        }
    }

    private FrameHarness CreateHarness()
    {
        var options = new DbContextOptionsBuilder<MediaFeederDataContext>()
            .UseInMemoryDatabase("mediafeeder-func05-" + Guid.NewGuid().ToString("N"))
            .Options;
        var manager = new PlaybackSessionManager(
            new TestDbContextFactory(options),
            _fixture.Create<ILogger<PlaybackSession>>()
        );
        var user = new AuthUser { Id = 1, Username = "func05-user", Email = "func05-user@example.com" };
        var reference = manager.NewSession("func05-player", user);

#pragma warning disable BL0005 // Unit test drives the component without a renderer, so parameters are set directly.
        var frame = new YouTubeVideoFrame { PlaybackSession = reference };
#pragma warning restore BL0005
        SetPrivateField(frame, "_player", _fixture.Create<IJSObjectReference>());

        return new FrameHarness(frame, reference, manager);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{target.GetType().Name}.{fieldName} was renamed or removed.");

        field.SetValue(target, value);
    }

    private static Task InvokeOnParametersSetAsync(YouTubeVideoFrame frame)
    {
        var method = typeof(YouTubeVideoFrame).GetMethod("OnParametersSetAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("YouTubeVideoFrame.OnParametersSetAsync (protected) was renamed or removed.");

        return (Task)method.Invoke(frame, null)!;
    }

    private static int CountEventHandlers(PlaybackSession.PlaybackSessionReference reference, string eventName)
    {
        var referenceType = typeof(PlaybackSession.PlaybackSessionReference);
        // Compiler versions differ on auto-event backing field names: plain name or <name>k__BackingField.
        var backingField = referenceType.GetField(eventName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            ?? referenceType.GetField($"<{eventName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Event {eventName} no longer has its compiler-generated backing field.");

        return ((Delegate?) backingField.GetValue(reference))?.GetInvocationList().Length ?? 0;
    }

    private sealed class FrameHarness : IDisposable
    {
        public FrameHarness(YouTubeVideoFrame frame, PlaybackSession.PlaybackSessionReference reference, PlaybackSessionManager manager)
        {
            Frame = frame;
            Reference = reference;
            Manager = manager;
        }

        public YouTubeVideoFrame Frame { get; }

        public PlaybackSession.PlaybackSessionReference Reference { get; }

        public PlaybackSessionManager Manager { get; }

        public Task DriveVideoAsync(int videoId)
        {
#pragma warning disable BL0005 // Unit test drives the component without a renderer, so parameters are set directly.
            Frame.Video = new Video
            {
                Id = videoId,
                VideoId = "video-" + videoId,
                Name = "Video " + videoId,
                Description = "test video",
                UploaderName = "Uploader",
                SubscriptionId = 1,
            };
#pragma warning restore BL0005
            return InvokeOnParametersSetAsync(Frame);
        }

        public void Dispose()
        {
            // Unsubscribes the reference from the session (releases the session's player timer),
            // then disposes the sessions (and their timers). Both objects are created in this
            // harness and released here.
#pragma warning disable IDISP007
            Reference.Dispose();
            Manager.Dispose();
#pragma warning restore IDISP007
        }
    }
}

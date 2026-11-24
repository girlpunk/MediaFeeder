using MediaFeeder.Providers.Youtube;
using Microsoft.JSInterop;
using NSubstitute;
using Shouldly;
using System.Reflection;
using System.Text.Json;

namespace MediaFeeder.Providers;

// Verifies bug low-15 (bugs/low/low-15-dead-code-noise.md): the Console.WriteLine noise on
// every playback event (DownloadedVideoFrame) and on player-ready (YouTubeVideoFrame).
// The commented-out Python block, the fully commented SynchroniseAllContract.cs and the
// no-op `using Timer = Timer;` alias have no runtime behavior, so they cannot be tested
// in unit scope.
public sealed class VideoFrameConsoleNoiseTests
{
    [Fact]
    public void DownloadedVideoFrame_EventHandlers_DoNotWriteToConsole()
    {
        var frame = new DownloadedVideoFrame();

        var output = CaptureConsole(() =>
        {
            var type = typeof(DownloadedVideoFrame);
            var attributes = BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var name in new[] { "CanPlay", "Ended", "CanPlayThrough", "Abort", "Error", "LoadedData" })
                type.GetMethod(name, attributes)!.Invoke(frame, new object?[] { null });
        });

        output.ShouldBeEmpty();
    }

    [Fact]
    public async Task YouTubeVideoFrame_OnPlayerReady_DoesNotWriteToConsole()
    {
        var component = new YouTubeVideoFrame();
        var target = Substitute.For<IJSObjectReference>();
        var data = JsonSerializer.SerializeToElement(new
        {
            ready = true,
        });

        var output = await CaptureConsoleAsync(async () =>
        {
            await Should.ThrowAsync<ArgumentNullException>(
                () => component.OnPlayerReady(target, data));
        });

        output.ShouldBeEmpty();
    }

    private static string CaptureConsole(Action action)
    {
        var original = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static async Task<string> CaptureConsoleAsync(Func<Task> action)
    {
        var original = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            await action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}

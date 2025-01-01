using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MediaFeeder.Providers.Youtube;

public partial class VideoFrame
{
    [Parameter] public string? VideoId { get; set; }
    private IJSObjectReference? _youtubeCustomModule;
    private IJSObjectReference? _youtubeLibraryModule;
    private DotNetObjectReference<VideoFrame>? _videoFrameRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _youtubeLibraryModule = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "/iframe_api.js");
            _youtubeCustomModule = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Providers/Youtube/VideoFrame.razor.js");

            Console.WriteLine($"_youtubeLibraryModule: {JsonSerializer.Serialize(_youtubeLibraryModule)}");
            Console.WriteLine($"_youtubeCustomModule: {JsonSerializer.Serialize(_youtubeCustomModule)}");

            _videoFrameRef ??= DotNetObjectReference.Create(this);
            var player = await _youtubeCustomModule.InvokeAsync<object>("initPlayer", _videoFrameRef, VideoId);

            Console.WriteLine($"player: {JsonSerializer.Serialize(player)}");
        }
    }

    [JSInvokable]
    public Task OnPlayerReady(JsonElement eventData)
    {
        Console.WriteLine($"Player Ready: {JsonSerializer.Serialize(eventData)}");
        // event.target.playVideo();

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnPlaybackQualityChange(JsonElement eventData)
    {
        Console.WriteLine($"Quality change: {JsonSerializer.Serialize(eventData)}");

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnPlaybackRateChange(object eventData)
    {
        Console.WriteLine($"Playback rate change: {JsonSerializer.Serialize(eventData)}");

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnError(object eventData)
    {
        Console.WriteLine($"Playback Error: {JsonSerializer.Serialize(eventData)}");

        // if(eventData.data == 150) {
        //     // "This error is the same as 101. It's just a 101 error in disguise!" - from the YT API Documentation, not 100% this is true.
        //     // Skip to the next video after 10 seconds, do not mark as watched.
        //     setTimeout(goNextVideo, 10 * 1000);
        // }

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnApiChange(object eventData)
    {
        Console.WriteLine($"API Change: {JsonSerializer.Serialize(eventData)}");

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnPlayerStateChange(JsonElement eventData)
    {
        Console.WriteLine($"State change: {JsonSerializer.Serialize(eventData)}");

        // if (event.data == YT.PlayerState.ENDED) {
        //     console.log("Video finished!");
        //     setWatchedStatus(1);
        // } else if (event.data == YT.PlayerState.UNSTARTED) {
        //     player.playVideo();
        // }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _videoFrameRef?.Dispose();
        _youtubeLibraryModule?.DisposeAsync();
        _youtubeCustomModule?.DisposeAsync();
    }
}
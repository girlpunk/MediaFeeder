using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MediaFeeder.Providers.Youtube;

public partial class YouTubeVideoFrame
{
    [Parameter] public string? VideoId { get; set; }
    private IJSObjectReference? _youtubeCustomModule;
    private IJSObjectReference? _youtubeLibraryModule;
    private DotNetObjectReference<YouTubeVideoFrame>? _videoFrameRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _youtubeLibraryModule = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "/iframe_api.js");
            _youtubeCustomModule = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Providers/Youtube/YouTubeVideoFrame.razor.js");

            Console.WriteLine($"_youtubeLibraryModule: {JsonSerializer.Serialize(_youtubeLibraryModule)}");
            Console.WriteLine($"_youtubeCustomModule: {JsonSerializer.Serialize(_youtubeCustomModule)}");

            _videoFrameRef ??= DotNetObjectReference.Create(this);
            var player = await _youtubeCustomModule.InvokeAsync<object>("initPlayer", _videoFrameRef, VideoId);

            Console.WriteLine($"player: {JsonSerializer.Serialize(player)}");
        }
    }

    [JSInvokable]
    public Task OnPlayerReady(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"Player Ready: {JsonSerializer.Serialize(data)}");

        // event.target.playVideo();
        target.InvokeVoidAsync("playVideo");

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnPlaybackQualityChange(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"Quality change: {JsonSerializer.Serialize(data)}");

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnPlaybackRateChange(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"Playback rate change: {JsonSerializer.Serialize(data)}");

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnError(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"Playback Error: {JsonSerializer.Serialize(data)}");

        if (data.GetInt32() == 150)
        {
            // "This error is the same as 101. It's just a 101 error in disguise!" - from the YT API Documentation, not 100% this is true.
            // Skip to the next video after 10 seconds, do not mark as watched.
            // setTimeout(goNextVideo, 10 * 1000);
        }

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnApiChange(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"API Change: {JsonSerializer.Serialize(data)}");

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnPlayerStateChange(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"State change: {JsonSerializer.Serialize(data)}");

        var state = (PlayerState) data.GetInt32();
        if (state == PlayerState.ENDED)
        {
            Console.WriteLine("Video finished!");
            // setWatchedStatus(1);
        }
        else if (state == PlayerState.UNSTARTED)
        {
            // player.playVideo();
            target.InvokeVoidAsync("playVideo");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _videoFrameRef?.Dispose();
        _youtubeLibraryModule?.DisposeAsync();
        _youtubeCustomModule?.DisposeAsync();
    }

    enum PlayerState
    {
        UNSTARTED = -1,
        ENDED = 0,
        PLAYING = 1,
        PAUSED = 2,
        BUFFERING = 3,
        CUED = 5
    }
}
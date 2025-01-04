using System.Text.Json;
using Microsoft.JSInterop;

namespace MediaFeeder.Providers.Youtube;

public sealed partial class YouTubeVideoFrame
{
    private IJSObjectReference? _youtubeCustomModule;
    private IJSObjectReference? _youtubeLibraryModule;
    private IJSObjectReference? _player;
    private DotNetObjectReference<YouTubeVideoFrame>? _videoFrameRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) //_player == null && Video != null)
        {
            ArgumentNullException.ThrowIfNull(Video);

            _youtubeLibraryModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "/iframe_api.js");
            _youtubeCustomModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Providers/Youtube/YouTubeVideoFrame.razor.js");

            _videoFrameRef ??= DotNetObjectReference.Create(this);
            await _youtubeCustomModule.InvokeVoidAsync("helperReady", _videoFrameRef);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        Console.WriteLine("Parameters set, should this load a new video?");

        if (_player != null)
        {
            ArgumentNullException.ThrowIfNull(Video);

            Console.WriteLine("Player is ready if it should");
            //await _player.InvokeVoidAsync("loadVideoById", Video.VideoId);
        }
    }

    [JSInvokable]
    public async Task OnLibraryLoaded()
    {
        ArgumentNullException.ThrowIfNull(_youtubeCustomModule);
        ArgumentNullException.ThrowIfNull(Video);

        _player = await _youtubeCustomModule.InvokeAsync<IJSObjectReference>("initPlayer", Video.VideoId);
    }

    [JSInvokable]
    public Task OnPlayerReady(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"Player Ready: {JsonSerializer.Serialize(data)}. Trying to play.");

        target.InvokeVoidAsync("playVideo");

        ArgumentNullException.ThrowIfNull(_player);
        _player.InvokeVoidAsync("playVideo");

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
    public async Task OnError(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"Playback Error: {JsonSerializer.Serialize(data)}");

        if (data.GetInt32() == 150)
        {
            // "This error is the same as 101. It's just a 101 error in disguise!" - from the YT API Documentation, not 100% this is true.
            // Skip to the next video after 10 seconds, do not mark as watched.
            await Task.Delay(TimeSpan.FromSeconds(10));

            if (Page != null)
                await Page.GoNext(false);
        }
    }

    [JSInvokable]
    public Task OnApiChange(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"API Change: {JsonSerializer.Serialize(data)}");

        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task OnPlayerStateChange(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"State change: {JsonSerializer.Serialize(data)}");

        var state = (PlayerState)
            data.GetInt32();

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (state)
        {
            case PlayerState.Ended:
            {
                // setWatchedStatus(1);
                Console.WriteLine("Video finished!");

                if (Page != null)
                    await Page.GoNext(true);
                break;
            }
            case PlayerState.Unstarted:
            {
                ArgumentNullException.ThrowIfNull(_player);

                await target.InvokeVoidAsync("playVideo");
                await _player.InvokeVoidAsync("playVideo");
                break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _videoFrameRef?.Dispose();
        if (_player != null) await _player.DisposeAsync();
        if (_youtubeLibraryModule != null) await _youtubeLibraryModule.DisposeAsync();
        if (_youtubeCustomModule != null) await _youtubeCustomModule.DisposeAsync();
    }
}

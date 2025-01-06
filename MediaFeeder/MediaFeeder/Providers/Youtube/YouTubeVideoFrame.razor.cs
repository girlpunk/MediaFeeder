using System.Text.Json;
using Humanizer;
using Microsoft.JSInterop;
using Timer = System.Threading.Timer;

namespace MediaFeeder.Providers.Youtube;

public sealed partial class YouTubeVideoFrame
{
    private IJSObjectReference? _youtubeCustomModule;
    private IJSObjectReference? _youtubeLibraryModule;
    private IJSObjectReference? _player;
    private DotNetObjectReference<YouTubeVideoFrame>? _videoFrameRef;

    private int? _playingVideo;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) //_player == null && Video != null)
        {
            ArgumentNullException.ThrowIfNull(Video);

            _youtubeCustomModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Providers/Youtube/YouTubeVideoFrame.razor.js");
            _youtubeLibraryModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "/iframe_api.js");

            _videoFrameRef ??= DotNetObjectReference.Create(this);
            await _youtubeCustomModule.InvokeVoidAsync("helperReady", _videoFrameRef);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_player != null && Video?.Id != _playingVideo)
        {
            ArgumentNullException.ThrowIfNull(Video);
            _playingVideo = Video.Id;

            await _player.InvokeVoidAsync("loadVideoById", Video.VideoId);
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

        var tsInterval = TimeSpan.FromSeconds(10);
        Timer = new Timer(ProgressUpdate, null, tsInterval, tsInterval);

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

            var toSkip = _playingVideo;

            await Task.Delay(TimeSpan.FromSeconds(10));

            if (Page != null && toSkip == _playingVideo)
                await Page.GoNext(false);
        }
    }

    [JSInvokable]
    public async Task OnPlayerStateChange(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"State change: {JsonSerializer.Serialize(data)}");

        var state = (PlayerState)
            data.GetInt32();

        if (PlaybackSession != null)
            PlaybackSession.State = state.Humanize();

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

    [JSInvokable]
    public async Task OnPlaybackQualityChange(IJSObjectReference target, JsonElement data)
    {
        Quality = data.ToString();
    }

    private string? Quality { get; set; }
    private Timer? Timer { get; set; }

    public void ProgressUpdate(object? sender)
    {
        Task.Run(async () =>
        {
            if (_player == null || PlaybackSession == null)
                return;

            var volume = await _player.InvokeAsync<int>("getVolume");
            var rate = await _player.InvokeAsync<float>("getPlaybackRate");
            var loaded = await _player.InvokeAsync<float?>("getVideoLoadedFraction");

            var status = new
            {
                volume, rate, loaded, Quality
            };

            var progress = await _player.InvokeAsync<float>("getCurrentTime");

            PlaybackSession.CurrentPosition = TimeSpan.FromSeconds(progress);
            PlaybackSession.Quality = JsonSerializer.Serialize(status);
        }).Wait();
    }

    public async ValueTask DisposeAsync()
    {
        _videoFrameRef?.Dispose();
        if (_player != null) await _player.DisposeAsync();
        if (_youtubeLibraryModule != null) await _youtubeLibraryModule.DisposeAsync();
        if (_youtubeCustomModule != null) await _youtubeCustomModule.DisposeAsync();

        if (Timer != null)
        {
            Timer.Change(Timeout.Infinite, Timeout.Infinite);
            await Timer.DisposeAsync();
        }
    }
}

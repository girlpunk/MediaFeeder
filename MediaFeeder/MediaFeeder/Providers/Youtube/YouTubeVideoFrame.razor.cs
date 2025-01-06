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
    private PlayerState _state;

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

            if (PlaybackSession != null)
                PlaybackSession.PlayPauseEvent += PlayPause;
        }
    }

    private async void PlayPause()
    {
        await InvokeAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(_player);

            switch (_state)
            {
                case PlayerState.Paused:
                    await _player.InvokeVoidAsync("playVideo");
                    break;
                case PlayerState.Playing:
                    await _player.InvokeVoidAsync("pauseVideo");
                    break;
            }
        });
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
        ProgressUpdate(this);

        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task OnError(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"Playback Error: {JsonSerializer.Serialize(data)}");

        ProgressUpdate(this);
        if (PlaybackSession != null)
            PlaybackSession.State = $"Error {data.ToString()}";

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

        _state = (PlayerState)data.GetInt32();

        if (PlaybackSession != null)
        {
            PlaybackSession.State = _state.Humanize();
            ProgressUpdate(this);
        }

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (_state)
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
    public void OnPlaybackQualityChange(IJSObjectReference target, JsonElement data)
    {
        if (PlaybackSession == null)
            return;

        PlaybackSession.Quality = data.ToString();
        ProgressUpdate(this);
    }

    private Timer? Timer { get; set; }

    public void ProgressUpdate(object? sender)
    {
        Task.Run(async () =>
        {
            if (_player == null || PlaybackSession == null)
                return;

            PlaybackSession.Volume = await _player.InvokeAsync<int>("getVolume");
            PlaybackSession.Rate = await _player.InvokeAsync<float>("getPlaybackRate");
            PlaybackSession.Loaded = await _player.InvokeAsync<float?>("getVideoLoadedFraction");

            var progress = await _player.InvokeAsync<float>("getCurrentTime");
            PlaybackSession.CurrentPosition = TimeSpan.FromSeconds(progress);
        });
    }

    public async ValueTask DisposeAsync()
    {
        ProgressUpdate(this);

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

using System.Text.Json;
using Microsoft.JSInterop;
using Timer = System.Threading.Timer;

namespace MediaFeeder.Providers.Youtube;

public sealed partial class YouTubeVideoFrame : IDisposable
{
    private IJSObjectReference? _youtubeCustomModule;
    private IJSObjectReference? _youtubeLibraryModule;
    private IJSObjectReference? _player;
    private DotNetObjectReference<YouTubeVideoFrame>? _videoFrameRef;

    private int? _playingVideo;
    private YtEmbeddedPlayerState _state;
    private bool _disposed;
    private int? _lastRestoredPositionVideoId;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _videoFrameRef?.Dispose();
        Timer?.Dispose();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) //_player == null && Video != null)
        {
            ArgumentNullException.ThrowIfNull(Video);

            _youtubeCustomModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./Providers/Youtube/YouTubeVideoFrame.razor.js");
            _youtubeLibraryModule ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", Assets["./iframe_api.js"]);

            _videoFrameRef ??= DotNetObjectReference.Create(this);
            await _youtubeCustomModule.InvokeVoidAsync("helperReady", _videoFrameRef);
        }
    }

    // runs after OnPlayerReady()
    protected override async Task OnParametersSetAsync()
    {
        if (_player != null && Video?.Id != _playingVideo)
        {
            ArgumentNullException.ThrowIfNull(Video);
            _playingVideo = Video.Id;

            await _player.InvokeVoidAsync("loadVideoById", Video.VideoId);

            if (PlaybackSession != null)
            {
                PlaybackSession.PlayPauseEvent += PlayPause;
                PlaybackSession.SeekRelativeEvent += Seek;
                PlaybackSession.ChangeRateEvent += ChangeRate;
                PlaybackSession.ChangeVolumeEvent += ChangeVolume;

                PlaybackSession.SupportsRateChange = true;
                PlaybackSession.SupportsSubtitles = false;
                PlaybackSession.SupportsVolumeChange = true;
            }
        }
    }

    private async void PlayPause()
    {
        await InvokeAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(_player);

            switch (_state)
            {
                case YtEmbeddedPlayerState.Paused:
                    await _player.InvokeVoidAsync("playVideo");
                    break;
                case YtEmbeddedPlayerState.Playing:
                    await _player.InvokeVoidAsync("pauseVideo");
                    break;
            }
        });
    }

    private async void Seek(int amount)
    {
        if (_player == null || PlaybackSession == null)
            return;

        var progress = await _player.InvokeAsync<float>("getCurrentTime");

        var seekTo = progress + amount;
        if (seekTo < 0)
            seekTo = 0;

        await _player.InvokeVoidAsync("seekTo", seekTo, true);

        ProgressUpdate(this);
    }

    private async void ChangeRate(bool direction)
    {
        if (_player == null)
            return;

        // Get available rates
        var availableRates = (await _player.InvokeAsync<float[]>("getAvailablePlaybackRates")).ToList();

        // Get current rate
        var currentRate = await _player.InvokeAsync<float>("getPlaybackRate");

        // Find next rate in appropriate direction
        var index = availableRates.IndexOf(currentRate);
        var nextIndex = direction ? index + 1 : index - 1;
        if (nextIndex < 0 || nextIndex >= availableRates.Count)
            return;

        // Update to new rate
        await _player.InvokeVoidAsync("setPlaybackRate", availableRates[nextIndex]);
    }

    private async void ChangeVolume(bool direction)
    {
        if (_player == null)
            return;

        // Get current volume
        var volume = await _player.InvokeAsync<float>("getVolume");
        var newVolume = direction ? volume + 10 : volume - 10;

        await _player.InvokeVoidAsync("setVolume", newVolume);
    }

    [JSInvokable]
    public async Task OnLibraryLoaded()
    {
        ArgumentNullException.ThrowIfNull(_youtubeCustomModule);
        ArgumentNullException.ThrowIfNull(Video);

        _player = await _youtubeCustomModule.InvokeAsync<IJSObjectReference>("initPlayer", Video.VideoId);
    }

    // runs before OnParametersSetAsync()
    [JSInvokable]
    public Task OnPlayerReady(IJSObjectReference target, JsonElement data)
    {
        Console.WriteLine($"Player Ready: {JsonSerializer.Serialize(data)}. Trying to play.");

        // TODO why target and _player?
        target.InvokeVoidAsync("playVideo");

        ArgumentNullException.ThrowIfNull(_player);
        _player.InvokeVoidAsync("playVideo");

        var tsInterval = TimeSpan.FromSeconds(10);
        Timer?.Dispose();
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
        {
            PlaybackSession.State = PlayerState.Unknown;
            PlaybackSession.Message = $"Error {data.ToString()}";
        }

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

        _state = (YtEmbeddedPlayerState) data.GetInt32();

        if (PlaybackSession != null)
        {
            PlaybackSession.State = _state.ToPlayState();
            PlaybackSession.Message = null;
            ProgressUpdate(this);
        }

        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (_state)
        {
            case YtEmbeddedPlayerState.Ended:
            {
                // setWatchedStatus(1);
                Console.WriteLine("Video finished!");

                if (Page != null)
                    await Page.GoNext(true);
                break;
            }
            case YtEmbeddedPlayerState.Unstarted:
            {
                ArgumentNullException.ThrowIfNull(_player);

                await target.InvokeVoidAsync("playVideo");
                await _player.InvokeVoidAsync("playVideo");
                break;
            }
        }
    }

    [JSInvokable]
    public void OnPlaybackRateChange(IJSObjectReference target, JsonElement data)
    {
        if (PlaybackSession == null)
            return;

        PlaybackSession.Rate = (float?) data.GetDouble();
        ProgressUpdate(this);
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

            try
            {
                PlaybackSession.Volume = await callJsOrNull<int?>(_player, "getVolume");
                PlaybackSession.Loaded = await callJsOrNull<float?>(_player, "getVideoLoadedFraction");

                // TODO error: Could not find 'getSubtitles' ('getSubtitles' was undefined).
                //PlaybackSession.Subtitles = await _player.InvokeAsync<string>("getSubtitles");

                var progress = await callJsOrNull<float?>(_player, "getCurrentTime");
                PlaybackSession.CurrentPosition = progress != null ? TimeSpan.FromSeconds(progress.Value) : null;

                // trying to seek before playback has actually started seems to do nothing.
                if (progress != null && progress > 0 && _lastRestoredPositionVideoId != Video.Id)
                {
                    _lastRestoredPositionVideoId = Video.Id;

                    var positionToRestore = await PlaybackSession.PlaybackPositionToRestore();
                    Console.WriteLine($"(session: {PlaybackSession.SessionId}) Restoring position: {positionToRestore}");

                    if (positionToRestore != null)
                        await _player.InvokeVoidAsync("seekTo", positionToRestore, true);
                }
            }
            catch (ObjectDisposedException) // seems sometimes timer runs after page left / refreshed, so clean up.
            {
                Timer?.Dispose();
            }
            catch (JSDisconnectedException)
            {
                Timer?.Dispose();
            }
            catch (TaskCanceledException)
            {
                Timer?.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine($"(session: {PlaybackSession.SessionId}) Exception reading data from YT player: " + e);
            }
        });
    }

    // for some reason changes to this method do not actually get picked up by hot-reload.
    private static async Task<T?> callJsOrNull<T>(IJSObjectReference? thing, string method)
    {
        if (thing == null) return default;

        try
        {
            return await thing.InvokeAsync<T>(method);
        }
        catch (JSException e)
        {
            // TODO maybe fixed in aspnetcore 10+ https://github.com/dotnet/aspnetcore/pull/60850
            if (e.Message.Contains("Null object cannot be converted to a value type"))
                return default;

            if (e.Message.Contains("The JSON value could not be converted"))
                return default;

            throw e;
        }
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

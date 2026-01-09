using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.PlaybackManager;

public sealed class PlaybackSession : IDisposable
{
    public readonly string SessionId = Guid.NewGuid().ToString();
    private readonly PlaybackSessionManager _manager;
    private IDbContextFactory<MediaFeederDataContext> DbContextFactory { get; }

    private Video? _video;
    private TimeSpan? _currentPosition;
    private AuthUser _user;
    private string? _quality;
    private Provider? _provider;
    private PlayerState? _state;
    private string? _message;
    private int? _volume;
    private float? _rate;
    private float? _loaded;
    private string? _subtitles;

    private bool _supportsRateChange;
    private bool _supportsVolumeChange;
    private bool _supportsSubtitles;

    public string? Title { get; set; }
    public event Action? UpdateEvent;
    public event Action? PlayPauseEvent;
    public event Action? PauseIfPlayingEvent;
    public event Action<Video, int?>? StartPlayingVideo;  // params: video, position to play from in seconds.
    public event Action<Int32>? SeekRelativeEvent;  // param: position to play from in seconds.
    public event Action? ToggleSubtitleEvent;
    public event Action<bool>? ChangeRateEvent;
    public event Action<bool>? ChangeVolumeEvent;
    public event Action? WatchEvent;
    public event Action? SkipEvent;
    public int? SelectedFolderId { get; set; }
    public event Action<Int32>? AddVideos;

    public void PlayPause() => PlayPauseEvent?.Invoke();
    public void SeekRelative(int seconds) => SeekRelativeEvent?.Invoke(seconds);

    public void ToggleSubtitles() => ToggleSubtitleEvent?.Invoke();
    public void ChangeRate(bool increase) => ChangeRateEvent?.Invoke(increase);
    public void ChangeVolume(bool increase) => ChangeVolumeEvent?.Invoke(increase);

    internal PlaybackSession(PlaybackSessionManager manager, AuthUser user, IDbContextFactory<MediaFeederDataContext> dbContextFactory)
    {
        _manager = manager;
        _user = user;
        User = user;
        DbContextFactory = dbContextFactory;
    }

    public void Dispose()
    {
        _manager.RemoveSession(this);
    }

    public List<Video> Playlist { get; } = [];

    public void AddToPlaylist(IEnumerable<Video> items)
    {
        Playlist.AddRange(items);
        UpdateEvent?.Invoke();
    }

    public bool AddToPlaylistIfNotPresent(Video video)
    {
        if (Playlist.Contains(video)) return false;
        Playlist.Add(video);
        UpdateEvent?.Invoke();
        return true;
    }

    public void RemoveFromPlaylist(Video item)
    {
        Playlist.Remove(item);
        UpdateEvent?.Invoke();
    }

    public Video? PopPlaylistHead()
    {
        if (Playlist.Count < 1)
            return null;

        var video = Playlist.First();
        Playlist.Remove(video);
        UpdateEvent?.Invoke();
        return video;
    }

    public void ClearPlaylist()
    {
        Playlist.Clear();
    }

    // TODO remove after migrating Video page.
    public async Task Watch()
    {
        if (WatchEvent != null)
        {
            WatchEvent.Invoke();
            return;
        }

        await MarkAsWatchedAndGoNext();
    }

    // TODO remove after migrating Video page.
    public async Task Skip()
    {
        if (SkipEvent != null)
        {
            SkipEvent.Invoke();
            return;
        }

        await PlayNextInPlaylist();
    }

    public async Task MarkAsWatchedAndGoNext()
    {
        var video = Video;  // capture for thread safety.
        if (video == null) throw new InvalidOperationException("Video not set in session.");
        await OnWatchedToEnd(video.Id);
    }

    public async Task OnWatchedToEnd(int videoId)
    {
        var video = Video;  // capture for thread safety.
        if (video == null) throw new InvalidOperationException("Video not set in session.");
        if (videoId != video.Id) throw new InvalidOperationException("Video ID does not match current video.");

        await using var db = await DbContextFactory.CreateDbContextAsync();
        db.Attach(video);
        video.MarkWatched(true);
        await db.SaveChangesAsync();

        // clear this to match what MarkWatched() does above, since this is what is sent when a video is replayed
        // by play/pause when the video is not already playing (or paused).
        CurrentPosition = null;

        await PlayNextInPlaylist();
    }

    public async Task PlayNextInPlaylist()
    {
        if (StartPlayingVideo == null) throw new InvalidOperationException("StartPlayingVideo not defined.");

        var nextVideo = PopPlaylistHead();
        if (nextVideo != null)
        {
            Video = nextVideo;
            var position = PlaybackPositionToRestore(nextVideo);
            StartPlayingVideo.Invoke(nextVideo, position);
        }
        else
        {
            PauseIfPlayingEvent?.Invoke();
        }
    }

    public string RateAdjustedDuration()
    {
        var rate = _rate;
        var video = _video;
        if (rate is null or 1.0f || video?.Duration == null) return "";
        var span = TimeSpan.FromSeconds((long)(video.Duration / rate));
        return $" ({span.ToString()})";
    }

    public Video? Video
    {
        get => _video;
        set
        {
            _video = value;
            UpdateEvent?.Invoke();
        }
    }

    public AuthUser User
    {
        get => _user;
        set
        {
            _user = value;
            UpdateEvent?.Invoke();
        }
    }

    public TimeSpan? CurrentPosition
    {
        get => _currentPosition;
        set
        {
            _currentPosition = value;
            UpdateEvent?.Invoke();
        }
    }

    public string? Quality
    {
        get => _quality;
        set
        {
            _quality = value;
            UpdateEvent?.Invoke();
        }
    }

    public Provider? Provider
    {
        get => _provider;
        set
        {
            _provider = value;
            UpdateEvent?.Invoke();
        }
    }

    public PlayerState? State
    {
        get => _state;
        set
        {
            _state = value;
            UpdateEvent?.Invoke();
        }
    }

    public string? Message
    {
        get => _message;
        set
        {
            _message = value;
            UpdateEvent?.Invoke();
        }
    }

    public int? Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            UpdateEvent?.Invoke();
        }
    }

    public float? Rate
    {
        get => _rate;
        set
        {
            _rate = value;
            UpdateEvent?.Invoke();
        }
    }

    public float? Loaded
    {
        get => _loaded;
        set
        {
            _loaded = value;
            UpdateEvent?.Invoke();
        }
    }

    public bool HasAddVideosHandlers() => AddVideos != null;

    internal void TriggerAddVideos(int minutes)
    {
        AddVideos?.Invoke(minutes);
    }

    public bool SupportsRateChange
    {
        get => _supportsRateChange;
        set
        {
            _supportsRateChange = value;
            UpdateEvent?.Invoke();
        }
    }

    public bool SupportsVolumeChange
    {
        get => _supportsVolumeChange;
        set
        {
            _supportsVolumeChange = value;
            UpdateEvent?.Invoke();
        }
    }

    public bool SupportsSubtitles
    {
        get => _supportsSubtitles;
        set
        {
            _supportsSubtitles = value;
            UpdateEvent?.Invoke();
        }
    }

    public string? Subtitles
    {
        get => _subtitles;
        set
        {
            _subtitles = value;
            UpdateEvent?.Invoke();
        }
    }

    public int? PlaybackPositionToRestore(Video? alt = default)
    {
        var v = alt ?? Video;
        if (v == null) return null;

        var position = v?.PlaybackPosition ?? 0;
        if (position > 0 && position < v.Duration - 10)
            return position;

        return null;
    }
}

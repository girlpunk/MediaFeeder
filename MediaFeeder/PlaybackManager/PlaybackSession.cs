using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.PlaybackManager;

public sealed class PlaybackSession : IDisposable
{
    private readonly PlaybackSessionManager _manager;
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
    public event Action<Int32>? SeekRelativeEvent;
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
    public void Watch() => WatchEvent?.Invoke();
    public void Skip() => SkipEvent?.Invoke();

    internal PlaybackSession(PlaybackSessionManager manager, AuthUser user)
    {
        _manager = manager;
        _user = user;
        User = user;
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
}

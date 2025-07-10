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
    private string? _state;
    private int? _volume;
    private float? _rate;
    private float? _loaded;
    public event Action? UpdateEvent;
    public event Action? PlayPauseEvent;
    public event Action? WatchEvent;
    public event Action? SkipEvent;
    public event Action<Int32>? AddVideos;

    public void PlayPause() => PlayPauseEvent?.Invoke();
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

    public string? State
    {
        get => _state;
        set
        {
            _state = value;
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

    public bool HasAddVideosHandlers()
    {
        return AddVideos != null;
    }
    public void TriggerAddVideos(int count)
    {
        AddVideos?.Invoke(count);
    }
}

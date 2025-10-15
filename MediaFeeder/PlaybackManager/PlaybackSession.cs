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
    public string? Title { get; set; }
    public event Action? UpdateEvent;
    public event Action? PlayPauseEvent;
    public event Action<Int32>? SeekRelativeEvent;
    public event Action? WatchEvent;
    public event Action? SkipEvent;
    public int? SelectedFolderId { get; set; }
    public event Action<Int32>? AddVideos;

    public void PlayPause() => PlayPauseEvent?.Invoke();
    public void SeekRelative(int seconds) => SeekRelativeEvent?.Invoke(seconds);
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

    public void AddToPlaylist(List<Video> items)
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
        Video video = Playlist.First();
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

    public bool HasAddVideosHandlers()
    {
        return AddVideos != null;
    }
    public void TriggerAddVideos(int minutes)
    {
        AddVideos?.Invoke(minutes);
    }
}

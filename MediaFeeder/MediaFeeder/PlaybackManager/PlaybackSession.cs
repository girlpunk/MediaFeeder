using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.PlaybackManager;

public class PlaybackSession : IDisposable
{
    private readonly PlaybackSessionManager _manager;
    private Video? _video;
    private TimeSpan? _currentPosition;
    private AuthUser _user;
    private string? _quality;
    private Provider? _provider;
    private string? _state;
    public event Action? UpdateEvent;

    internal PlaybackSession(PlaybackSessionManager manager, AuthUser user)
    {
        _manager = manager;
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
}
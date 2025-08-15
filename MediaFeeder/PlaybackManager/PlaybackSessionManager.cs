using MediaFeeder.Data.db;

namespace MediaFeeder.PlaybackManager;

public sealed class PlaybackSessionManager
{
    internal List<PlaybackSession> PlaybackSessions { get; } = new();

    internal PlaybackSession NewSession(AuthUser user)
    {
        var session = new PlaybackSession(this, user);
        PlaybackSessions.Add(session);

        UpdateEvent?.Invoke();

        return session;
    }

    internal void RemoveSession(PlaybackSession playbackSession)
    {
        PlaybackSessions.Remove(playbackSession);
        UpdateEvent?.Invoke();
    }

    public event Action? UpdateEvent;
}

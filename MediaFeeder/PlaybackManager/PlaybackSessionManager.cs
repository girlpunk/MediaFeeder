using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.PlaybackManager;

public sealed class PlaybackSessionManager(
    IDbContextFactory<MediaFeederDataContext> dbContextFactory
)
{
    internal Dictionary<string, PlaybackSession> PlaybackSessions { get; } = new();

    internal PlaybackSessionReference NewSession(string PlayerId, AuthUser user)
    {
        PlaybackSession session;
        if(!PlaybackSessions.TryGetValue(PlayerId, out session)) {
            session = new PlaybackSession(this, PlayerId, user, dbContextFactory);
            session.UpdateEvent += () => UpdateEvent?.Invoke();
            PlaybackSessions.Add(PlayerId, session);
        }

        UpdateEvent?.Invoke();

        return session.GetReference();
    }

    internal void RemoveSession(PlaybackSession playbackSession)
    {
        PlaybackSessions.Remove(playbackSession.PlayerId);
        UpdateEvent?.Invoke();
    }

    public event Action? UpdateEvent;
}

public sealed class PlaybackSessionReference(PlaybackSession session) : IDisposable
{
    internal PlaybackSession Session { get; } = session;

    public void Dispose()
    {
        Session.RemoveReference(this);
    }
}

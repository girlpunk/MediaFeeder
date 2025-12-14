using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.PlaybackManager;

public sealed class PlaybackSessionManager(IDbContextFactory<MediaFeederDataContext> dbContextFactory)
{
    private IDbContextFactory<MediaFeederDataContext> DbContextFactory { get; set; } = dbContextFactory;
    internal List<PlaybackSession> PlaybackSessions { get; } = new();

    internal PlaybackSession NewSession(AuthUser user)
    {
        var session = new PlaybackSession(this, user, DbContextFactory);
        session.UpdateEvent += () => UpdateEvent?.Invoke();
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

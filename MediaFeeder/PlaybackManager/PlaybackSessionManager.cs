using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.PlaybackManager;

public sealed class PlaybackSessionManager(
    IDbContextFactory<MediaFeederDataContext> dbContextFactory,
    ILogger logger
)
{
    internal Dictionary<string, PlaybackSession> PlaybackSessions { get; } = new();

    internal PlaybackSession.PlaybackSessionReference NewSession(string PlayerId, AuthUser user)
    {
        PlaybackSession session;
        if(!PlaybackSessions.TryGetValue(PlayerId, out session)) {
            logger.LogDebug("Creating new sesion for player {PlayerId}", PlayerId);
            session = new PlaybackSession(this, PlayerId, user, dbContextFactory, logger);
            session.UpdateEvent += () => UpdateEvent?.Invoke();
            PlaybackSessions.Add(PlayerId, session);
        } else {
            logger.LogDebug("Connecting existing session for player {PlayerId}", PlayerId);
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


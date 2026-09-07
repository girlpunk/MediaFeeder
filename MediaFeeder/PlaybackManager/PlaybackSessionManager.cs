namespace MediaFeeder.PlaybackManager;

using Data;
using Data.db;
using Microsoft.EntityFrameworkCore;

public sealed class PlaybackSessionManager(
    IDbContextFactory<MediaFeederDataContext> dbContextFactory,
    ILogger<PlaybackSession> logger
) : IDisposable
{
    internal Dictionary<string, PlaybackSession> PlaybackSessions { get; } = new(StringComparer.Ordinal);

    internal PlaybackSession.PlaybackSessionReference NewSession(string playerId, AuthUser user)
    {
        PlaybackSession session;
#pragma warning disable IDISP001
        if (!PlaybackSessions.TryGetValue(playerId, out session))
#pragma warning restore IDISP001
        {
            logger.LogDebug("Creating new sesion for player {PlayerId}", playerId);
#pragma warning disable IDISP001
            session = new PlaybackSession(this, playerId, user, dbContextFactory, logger);
#pragma warning restore IDISP001
            session.UpdateEvent += () => UpdateEvent?.Invoke();
            PlaybackSessions.Add(playerId, session);
        }
        else
        {
            logger.LogDebug("Connecting existing session for player {PlayerId}", playerId);
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

    public void Dispose()
    {
        foreach (var session in PlaybackSessions)
        {
            session.Value.Dispose();
        }
    }
}


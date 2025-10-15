namespace MediaFeeder.Providers.Youtube;

internal enum YtEmbeddedPlayerState
{
    Unstarted = -1,
    Ended = 0,
    Playing = 1,
    Paused = 2,
    Buffering = 3,
    Cued = 5
}

static class YtEmbeddedPlayerStateExtensions
{
    public static PlayerState ToPlayState(this YtEmbeddedPlayerState state)
    {
        return state switch
        {
            YtEmbeddedPlayerState.Unstarted => PlayerState.Idle,
            YtEmbeddedPlayerState.Ended => PlayerState.Idle,
            YtEmbeddedPlayerState.Playing => PlayerState.Playing,
            YtEmbeddedPlayerState.Paused => PlayerState.Paused,
            YtEmbeddedPlayerState.Buffering => PlayerState.Loading,
            YtEmbeddedPlayerState.Cued => PlayerState.Idle,
        };
    }
}
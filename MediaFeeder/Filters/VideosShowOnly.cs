namespace MediaFeeder.Filters;

[Flags]
public enum VideosShowOnly
{
    Watched = 1 << 0,
    NotWatched = 1 << 1,
    Downloaded = 1 << 2,
    NotDownloaded = 1 << 3,
    Stared = 1 << 4,
    NotStared = 1 << 5,
}

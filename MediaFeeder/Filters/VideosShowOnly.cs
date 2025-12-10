namespace MediaFeeder.Filters;

[Flags]
public enum VideosShowOnly
{
    Watched = 1 << 0,
    NotWatched = 1 << 1,
    Downloaded = 1 << 2,
    NotDownloaded = 1 << 3
}
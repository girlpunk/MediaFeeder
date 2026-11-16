namespace MediaFeeder.Data.DTOs;

[Serializable]
public record StatsResponse(
    int Published,
    int Watched,
    int Subscriptions,
    int VideosTracked,
    int VideosWatched,
    int VideosUnwatched,
    IReadOnlyDictionary<int, StatsFolder> Folders,
    string? Version
);

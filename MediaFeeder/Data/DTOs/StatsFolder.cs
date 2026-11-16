namespace MediaFeeder.Data.DTOs;

[Serializable]
public record StatsFolder(
    int Key,
    string Name,
    int Tracked,
    int UnwatchedCount,
    int? UnwatchedDuration
);

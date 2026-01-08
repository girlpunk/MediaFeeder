using System.Text.Json.Serialization;

namespace MediaFeeder.Providers.CCC;

[Serializable]
public record Event
{
    [JsonPropertyName("guid")] public string? Guid { get; init; }

    [JsonPropertyName("title")] public string? Title { get; init; }

    [JsonPropertyName("subtitle")] public string? Subtitle { get; init; }

    [JsonPropertyName("slug")] public string? Slug { get; init; }

    [JsonPropertyName("link")] public Uri? Link { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }

    [JsonPropertyName("original_language")]
    public string? OriginalLanguage { get; init; }

    [JsonPropertyName("persons")] public string[] Persons { get; init; } = [];

    [JsonPropertyName("tags")] public string[] Tags { get; init; } = [];

    [JsonPropertyName("view_count")] public int? ViewCount { get; init; }

    [JsonPropertyName("promoted")] public bool? Promoted { get; init; }

    [JsonPropertyName("date")] public DateTimeOffset? Date { get; init; }

    [JsonPropertyName("release_date")] public DateTimeOffset? ReleaseDate { get; init; }

    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("length")] public int? Length { get; init; }

    [JsonPropertyName("duration")] public int? Duration { get; init; }

    [JsonPropertyName("thumb_url")] public Uri? ThumbUrl { get; init; }

    [JsonPropertyName("poster_url")] public Uri? PosterUrl { get; init; }

    [JsonPropertyName("timeline_url")] public Uri? TimelineUrl { get; init; }

    [JsonPropertyName("thumbnails_url")] public Uri? ThumbnailsUrl { get; init; }

    [JsonPropertyName("frontend_link")] public Uri? FrontendLink { get; init; }

    [JsonPropertyName("url")] public Uri? Url { get; init; }

    [JsonPropertyName("conference_title")] public string? ConferenceTitle { get; init; }

    [JsonPropertyName("conference_url")] public Uri? ConferenceUrl { get; init; }

    [JsonPropertyName("related")] public object[] Related { get; init; } = [];

    [JsonPropertyName("recordings")] public Recording[] Recordings { get; init; } = [];
}
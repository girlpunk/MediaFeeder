using System.Text.Json.Serialization;

namespace MediaFeeder.Providers.CCC;

[Serializable]
public record Conference
{
    [JsonPropertyName("acronym")]
    public string? Acronym { get; init; }

    [JsonPropertyName("aspect_ratio")]
    public string? AspectRatio { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("schedule_url")]
    public string? ScheduleUrl { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("event_last_released_at")]
    public DateTimeOffset? EventLastReleasedAt { get; init; }

    [JsonPropertyName("link")]
    public Uri? Link { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("webgen_location")]
    public string? WebgenLocation { get; init; }

    [JsonPropertyName("logo_url")]
    public Uri? LogoUrl { get; init; }

    [JsonPropertyName("images_url")]
    public Uri? ImagesUrl { get; init; }

    [JsonPropertyName("recordings_url")]
    public Uri? RecordingsUrl { get; init; }

    [JsonPropertyName("url")]
    public Uri? Url { get; init; }

    [JsonPropertyName("events")]
    public Event[] Events { get; init; } = [];
}

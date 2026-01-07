using System.Text.Json.Serialization;

namespace MediaFeeder.Providers.CCC;

[Serializable]
public record Recording
{
    [JsonPropertyName("size")]
    public int? Size { get; init; }

    [JsonPropertyName("length")]
    public int? Length { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("high_quality")]
    public bool HighQuality { get; init; }

    [JsonPropertyName("width")]
    public int? Width { get; init; }

    [JsonPropertyName("height")]
    public int? Height { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("recording_url")]
    public Uri? RecordingUrl { get; init; }

    [JsonPropertyName("url")]
    public Uri? Url { get; init; }

    [JsonPropertyName("event_url")]
    public Uri? EventUrl { get; init; }

    [JsonPropertyName("conference_url")]
    public Uri? ConferenceUrl { get; init; }
}

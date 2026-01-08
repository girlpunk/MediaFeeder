using System.Text.Json.Serialization;

namespace MediaFeeder.Providers.CCC;

internal record EventList
{
    [JsonPropertyName("events")]
    public Event[] Events { get; init; } = [];
}

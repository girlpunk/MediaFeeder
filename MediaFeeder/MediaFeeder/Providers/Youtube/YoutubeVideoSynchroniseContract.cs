using System.Diagnostics;
using Paramore.Brighter;

namespace MediaFeeder.Providers.Youtube;

public record YoutubeVideoSynchroniseContract(int VideoId) : Command
{
    public Guid Id { get; set; }
    public Activity Span { get; set; }
}

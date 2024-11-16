using MediaFeeder.DTOs.Enums;

namespace MediaFeeder.Web.Providers.Youtube;

public class YoutubeProvider : IProvider
{
    public Type VideoFrameView => typeof(VideoFrame);

    public Provider Provider => Provider.YouTube;

    public string ProviderIdentifier => "Youtube";
}

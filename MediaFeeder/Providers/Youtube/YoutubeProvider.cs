using AntDesign;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.Providers.Youtube;

public class YoutubeProvider : IProvider
{
    public string Name => "YouTube";
    public string Icon => IconType.Outline.Youtube;

    public string GetUrl(Video video) => $"https://www.youtube.com/watch?v={video.VideoId}";

    public Task<bool> ProcessUrl(string url, Subscription subscription) => throw new NotImplementedException();

    public Task<bool> IsUrlValid(string url) => throw new NotImplementedException();

    public Provider Provider => Provider.YouTube;

    public Type VideoFrameView => typeof(YouTubeVideoFrame);

    public string ProviderIdentifier => "Youtube";

    public string? StreamMimeType { get; } = "Video/YouTube";
}

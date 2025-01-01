using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.Providers.Youtube;

public class YoutubeProvider : IProvider
{
    public Task DownloadVideo(Video video)
    {
        throw new NotImplementedException();
    }

    public Task SynchroniseSubscription(Subscription subscription)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ProcessUrl(string url, Subscription subscription)
    {
        throw new NotImplementedException();
    }

    public Task<bool> IsUrlValid(string url)
    {
        throw new NotImplementedException();
    }

    public Provider Provider => Provider.YouTube;

    public Type VideoFrameView => typeof(YouTubeVideoFrame);

    public string ProviderIdentifier => "Youtube";

    public string MimeType { get; } = "Video/YouTube";
}

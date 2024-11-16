using MediaFeeder.Models;
using MediaFeeder.Models.db;

namespace MediaFeeder.Providers.Youtube;

public class YoutubeProvider : IProvider
{
    public Task DownloadVideo(YtManagerAppVideo video) => throw new NotImplementedException();

    public Task SynchroniseSubscription(YtManagerAppSubscription subscription) => throw new NotImplementedException();

    public Task<bool> ProcessUrl(string url, YtManagerAppSubscription subscription) => throw new NotImplementedException();

    public Task<bool> IsUrlValid(string url) => throw new NotImplementedException();

    public Type VideoFrameView => typeof(VideoFrame);

    public string ProviderIdentifier => "Youtube";
}

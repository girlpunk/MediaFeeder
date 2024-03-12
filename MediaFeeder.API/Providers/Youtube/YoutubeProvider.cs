using MediaFeeder.API.Models.db;
using MediaFeeder.DTOs.Enums;

namespace MediaFeeder.API.Providers.Youtube;

public class YoutubeProvider : IProvider
{
    public Task DownloadVideo(YtManagerAppVideo video) => throw new NotImplementedException();

    public Task SynchroniseSubscription(YtManagerAppSubscription subscription) => throw new NotImplementedException();

    public Task<bool> ProcessUrl(string url, YtManagerAppSubscription subscription) => throw new NotImplementedException();

    public Task<bool> IsUrlValid(string url) => throw new NotImplementedException();

    public Provider Provider => Provider.YouTube;

    // public Type VideoFrameView => typeof(VideoFrame);

    public string ProviderIdentifier => "Youtube";
}

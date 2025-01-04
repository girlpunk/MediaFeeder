using AntDesign;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.Providers;

public class RSSProvider : IProvider
{
    public string Name => "RSS";
    public string Icon => IconType.Outline.Api;

    public string GetUrl(Video video) => video.VideoId;

    public Task DownloadVideo(Video video) => throw new NotImplementedException();

    public Task SynchroniseSubscription(Subscription subscription) => throw new NotImplementedException();

    public Task<bool> ProcessUrl(string url, Subscription subscription) => throw new NotImplementedException();

    public Task<bool> IsUrlValid(string url) => throw new NotImplementedException();

    public Provider Provider => Provider.RSS;

    public Type VideoFrameView => typeof(DownloadedVideoFrame);

    public string ProviderIdentifier => "RSS";

    public string? StreamMimeType { get; } = null;
}
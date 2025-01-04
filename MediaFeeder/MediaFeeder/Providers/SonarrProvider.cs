using AntDesign;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.Providers;

public class SonarrProvider : IProvider
{
    public Task DownloadVideo(Video video) => throw new NotImplementedException();

    public Task SynchroniseSubscription(Subscription subscription) => throw new NotImplementedException();

    public Task<bool> ProcessUrl(string url, Subscription subscription) => throw new NotImplementedException();

    public Task<bool> IsUrlValid(string url) => throw new NotImplementedException();

    public Provider Provider => Provider.Sonarr;

    public Type VideoFrameView => typeof(DownloadedVideoFrame);

    public string ProviderIdentifier => "Sonarr";

    public string? StreamMimeType { get; } = null;

    public string Name => "Sonarr TV";
    public string Icon => IconType.Outline.Monitor;
    public string GetUrl(Video video) => throw new NotImplementedException();
}

using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder;

public interface IProvider
{
    public Type VideoFrameView { get; }
    public Provider Provider { get; }
    public string ProviderIdentifier { get; }
    public Task DownloadVideo(YtManagerAppVideo video);
    public Task SynchroniseSubscription(YtManagerAppSubscription subscription);
    public Task<bool> ProcessUrl(string url, YtManagerAppSubscription subscription);
    public Task<bool> IsUrlValid(string url);
}
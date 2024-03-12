using MediaFeeder.API.Models.db;
using MediaFeeder.DTOs.Enums;

namespace MediaFeeder.API;

public interface IProvider
{
    public Task DownloadVideo(YtManagerAppVideo video);
    public Task SynchroniseSubscription(YtManagerAppSubscription subscription);
    public Task<bool> ProcessUrl(string url, YtManagerAppSubscription subscription);
    public Task<bool> IsUrlValid(string url);
    // public Type VideoFrameView { get; }
    public Provider Provider { get; }
    public string ProviderIdentifier { get; }
}

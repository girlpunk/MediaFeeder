using MediaFeeder.Models.db;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace MediaFeeder.Models;

public interface IProvider
{
    public Task DownloadVideo(YtManagerAppVideo video);
    public Task SynchroniseSubscription(YtManagerAppSubscription subscription);
    public Task<bool> ProcessUrl(string url, YtManagerAppSubscription subscription);
    public Task<bool> IsUrlValid(string url);
    public Type VideoFrameView { get; }
    public string ProviderIdentifier { get; }
}

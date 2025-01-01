using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder;

public interface IProvider
{
    public Type VideoFrameView { get; }
    public Provider Provider { get; }
    public string ProviderIdentifier { get; }
    public Task DownloadVideo(Video video);
    public Task SynchroniseSubscription(Subscription subscription);
    public Task<bool> ProcessUrl(string url, Subscription subscription);
    public Task<bool> IsUrlValid(string url);
    public string MimeType { get; }
}

using AntDesign;
using HtmlAgilityPack;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.Providers;

public class SonarrProvider : IProvider
{
    public Task DownloadVideo(Video video) => throw new NotImplementedException();

    public Task SynchroniseSubscription(Subscription subscription) => throw new NotImplementedException();

    public Task<bool> IsUrlValid(Uri url, HttpResponseMessage request, HtmlDocument? doc) => Task.FromResult(false);

    public Task CreateSubscription(Uri url, HttpResponseMessage request, HtmlDocument? doc, SubscriptionForm subscription) => throw new NotImplementedException();

    public Provider Provider => Provider.Sonarr;

    public Type VideoFrameView => typeof(DownloadedVideoFrame);

    public string ProviderIdentifier => "Sonarr";

    public string? StreamMimeType { get; } = null;

    public string Name => "Sonarr TV";
    public string Icon => IconType.Outline.Monitor;
    public string GetUrl(Video video) => throw new NotImplementedException();
}

namespace MediaFeeder;

using HtmlAgilityPack;
using Data;
using Data.db;
using Data.Enums;
using TickerQ.Utilities.Interfaces;

public interface IProvider
{
    public Type VideoFrameView { get; }
    public Provider Provider { get; }
    public string ProviderIdentifier { get; }
    public string? StreamMimeType { get; }
    string Name { get; }
    string Icon { get; }
    string? GetUrl(Video video);
    public Task<bool> IsUrlValid(Uri url, HttpResponseMessage request, HtmlDocument? doc);

    public Task CreateSubscription(
        Uri url,
        HttpResponseMessage request,
        HtmlDocument? doc,
        SubscriptionForm subscription
    );

    /// <summary>
    /// Must implement <see cref="ITickerFunction{SynchroniseSubscriptionContract}"/>
    /// </summary>
    public Type? SubscriptionSynchroniseType { get; }

    /// <summary>
    /// Must implement <see cref="ITickerFunction{DownloadVideoContract}"/>
    /// </summary>
    public Type? VideoDownloadType { get; }
}

using HtmlAgilityPack;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder;

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
    public Task CreateSubscription(Uri url, HttpResponseMessage request, HtmlDocument? doc, SubscriptionForm subscription);
}

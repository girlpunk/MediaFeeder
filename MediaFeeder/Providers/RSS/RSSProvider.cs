using AntDesign;
using HtmlAgilityPack;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.Providers.RSS;

public class RSSProvider : IProvider
{
    public string Name => "RSS";
    public string Icon => IconType.Outline.Api;

    public string GetUrl(Video video) => video.VideoId;

    public Task<bool> IsUrlValid(Uri url, HttpResponseMessage request, HtmlDocument? doc) =>
        Task.FromResult(request.Content.Headers.ContentType?.MediaType == "application/rss+xml");

    public Task CreateSubscription(Uri url, HttpResponseMessage request, HtmlDocument? doc, SubscriptionForm subscription)
    {
        ArgumentNullException.ThrowIfNull(doc);

        subscription.Name = doc.DocumentNode.SelectSingleNode("/rss/channel/title").InnerText;
        subscription.PlaylistId = url.ToString();
        //subscription.Description = doc.DocumentNode.SelectSingleNode("/rss/channel/description")?.InnerText;
        //subscription.Thumbnail = url + doc.DocumentNode.SelectSingleNode("/rss/channel/image/url")?.InnerText
        subscription.ChannelId = url.ToString();
        subscription.ChannelName = doc.DocumentNode.SelectSingleNode("/rss/channel/title").InnerText;
        subscription.Provider = ProviderIdentifier;

        return Task.CompletedTask;
    }

    public Provider Provider => Provider.RSS;

    public Type VideoFrameView => typeof(DownloadedVideoFrame);

    public string ProviderIdentifier => "RSS";

    public string? StreamMimeType { get; } = null;
}
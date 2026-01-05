using AntDesign;
using HtmlAgilityPack;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.Providers.Youtube;

public class YoutubeProvider : IProvider
{
    public string Name => "YouTube";
    public string Icon => IconType.Outline.Youtube;

    public string GetUrl(Video video) => $"https://www.youtube.com/watch?v={video.VideoId}";

    public async Task<bool> IsUrlValid(Uri url, HttpResponseMessage request, HtmlDocument? doc)
    {
        //1. Does it _look_ like a YouTube URL
        if ((url.Host != "kids.youtube.com" &&
             url.Host != "m.youtube.com" &&
             url.Host != "youtu.be" &&
             url.Host != "youtube-nocookie.com" &&
             url.Host != "youtube.com") || doc == null)
            return Task.FromResult(false);

        //2. Does it have the attributes we need
        var metaTag = doc.DocumentNode.SelectNodes("//meta[@itemprop='identifier']");
        if (metaTag.SingleOrDefault() == null || string.IsNullOrWhiteSpace(metaTag.SingleOrDefault()?.GetAttributeValue("content", "")))
            return Task.FromResult(false);

        var nameTag = doc.DocumentNode.SelectNodes("//meta[@itemprop='name']");
        if (nameTag.SingleOrDefault() == null ||
            string.IsNullOrWhiteSpace(nameTag.SingleOrDefault()?.GetAttributeValue("content", "")))
            return Task.FromResult(false);

        var channelId = metaTag.SingleOrDefault()?.GetAttributeValue("content", "") ?? "";

        if (channelId.StartsWith("UC", StringComparison.Ordinal))
            return Task.FromResult(true);

        return Task.FromResult(false);
    }

    public Task CreateSubscription(Uri url, HttpResponseMessage request, HtmlDocument? doc, SubscriptionForm subscription)
    {
        ArgumentNullException.ThrowIfNull(doc);

        //2. Does it have the attributes we need
        var metaTag = doc.DocumentNode.SelectNodes("//meta[@itemprop='identifier']");
        var nameTag = doc.DocumentNode.SelectNodes("//meta[@itemprop='name']");

        var channelId = metaTag.SingleOrDefault()?.GetAttributeValue("content", "") ?? "";
        var name = nameTag.SingleOrDefault()?.GetAttributeValue("content", "") ?? "";
        var playlistId = "UU" + channelId[2..];

        subscription.Name = name;
        subscription.ChannelId = channelId;
        subscription.PlaylistId = playlistId;
        subscription.Provider = ProviderIdentifier;
        return Task.CompletedTask;
    }

    public Provider Provider => Provider.YouTube;

    public Type VideoFrameView => typeof(YouTubeVideoFrame);

    public string ProviderIdentifier => "Youtube";

    public string? StreamMimeType { get; } = "Video/YouTube";
}

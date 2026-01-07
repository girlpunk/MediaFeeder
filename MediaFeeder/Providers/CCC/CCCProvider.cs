using AntDesign;
using HtmlAgilityPack;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.Providers.CCC;

public class CCCProvider(IHttpClientFactory httpClientFactory) : IProvider
{
    public string Name => "CCC Media";
    public string Icon => IconType.Outline.Laptop;

    public string GetUrl(Video video) => $"https://media.ccc.de/events/{video.VideoId}";

    public Task<bool> IsUrlValid(Uri url, HttpResponseMessage request, HtmlDocument? doc)
    {
        if (url.Host != "media.ccc.de" && url.Host != "api.media.ccc.de")
            return Task.FromResult(false);

        return Task.FromResult(url.AbsolutePath.StartsWith("/c/", StringComparison.Ordinal) ||
                               url.AbsolutePath == "/" ||
                               url.AbsolutePath.StartsWith("/public/conferences/", StringComparison.Ordinal) ||
                               url.AbsolutePath == "/public/events/");
    }

    public async Task CreateSubscription(Uri url, HttpResponseMessage request, HtmlDocument? doc, SubscriptionForm subscription)
    {
        if (url.AbsolutePath.StartsWith("/public/", StringComparison.Ordinal))
        {
            if (url.AbsolutePath.StartsWith("/public/conferences/", StringComparison.Ordinal))
                await CreateConferenceSubscription(request, subscription);
            else
                CreateAllSubscription(subscription);
        }
        else
        {
            //Load API version
            if (url.AbsolutePath.StartsWith("/c/", StringComparison.Ordinal))
            {
                var apiUrl = "https://api.media.ccc.de/public/conferences/" + url.AbsolutePath.Replace("/c/", "");
                using var client = httpClientFactory.CreateClient("retry");
                using var page = await client.GetAsync(apiUrl);
                page.EnsureSuccessStatusCode();
                await CreateConferenceSubscription(request, subscription);
            }
            else
            {
                CreateAllSubscription(subscription);
            }
        }
    }

    private void CreateAllSubscription(SubscriptionForm subscription)
    {
        subscription.Name = "CCC Media - All Videos - HD English";
        subscription.PlaylistId = "https://api.media.ccc.de/public/events/";
        //subscription.Description = doc.DocumentNode.SelectSingleNode("/rss/channel/description")?.InnerText;
        //subscription.Thumbnail = "https://media.ccc.de/assets/frontend/voctocat-header-b587ba587ba768c4a96ed33ee72747b9a5432b954892e25ed9f850a99c7d161c.svg";
        subscription.ChannelId = "https://api.media.ccc.de/public/events/";
        subscription.ChannelName = "CCC Media - All Videos - HD English";
        subscription.Provider = ProviderIdentifier;
    }

    private async Task CreateConferenceSubscription(HttpResponseMessage request, SubscriptionForm subscription)
    {
        var conference = await request.Content.ReadFromJsonAsync<Conference>();
        ArgumentNullException.ThrowIfNull(conference);

        subscription.Name = conference.Title ?? "";
        subscription.PlaylistId = request.RequestMessage?.RequestUri?.ToString() ?? throw new InvalidOperationException("No URL found");
        //subscription.Description = doc.DocumentNode.SelectSingleNode("/rss/channel/description")?.InnerText;
        //subscription.Thumbnail = url + doc.DocumentNode.SelectSingleNode("/rss/channel/image/url")?.InnerText
        subscription.ChannelId = request.RequestMessage?.RequestUri?.ToString() ?? throw new InvalidOperationException("No URL found");
        subscription.ChannelName = conference.Title ?? "";
        subscription.Provider = ProviderIdentifier;
    }

    public Provider Provider => Provider.CCC;

    public Type VideoFrameView => typeof(DownloadedVideoFrame);

    public string ProviderIdentifier => "CCC";

    public string? StreamMimeType { get; } = null;
}
using MassTransit;
using MediaFeeder.Data;
using MediaFeeder.Tasks;
using Microsoft.EntityFrameworkCore;
using MediaFeeder.Data.db;

namespace MediaFeeder.Providers.CCC;

public class CCCSubscriptionSynchroniseConsumer(
    ILogger<CCCSubscriptionSynchroniseConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    IHttpClientFactory httpClientFactory
) : IConsumer<SynchroniseSubscriptionContract<CCCProvider>>
{
    public async Task Consume(ConsumeContext<SynchroniseSubscriptionContract<CCCProvider>> context)
    {
        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var subscription = await db.Subscriptions.SingleAsync(s => s.Id == context.Message.SubscriptionId, context.CancellationToken);

        if (subscription.LastSynchronised > DateTimeOffset.Now - TimeSpan.FromHours(1))
        {
            logger.LogInformation("Subscription {Subscription} was already synchronised {Time} ago, skipping", subscription.Name, DateTimeOffset.Now - subscription.LastSynchronised);
            return;
        }

        logger.LogInformation("Starting synchronize {}", subscription.Name);

        foreach (var video in await db.Videos
                     //TODO: Does <= need to be reversed?
                     .Where(v => v.SubscriptionId == subscription.Id && v.New && DateTimeOffset.UtcNow - v.PublishDate <= TimeSpan.FromDays(1))
                     .ToListAsync(context.CancellationToken)
                )
            video.New = false;

        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Starting check new videos {}", subscription.Name);

        var startTime = DateTimeOffset.UtcNow;
        var page = 0;
        var continueDownloading = true;
        using var client = httpClientFactory.CreateClient("retry");

        while (continueDownloading)
        {
            continueDownloading = await DownloadPage(page, subscription, client, context.CancellationToken);
            page++;
        }

        subscription.LastSynchronised = startTime;

        await db.SaveChangesAsync(context.CancellationToken);
    }

    private async Task<bool> DownloadPage(int pageNumber, Subscription subscription, HttpClient client,
        CancellationToken cancellationToken = default)
    {
        var url = new UriBuilder(new Uri(subscription.ChannelId))
        {
            Query = $"page={pageNumber}"
        };
        using var pageResponse = await client.GetAsync(url.ToString(), cancellationToken);
        pageResponse.EnsureSuccessStatusCode();

        if (subscription.ChannelId.Contains("/conferences/"))
        {
            var page = await pageResponse.Content.ReadFromJsonAsync<Conference>(cancellationToken);
            ArgumentNullException.ThrowIfNull(page);
            await ProcessConferencePage(page, subscription, client, cancellationToken);
            return false;
        }
        else
        {
            var page = await pageResponse.Content.ReadFromJsonAsync<EventList>(cancellationToken);
            ArgumentNullException.ThrowIfNull(page);
            return await ProcessEventsPage(page.Events, subscription, client, cancellationToken);
        }
    }

    private async Task ProcessConferencePage(Conference conference, Subscription subscription, HttpClient client,
        CancellationToken cancellationToken)
    {
        if (subscription.Name == subscription.ChannelName)
            subscription.Name = conference.Title ?? subscription.Name;
        subscription.ChannelName = conference.Title ?? subscription.ChannelName;

        // Need to make absolute
        subscription.Thumb = conference.LogoUrl?.ToString();
        subscription.Thumbnail = conference.LogoUrl?.ToString();

        foreach (var conferenceEvent in conference.Events.Where(conferenceEvent => conferenceEvent.UpdatedAt >= subscription.LastSynchronised))
            await SyncVideo(conferenceEvent, subscription, client, cancellationToken);
    }

    private async Task<bool> ProcessEventsPage(IEnumerable<Event> events, Subscription subscription, HttpClient client,
        CancellationToken cancellationToken)
    {
        subscription.Thumbnail = "https://media.ccc.de/assets/frontend/voctocat-header-b587ba587ba768c4a96ed33ee72747b9a5432b954892e25ed9f850a99c7d161c.svg";

        var foundUpdated = false;

        foreach (var conferenceEvent in events)
            if (conferenceEvent.UpdatedAt >= subscription.LastSynchronised)
                await SyncVideo(conferenceEvent, subscription, client, cancellationToken);
            else
                foundUpdated = true;

        return foundUpdated;
    }

    private async Task SyncVideo(Event item, Subscription subscription, HttpClient client, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var video = await db.Videos
            .Include(static v => v.Tags)
            .SingleOrDefaultAsync(v => v.VideoId == item.Guid.ToString() && v.SubscriptionId == subscription.Id, cancellationToken);

        if (video == null)
        {
            video = new Video
            {
                VideoId = item.Guid.ToString() ?? throw new InvalidOperationException("No GUID found"),
                Name = $"{item.Title} - {item.Subtitle} ({item.ConferenceTitle})",
                Description = item.Description ?? "",
                SubscriptionId = subscription.Id,
                UploaderName = subscription.Name,
            };

            db.Videos.Add(video);
        }

        video.Name = $"{item.Title} - {item.Subtitle} ({item.ConferenceTitle})";
        video.New = DateTimeOffset.UtcNow - item.Date <= TimeSpan.FromDays(7);
        video.PublishDate = item.Date;
        video.Thumb = item.PosterUrl?.ToString();
        video.Description = item.Description ?? "";
        video.Duration = item.Duration;
        video.UploaderName = string.Join(", ", item.Persons);
        video.Rating = (item.Promoted ?? false) ? 5 : 1;
        video.Views = item.ViewCount;

        var existingTags = video.Tags.Select(static t => t.Tag);
        var keywords = (item.Tags)
            .SelectMany(static k => k.Split(','))
            .Select(static k => k.Trim())
            .Where(k => !existingTags.Contains(k))
            .Select(k => new VideoTag()
            {
                Video = video,
                Tag = k,
            });
        foreach (var videoTag in keywords)
            video.Tags.Add(videoTag);

        await db.SaveChangesAsync(cancellationToken);

        var recordings = (await client.GetFromJsonAsync<Event>(item.Url, cancellationToken))?.Recordings;
        ArgumentNullException.ThrowIfNull(recordings);

        video.DownloadedPath =
            recordings
                .Where(static r => r is { Language: "eng", MimeType: "video/mp4" }) // First pass; best quality MP4 with English audio
                .MaxBy(static r => r.Width * r.Height)?.RecordingUrl?.ToString() ??
            recordings
                .Where(static r => r.MimeType == "video/mp4") // First pass; best quality MP4
                .MaxBy(static r => r.Width * r.Height)?.RecordingUrl?.ToString() ??
            recordings
                .Where(static r => r is { Language: "eng", MimeType: "video/mp4" }) // First pass; best quality with English audio
                .MaxBy(static r => r.Width * r.Height)?.RecordingUrl?.ToString() ??
            recordings
                .Where(static r => r is { Language: "eng", MimeType: "video/mp4" }) // First pass; best quality
                .MaxBy(static r => r.Width * r.Height)?.RecordingUrl?.ToString();

        await db.SaveChangesAsync(cancellationToken);
    }
}

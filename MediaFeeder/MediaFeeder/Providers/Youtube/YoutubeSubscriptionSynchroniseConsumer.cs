using System.Xml.Linq;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using JetBrains.Annotations;
using MassTransit;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Tasks;
using Microsoft.EntityFrameworkCore;
using Subscription = MediaFeeder.Data.db.Subscription;
using Video = MediaFeeder.Data.db.Video;

namespace MediaFeeder.Providers.Youtube;

[UsedImplicitly]
public sealed class YoutubeSubscriptionSynchroniseConsumer(
    ILogger<YoutubeSubscriptionSynchroniseConsumer> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    IHttpClientFactory httpClientFactory,
    IBus bus,
    Utils utils,
    YouTubeService youTubeService
) : IConsumer<SynchroniseSubscriptionContract<YoutubeProvider>>
{
    public async Task Consume(ConsumeContext<SynchroniseSubscriptionContract<YoutubeProvider>> context)
    {
        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var subscription =
            await db.Subscriptions.SingleAsync(s => s.Id == context.Message.SubscriptionId, context.CancellationToken);
        logger.LogInformation("Starting synchronize {}", subscription.Name);

        foreach (var video in await db.Videos
                     .Where(v => v.SubscriptionId == subscription.Id && v.New && DateTimeOffset.UtcNow - v.PublishDate <= TimeSpan.FromDays(1))
                     .ToListAsync(context.CancellationToken)
                )
            video.New = false;

        await db.SaveChangesAsync(context.CancellationToken);

        foreach (var video in db.Videos.Where(v =>
                     v.SubscriptionId == subscription.Id && (
                         (!string.IsNullOrWhiteSpace(v.DownloadedPath)) ||
                         (v.Duration == 0 || v.Duration == null) ||
                         string.IsNullOrWhiteSpace(v.Thumb)
                     )
                 ))
            await bus.Publish(new YoutubeActualVideoSynchroniseContract(video.Id), context.CancellationToken);

        logger.LogInformation("Starting check new videos {}", subscription.Name);

        if (subscription.LastSynchronised == null)
        {
            logger.LogInformation("New subscription, forcing full sync for {}", subscription.Name);
            await CheckAllVideos(subscription, db, context.CancellationToken);
        }
        else
        {
            if (subscription.LastSynchronised.Value.DayOfYear != DateTimeOffset.Now.DayOfYear)
            {
                var channelRequest = youTubeService.Channels.List("snippet");
                channelRequest.Id = subscription.ChannelId;
                var channelResponse = await channelRequest.ExecuteAsync(context.CancellationToken);
                var channelResult = channelResponse?.Items?.SingleOrDefault();

                if (channelResult == null)
                    logger.LogError("Could not load channel details for {} (channel {})", subscription.Id,
                        subscription.ChannelId);

                if (channelResult?.Snippet?.Title != null)
                {
                    if (subscription.Name == subscription.ChannelName)
                        subscription.Name = channelResult.Snippet.Title;

                    subscription.ChannelName = channelResult.Snippet.Title;
                }

                if (channelResult?.Snippet?.Thumbnails != null)
                    subscription.Thumb = await utils.LoadResourceThumbnail(
                        subscription.PlaylistId,
                        "sub",
                        channelResult.Snippet.Thumbnails,
                        logger,
                        context.CancellationToken);

                if (channelResult?.Snippet?.Description != null)
                    subscription.Description = channelResult.Snippet.Description;
            }

            try
            {
                await CheckRssVideos(subscription, db, context.CancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while running RSS Sync, running full sync ({})", subscription.Id);
                await CheckAllVideos(subscription, db, context.CancellationToken);
            }
        }

        subscription.LastSynchronised = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(context.CancellationToken);

        // enabled = first_non_null(channel.auto_download, channel.user.preferences['auto_download'])
        //
        // if enabled:
        //     global_limit = channel.user.preferences['download_global_limit']
        //     limit = first_non_null(channel.download_limit, channel.user.preferences['download_subscription_limit'])
        //     order = first_non_null(channel.download_order, channel.user.preferences['download_order'])
        //     order = VIDEO_ORDER_MAPPING[order]
        //
        //     videos_to_download = Video.objects \
        //         .filter(subscription=channel, downloaded_path__isnull=True, watched=False, subscription__auto_download=True) \
        //         .order_by(order)
        //
        //     if global_limit > 0:
        //         global_downloaded = Video.objects.filter(subscription__user=channel.user, downloaded_path__isnull=False).count()
        //         allowed_count = max(global_limit - global_downloaded, 0)
        //         videos_to_download = videos_to_download[0:allowed_count]
        //
        //     if limit > 0:
        //         sub_downloaded = Video.objects.filter(subscription=channel, downloaded_path__isnull=False).count()
        //         allowed_count = max(limit - sub_downloaded, 0)
        //         videos_to_download = videos_to_download[0:allowed_count]
        //
        //     # enqueue download
        //     for video in videos_to_download:
        //         download_video.delay(video.pk)
    }

    // @shared_task()
    // def download_video(video_pk: int, attempt: int = 1):
    //     # Issue: if multiple videos are downloaded at the same time, a race condition appears in the mkdirs() call that
    //     # youtube-dl makes, which causes it to fail with the error 'Cannot create folder - file already exists'.
    //     # For now, allow a single download instance.
    //     video = Video.objects.get(pk=video_pk, subscription__provider="Youtube")
    //     __lock.acquire()
    //
    //     try:
    //         user = video.subscription.user
    //         max_attempts = user.preferences['max_download_attempts']
    //
    //         youtube_dl_params, output_path = utils.build_youtube_dl_params(video)
    //         with youtube_dl.YoutubeDL(youtube_dl_params) as yt:
    //             ret = yt.download(["https://www.youtube.com/watch?v=" + video.video_id])
    //
    //         __log.info('Download finished with code %d', ret)
    //
    //         if ret == 0:
    //             video.downloaded_path = output_path
    //             video.save()
    //             __log.info('Video %d [%s %s] downloaded successfully!', video.id, video.video_id, video.name)
    //
    //         elif attempt <= max_attempts:
    //             __log.warning('Re-enqueueing video (attempt %d/%d)', attempt, max_attempts)
    //             download_video.delay(video, attempt + 1)
    //
    //         else:
    //             __log.error('Multiple attempts to download video %d [%s %s] failed!', video.id, video.video_id, video.name)
    //             video.downloaded_path = ''
    //             video.save()
    //
    //     finally:
    //         __lock.release()

    // @shared_task()
    // def delete_video(video_pk: int):
    //     video = Video.objects.get(pk=video_pk, subscription__provider="Youtube")
    //     count = 0
    //
    //     try:
    //         for file in video.get_files():
    //             __log.info("Deleting file %s", file)
    //             count += 1
    //             try:
    //                 os.unlink(file)
    //             except OSError as e:
    //                 __log.error("Failed to delete file %s: Error: %s", file, e)
    //
    //     except OSError as e:
    //         __log.error("Failed to delete video %d [%s %s]. Error: %s",
    //                     video.id,
    //                     video.video_id,
    //                     video.name,
    //                     e)
    //
    //     video.downloaded_path = None
    //     video.save()
    //
    //     __log.info('Deleted video %d successfully! (%d files) [%s %s]',
    //                video.id,
    //                count,
    //                video.video_id,
    //                video.name)

    private static readonly XNamespace AtomNamespace = XNamespace.Get("http://www.w3.org/2005/Atom");
    private static readonly XNamespace YouTubeNamespace = XNamespace.Get("http://www.youtube.com/xml/schemas/2015");
    private static readonly XNamespace YahooNamespace = XNamespace.Get("http://search.yahoo.com/mrss/");

    private async Task CheckRssVideos(Subscription subscription, MediaFeederDataContext db, CancellationToken cancellationToken)
    {
        var foundExistingVideo = false;

        using var httpClient = httpClientFactory.CreateClient("retry");

        using var rssRequest =
            await httpClient.GetAsync("https://www.youtube.com/feeds/videos.xml?channel_id=" + subscription.ChannelId, cancellationToken);
        rssRequest.EnsureSuccessStatusCode();

        var rss = await XDocument.LoadAsync(await rssRequest.Content.ReadAsStreamAsync(cancellationToken), LoadOptions.None, cancellationToken);

        foreach (var entry in rss.Root?.Elements(AtomNamespace + "entry") ?? [])
        {
            var videoId = entry.Element(YouTubeNamespace + "videoId")?.Value;
            var existing =
                await db.Videos.SingleOrDefaultAsync(v => v.VideoId == videoId && v.SubscriptionId == subscription.Id, cancellationToken);

            if (existing != null || videoId == null)
            {
                foundExistingVideo = true;
            }
            else
            {
                var thumbnailUrl = entry.Element(YahooNamespace + "group")?
                    .Element(YahooNamespace + "thumbnail")?
                    .Attribute("url")?.Value;

                var thumbnailPath = "";

                if (thumbnailUrl != null)
                    thumbnailPath = await utils.LoadUrlThumbnail(videoId, "video", thumbnailUrl, logger, cancellationToken);

                var video = new Video
                {
                    VideoId = videoId,
                    Name = entry.Element(AtomNamespace + "title")?.Value ?? "",
                    Description =
                        entry.Element(YahooNamespace + "group")?.Element(YahooNamespace + "description")?.Value ?? "",
                    Watched = false,
                    New = true,
                    DownloadedPath = null,
                    SubscriptionId = subscription.Id,
                    PlaylistIndex = 0,
                    PublishDate = entry.Element(AtomNamespace + "published")?.Value != null ? DateTime.Parse(entry.Element(AtomNamespace + "published")!.Value) : null,
                    Views = entry
                        .Element(YahooNamespace + "group")?
                        .Element(YahooNamespace + "community")?
                        .Element(YahooNamespace + "statistics")?
                        .Attribute("views")?.Value != null
                        ? int.Parse(entry
                            .Element(YahooNamespace + "group")!
                            .Element(YahooNamespace + "community")!
                            .Element(YahooNamespace + "statistics")!
                            .Attribute("views")!.Value)
                        : null,
                    Thumb = thumbnailPath,
                    Thumbnail = thumbnailPath,
                    UploaderName = entry.Element(AtomNamespace + "author")?.Element(AtomNamespace + "name")?.Value ?? subscription.Name
                };

                db.Videos.Add(video);
                await db.SaveChangesAsync(cancellationToken);

                await bus.Publish(new YoutubeActualVideoSynchroniseContract(video.Id), cancellationToken);
            }
        }

        if (!foundExistingVideo)
            await CheckAllVideos(subscription, db, cancellationToken);
    }

    private async Task CheckAllVideos(Subscription subscription, MediaFeederDataContext db, CancellationToken cancellationToken)
    {
        var nextPageToken = "";
        while (nextPageToken != null)
        {
            var playlistRequest = youTubeService.PlaylistItems.List("snippet");
            playlistRequest.PlaylistId = subscription.PlaylistId;
            playlistRequest.MaxResults = 50;
            playlistRequest.PageToken = nextPageToken;

            PlaylistItemListResponse? playlistResponse;
            // var playlistResponse = await playlistRequest.ExecuteAsync(cancellationToken);
            using (var request = playlistRequest.CreateRequest())
            {
                logger.LogInformation("Got request URL: {}", request.RequestUri);
                using (var response = await youTubeService.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    logger.LogInformation("Got response: {}", await response.Content.ReadAsStringAsync(cancellationToken));
                    response.EnsureSuccessStatusCode();

                    playlistResponse = await youTubeService.DeserializeResponse<PlaylistItemListResponse>(response).ConfigureAwait(false);
                }
            }

            var playlistItems = playlistResponse.Items ?? [];

            playlistItems = subscription.RewritePlaylistIndices
                ? playlistItems.OrderBy(static i => i.Snippet.PublishedAtDateTimeOffset).ToList()
                : playlistItems.OrderBy(static i => i.Snippet.Position).ToList();

            logger.LogInformation("Got page of {} videos for {}, next page will be {}", playlistItems.Count, subscription.Name, playlistResponse.NextPageToken);

            foreach (var item in playlistItems)
                await AddVideoFromApi(subscription, db, cancellationToken, item);

            nextPageToken = playlistResponse.NextPageToken;
        }
    }

    private async Task AddVideoFromApi(Subscription subscription, MediaFeederDataContext db,
        CancellationToken cancellationToken, PlaylistItem item)
    {
        var results = await db.Videos.SingleOrDefaultAsync(v =>
            v.VideoId == item.Snippet.ResourceId.VideoId && v.SubscriptionId == subscription.Id, cancellationToken);

        if (results != null)
            return;

        // fix playlist index if necessary
        if (subscription.RewritePlaylistIndices || await db.Videos.AnyAsync(v => v.SubscriptionId == subscription.Id && v.PlaylistIndex == item.Snippet.Position, cancellationToken))
        {
            var highest = (await db.Videos
                    .Where(v => v.SubscriptionId == subscription.Id)
                    .OrderByDescending(static v => v.PlaylistIndex)
                    .FirstOrDefaultAsync(cancellationToken: cancellationToken))
                ?.PlaylistIndex;
            item.Snippet.Position = 1 + (highest ?? -1);
        }

        var thumbnailPath = await utils.LoadResourceThumbnail(item.Snippet.ResourceId.VideoId, "video",
            item.Snippet.Thumbnails, logger, cancellationToken);

        var video = new Video
        {
            VideoId = item.Snippet.ResourceId.VideoId,
            Name = item.Snippet.Title,
            Description = item.Snippet.Description,
            Watched = false,
            New = true,
            DownloadedPath = null,
            SubscriptionId = subscription.Id,
            PlaylistIndex = (int)(item.Snippet.Position ?? 0),
            PublishDate = item.Snippet.PublishedAtDateTimeOffset,
            Thumb = thumbnailPath,
            Thumbnail = thumbnailPath,
            UploaderName = item.Snippet.VideoOwnerChannelTitle
        };

        db.Videos.Add(video);
        await db.SaveChangesAsync(cancellationToken);

        await bus.Publish(new YoutubeActualVideoSynchroniseContract(video.Id), cancellationToken);
    }
}

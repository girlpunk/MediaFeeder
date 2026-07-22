using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Helpers;

public static class ShuffleHelper
{
    public static async Task<List<Video>> Shuffle(
        MediaFeederDataContext dataContext,
        AuthUser user,
        int? durationMinutes,
        int? folderId,
        int? subscriptionId,
        List<Video>? exclude = null,
        CancellationToken cancellationToken = default
    )
    {
        var excludeOrEmpty = exclude ?? [];
        List<Subscription> subscriptions;

        if (folderId != null)
        {
            var subfolderIds = await Folder.RecursiveFolderIds(
                dataContext,
                folderId.Value,
                user.Id
            );
            subscriptions = await dataContext
                .Subscriptions.Where(s =>
                    subfolderIds.Contains(s.ParentFolderId) && s.UserId == user.Id
                )
                .OrderBy(static _ => EF.Functions.Random())
                .ToListAsync(cancellationToken);
        }
        else if (subscriptionId != null)
        {
            subscriptions =
            [
                await dataContext.Subscriptions.SingleAsync(
                    s => s.Id == subscriptionId && s.UserId == user.Id,
                    cancellationToken
                ),
            ];
        }
        else
        {
            subscriptions = await dataContext
                .Subscriptions.Where(s => s.UserId == user.Id)
                .OrderBy(static _ => EF.Functions.Random())
                .ToListAsync(cancellationToken);
        }

        List<Video> videos = [];
        var timeRemaining = TimeSpan.FromMinutes(durationMinutes ?? 60);

        timeRemaining = await FindVideos(
            dataContext,
            subscriptions,
            false,
            excludeOrEmpty,
            timeRemaining,
            q => q.Where(v => v.PlaybackPosition > 0),
            videos,
            cancellationToken
        );

        timeRemaining = await FindVideos(
            dataContext,
            subscriptions,
            false,
            excludeOrEmpty,
            timeRemaining,
            q => q.Where(v => v.Star),
            videos,
            cancellationToken
        );

        timeRemaining = await FindFirstVideo(
            dataContext,
            subscriptions,
            excludeOrEmpty,
            timeRemaining,
            videos,
            cancellationToken
        );

        // ReSharper disable once RedundantAssignment
        timeRemaining = await FindVideos(
            dataContext,
            subscriptions,
            true,
            excludeOrEmpty,
            timeRemaining,
            null,
            videos,
            cancellationToken
        );

        return videos;
    }

    private static async Task<TimeSpan> FindFirstVideo(
        MediaFeederDataContext dataContext,
        List<Subscription> subscriptions,
        List<Video> excludeOrEmpty,
        TimeSpan timeRemaining,
        List<Video> reply,
        CancellationToken cancellationToken
    )
    {
        var query = dataContext.Videos.Where(v =>
            v.Watched == false
            && v.Duration != null
            && subscriptions.Select(static s => s.Id).Contains(v.SubscriptionId)
            && !excludeOrEmpty.Contains(v)
        );

        if (timeRemaining.TotalMinutes < 60)
        {
            var timeRemainingTotalSeconds = timeRemaining.TotalSeconds;
            query = query.Where(v => v.Duration <= timeRemainingTotalSeconds);
        }

        (_, timeRemaining) = await MaybeAddVideo(query, timeRemaining, reply, cancellationToken);
        return timeRemaining;
    }

    private static async Task<TimeSpan> FindVideos(
        MediaFeederDataContext dataContext,
        List<Subscription> subscriptions,
        bool forEachSubscription,
        List<Video> excludeOrEmpty,
        TimeSpan timeRemaining,
        Func<IQueryable<Video>, IQueryable<Video>>? modifier,
        List<Video> reply,
        CancellationToken cancellationToken
    )
    {
        var baseQuery = dataContext.Videos.Where(v =>
            v.Watched == false
            && v.Duration != null
            && !reply.Contains(v)
            && !excludeOrEmpty.Contains(v)
        );

        if (modifier != null)
            baseQuery = modifier(baseQuery);

        bool loopAgain;
        do
        {
            loopAgain = false;

            if (forEachSubscription)
            {
                foreach (var subscription in subscriptions)
                {
                    var query = baseQuery.Where(v => v.SubscriptionId == subscription.Id);
                    (var added, timeRemaining) = await MaybeAddVideo(
                        query,
                        timeRemaining,
                        reply,
                        cancellationToken
                    );
                    if (added)
                        loopAgain = true;
                }
            }
            else
            {
                var query = baseQuery.Where(v =>
                    subscriptions.Select(static s => s.Id).Contains(v.SubscriptionId)
                );
                (loopAgain, timeRemaining) = await MaybeAddVideo(
                    query,
                    timeRemaining,
                    reply,
                    cancellationToken
                );
            }
        } while (loopAgain);

        return timeRemaining;
    }

    private static async Task<(bool added, TimeSpan timeRemaining)> MaybeAddVideo(
        IQueryable<Video> query,
        TimeSpan timeRemaining,
        List<Video> reply,
        CancellationToken cancellationToken
    )
    {
        var video = await query
            .Include(static v => v.Subscription)
            .OrderBy(static v => v.PublishDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (video == null || video.DurationSpan > timeRemaining)
            return (false, timeRemaining);

        reply.Add(video);
        timeRemaining -= video.DurationSpan ?? TimeSpan.Zero;
        return (true, timeRemaining);
    }
}

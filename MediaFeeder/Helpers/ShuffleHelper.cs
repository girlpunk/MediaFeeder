using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Helpers;

public class ShuffleHelper
{

    public static async Task<List<Video>> Shuffle(
        MediaFeederDataContext dataContext,
        AuthUser user,
        int? durationMinutes,
        int? folderId,
        int? subscriptionId,
        List<Video>? exclude = null,
        CancellationToken cancellationToken = default)
    {
        var timeRemaining = TimeSpan.FromMinutes(durationMinutes ?? 60);
        var excludeOrEmpty = exclude ?? [];
        List<Video> reply = [];
        List<Subscription> subscriptions;

        if (folderId != null)
        {
            var subfolderIds = await Folder.RecursiveFolderIds(dataContext, folderId.Value, user.Id);
            subscriptions = await dataContext.Subscriptions
                .Where(s => subfolderIds.Contains(s.ParentFolderId) && s.UserId == user.Id)
                .OrderBy(static _ => EF.Functions.Random())
                .ToListAsync(cancellationToken);
        }
        else if (subscriptionId != null)
        {
            subscriptions = [await dataContext.Subscriptions.SingleAsync(s => s.Id == subscriptionId && s.UserId == user.Id, cancellationToken)];
        }
        else
        {
            subscriptions = await dataContext.Subscriptions
                .Where(s => s.UserId == user.Id)
                .OrderBy(static _ => EF.Functions.Random())
                .ToListAsync(cancellationToken);
        }

        var query = dataContext.Videos
            .Where(v => v.Watched == false
                        && subscriptions.Select(static s => s.Id).Contains(v.SubscriptionId)
                        && !excludeOrEmpty.Contains(v));

        if (timeRemaining.TotalMinutes < 60)
            query = query.Where(v => v.Duration <= timeRemaining.TotalSeconds);

        var first = await query
            .OrderBy(static v => v.PublishDate)
            .FirstAsync(cancellationToken);

        reply.Add(first);
        timeRemaining -= first.DurationSpan ?? TimeSpan.Zero;

        var idleLoops = 0;
        while (idleLoops <= 2)
        {
            var addedVideo = false;

            foreach (var subscription in subscriptions)
            {
                var video = await dataContext.Videos
                    .Where(v => v.SubscriptionId == subscription.Id
                                && v.Watched == false
                                && !reply.Contains(v)
                                && !excludeOrEmpty.Contains(v))
                    .Include(static v => v.Subscription)
                    .OrderBy(static v => v.PublishDate)
                    .FirstOrDefaultAsync(cancellationToken);

                if (video == null || video.DurationSpan > timeRemaining)
                    continue;

                reply.Add(video);
                timeRemaining -= video.DurationSpan ?? TimeSpan.Zero;
                addedVideo = true;
            }

            if (!addedVideo)
                idleLoops++;
        }

        return reply;
    }

}
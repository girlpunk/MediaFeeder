using MassTransit;
using MediaFeeder.Data;
using Microsoft.EntityFrameworkCore;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace MediaFeeder.Providers.Youtube;

public sealed class YoutubeVideoSynchroniseConsumer(
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    IAmACommandProcessor bus
) : RequestHandlerAsync<YoutubeVideoSynchroniseContract>
{
    [RequestLoggingAsync(0, HandlerTiming.Before)]
    [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
    public async Task Consume(YoutubeVideoSynchroniseContract message, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var video = await db.Videos.SingleAsync(v => v.Id == message.VideoId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(video.DownloadedPath) || video.Duration is 0 or null || string.IsNullOrWhiteSpace(video.Thumb))
            await bus.PostAsync(new YoutubeActualVideoSynchroniseContract(video.Id), cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}

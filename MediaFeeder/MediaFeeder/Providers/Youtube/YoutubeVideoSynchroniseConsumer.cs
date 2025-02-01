using MassTransit;
using MediaFeeder.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Providers.Youtube;

public sealed class YoutubeVideoSynchroniseConsumer(
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    IBus bus
) : IConsumer<YoutubeVideoSynchroniseContract>
{
    public async Task Consume(ConsumeContext<YoutubeVideoSynchroniseContract> context)
    {
        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var video = await db.Videos.SingleAsync(v => v.Id == context.Message.VideoId, context.CancellationToken);

        if (video.DownloadedPath != null || video.Duration == 0 || string.IsNullOrWhiteSpace(video.Thumb))
            await bus.Publish(new YoutubeActualVideoSynchroniseContract(video.Id), context.CancellationToken);

        await db.SaveChangesAsync(context.CancellationToken);
    }
}

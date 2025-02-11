using Grpc.Core;
using MassTransit;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Services;

public sealed class ApiService(
    IBus bus,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    UserManager<AuthUser> userManager,
    IServiceProvider serviceProvider,
    IConfiguration configuration
) : API.APIBase
{
    public override async Task ListFolder(ListFolderRequest request, IServerStreamWriter<FolderReply> responseStream, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var folders = db.Folders
            .Where(folder => folder.UserId == user.Id && folder.ParentId == null)
            .Select(static folder => new
            {
                folder.Name,
                folder.Id,
                ChildFolders = folder.Subfolders.Select(static f => f.Id).ToList(),
                ChildSubscriptions = folder.Subscriptions.Select(static s => s.Id).ToList()
            });

        foreach (var folder in folders)
        {
            var folderReply = new FolderReply()
            {
                Name = folder.Name,
                Id = folder.Id,
            };
            folderReply.ChildFolders.AddRange(folder.ChildFolders);
            folderReply.ChildSubscriptions.AddRange(folder.ChildSubscriptions);

            await responseStream.WriteAsync(folderReply, context.CancellationToken);
        }
    }

    public override async Task<FolderReply> Folder(FolderRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var folder = await db.Folders
            .Select(static folder => new
            {
                folder.Name,
                folder.Id,
                folder.UserId,
                ChildFolders = folder.Subfolders.Select(static f => f.Id).ToList(),
                ChildSubscriptions = folder.Subscriptions.Select(static s => s.Id).ToList(),
            })
            .SingleOrDefaultAsync(f => f.Id == request.Id && f.UserId == user.Id, context.CancellationToken);

        if (folder == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        var reply = new FolderReply
        {
            Id = folder.Id,
            Name = folder.Name,
        };
        reply.ChildFolders.AddRange(folder.ChildFolders);
        reply.ChildSubscriptions.AddRange(folder.ChildSubscriptions);

        return reply;
    }

    public override async Task ListSubscription(ListSubscriptionRequest request, IServerStreamWriter<SubscriptionReply> responseStream, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var subscriptions = db.Subscriptions
            .Where(subscription => subscription.UserId == user.Id)
            .Select(static subscription => new SubscriptionReply
            {
                Name = subscription.Name,
                Id = subscription.Id,
                Thumbnail = $"/media/{subscription.Thumb}",
                Unwatched = subscription.Videos.Count(static v => !v.Watched)
            });

        foreach (var subscription in subscriptions)
            await responseStream.WriteAsync(subscription, context.CancellationToken);
    }

    public override async Task<SubscriptionReply> Subscription(SubscriptionRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var subscription = await db.Subscriptions
            .Where(subscription => subscription.UserId == user.Id && subscription.Id == request.Id)
            .Select(static subscription => new SubscriptionReply
            {
                Name = subscription.Name,
                Id = subscription.Id,
                Thumbnail = $"/media/{subscription.Thumb}",
                Unwatched = subscription.Videos.Count(static v => !v.Watched)
            })
            .SingleOrDefaultAsync(context.CancellationToken);

        if (subscription == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        return subscription;
    }

    public override async Task<VideoReply> Video(VideoRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos
            .Where(v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == request.Id)
            .Select(static v => new
            {
                v.Id,
                Title = v.Name,
                Thumbnail = $"/media/{v.Thumb}",
                v.Description,
                Downloaded = v.DownloadedPath != null,
                v.Duration,
                v.New,
                Published = v.PublishDate, //?.ToUnixTimeSeconds(),
                v.Views,
                v.Watched,
                v.DownloadedPath
            })
            .SingleOrDefaultAsync(context.CancellationToken);

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        var reply = new VideoReply()
        {
            Id = video.Id,
            Title = video.Title,
            Description = video.Description,
            Downloaded = video.Downloaded,
            New = video.New,
            Watched = video.Watched,
        };

        if (video.Duration != null)
            reply.Duration = video.Duration.Value;

        if (video.Published != null)
            reply.Published = video.Published.Value.ToUnixTimeSeconds();

        if (video.Views != null)
            reply.Views = video.Views.Value;

        if (video.DownloadedPath != null)
        {
            var mediaRoot = configuration.GetValue<string>("MediaRoot");
            ArgumentNullException.ThrowIfNull(mediaRoot);
            reply.DownloadPath = video.DownloadedPath.Replace(mediaRoot, "/media");
        }

        return reply;
    }

    public override async Task<DownloadReply> Download(DownloadRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos
            .Include(static v => v.Subscription)
            .SingleOrDefaultAsync(v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == request.Id,
                context.CancellationToken);

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        var videoProvider = serviceProvider.GetServices<IProvider>()
            .Single(provider => provider.ProviderIdentifier == video.Subscription?.Provider);

        var contractType = typeof(DownloadVideoContract<>).MakeGenericType(videoProvider.GetType());
        var contract = Activator.CreateInstance(contractType, new object[] { video.Id });
        ArgumentNullException.ThrowIfNull(contract);

        // var client = bus.CreateRequestClient<dynamic>();
        // var response = await client.GetResponse<DownloadReply>(context, context.CancellationToken);

        await bus.Publish(contract, context.CancellationToken);
        // return response?.Message ?? new DownloadReply
        // {
        //     Status = DownloadStatus.TemporaryError,
        //     ExitCode = -1
        // };

        return new DownloadReply
        {
            Status = DownloadStatus.InProgress,
        };
    }

    public override async Task<WatchedReply> Watched(WatchedRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos
            .Include(static v => v.Subscription)
            .SingleOrDefaultAsync(v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == request.Id,
                context.CancellationToken);

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        video.Watched = request.Watched;

        await db.SaveChangesAsync(context.CancellationToken);
        return new WatchedReply();
    }

    public override async Task<ShuffleReply> Shuffle(ShuffleRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var timeRemaining = TimeSpan.FromHours(1);
        var reply = new ShuffleReply();
        List<Subscription> subscriptions;

        if (request.HasFolderId)
            subscriptions = await db.Subscriptions
                .Where(s => s.ParentFolderId == request.FolderId && s.UserId == user.Id)
                .OrderBy(static _ => EF.Functions.Random())
                .ToListAsync();
        else if (request.HasSubscriptionId)
            subscriptions = [await db.Subscriptions.SingleAsync(s => s.Id == request.SubscriptionId && s.UserId == user.Id)];
        else
            subscriptions = await db.Subscriptions
                .Where(s => s.UserId == user.Id)
                .OrderBy(static _ => EF.Functions.Random())
                .ToListAsync();

        var first = await db.Videos
            .Where(v => v.Watched == false && subscriptions.Select(static s => s.Id).Contains(v.SubscriptionId))
            .OrderBy(static v => v.PublishDate)
            .FirstAsync();

        reply.Id.Add(first.Id);
        timeRemaining -= first.DurationSpan ?? TimeSpan.Zero;

        var idleLoops = 0;
        while (idleLoops <= 2)
        {
            var addedVideo = false;

            foreach (var subscription in subscriptions)
            {
                var video = await db.Videos
                    .Where(v => v.SubscriptionId == subscription.Id && v.Watched == false && !reply.Id.Contains(v.Id))
                    .OrderBy(static v => v.PublishDate)
                    .FirstOrDefaultAsync();

                if (video == null || video.DurationSpan > timeRemaining)
                    continue;

                reply.Id.Add(video.Id);
                timeRemaining -= video.DurationSpan ?? TimeSpan.Zero;
                addedVideo = true;
            }

            if (!addedVideo)
                idleLoops++;
        }

        return reply;
    }
}

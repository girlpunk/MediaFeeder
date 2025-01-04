using Google.Protobuf;
using Grpc.Core;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaToad;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Services;

public sealed class MediaToadService(
    ILogger<MediaToadService> logger,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    UserManager<AuthUser> userManager,
    IServiceProvider serviceProvider) : Media.MediaBase
{
    private ILogger<MediaToadService> Logger { get; } = logger;

    public override Task<AboutReply> About(AboutRequest request, ServerCallContext context)
    {
        Logger.LogInformation($"Got about request from {context.Peer}");

        return Task.FromResult(new AboutReply
        {
            Name = "MediaFeeder"
        });
    }

    public override async Task<HasMediaReply> HasMedia(HasMediaRequest request, ServerCallContext context)
    {
        var principal = context.GetHttpContext().User;

        if (principal == null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication failed"));

        var user = await userManager.GetUserAsync(principal);

        if (user == null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication failed"));

        if (!int.TryParse(request.Id, out var videoId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID"));

        await using var dbContext = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var video = await dbContext.Videos
            .Include(static v => v.Subscription)
            .ThenInclude(static s => s.Provider)
            .SingleOrDefaultAsync(v => v.Id == videoId, context.CancellationToken);

        if (video == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Not Found"));

        if (video.Subscription.UserId != user.Id)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Authorization failed"));

        string? mimeType;

        if (video.DownloadedPath != null)
        {
            new FileExtensionContentTypeProvider().TryGetContentType(video.DownloadedPath, out mimeType);
        }
        else
        {
            var videoProvider = serviceProvider.GetServices<IProvider>()
                .Single(provider => provider.ProviderIdentifier == video.Subscription.Provider);

            mimeType = videoProvider.StreamMimeType;
        }

        var mediaItem = new MediaItem()
        {
            Id = video.Id.ToString(),
            DurationMillis = (long)video.DurationSpan.TotalMilliseconds,
            Title = video.Name,
        };

        if (mimeType != null)
            mediaItem.MimeType = mimeType;

        return new HasMediaReply()
        {
            Existance = FileExistance.Exists,
            Item = mediaItem,
        };
    }

    public override async Task ReadMedia(ReadMediaRequest request, IServerStreamWriter<ReadMediaReply> responseStream, ServerCallContext context)
    {
        var principal = context.GetHttpContext().User;

        if (principal == null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication failed"));

        var user = await userManager.GetUserAsync(principal);

        if (user == null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication failed"));

        if (!int.TryParse(request.Id, out var videoId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID"));

        await using var dbContext = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var video = await dbContext.Videos
            .Include(static v => v.Subscription)
            .ThenInclude(static s => s.Provider)
            .SingleOrDefaultAsync(v => v.Id == videoId, context.CancellationToken);

        if (video == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Not Found"));

        if (video.Subscription.UserId != user.Id)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Authorization failed"));

        if (video.DownloadedPath == null)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Not downloaded"));

        new FileExtensionContentTypeProvider().TryGetContentType(video.DownloadedPath, out var mimeType);

        await using var videoStream = new FileStream(video.DownloadedPath, FileMode.Open);

        var buffer = new Memory<byte>(new byte[1024 * 32]);
        var bytesRead = -1;

        while (!context.CancellationToken.IsCancellationRequested && bytesRead != 0)
        {
            bytesRead = await videoStream.ReadAsync(buffer, context.CancellationToken);

            var b = ByteString.CopyFrom(buffer.Span);
            var reply = new ReadMediaReply()
            {
                TotalFileLength = videoStream.Length,
                Content = b
            };

            if (mimeType != null)
                reply.MimeType = mimeType;

            await responseStream.WriteAsync(reply, context.CancellationToken);
        }
    }

    public override async Task<ListNodeReply> ListNode(ListNodeRequest request, ServerCallContext context)
    {
        var principal = context.GetHttpContext().User;

        if (principal == null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication failed"));

        var user = await userManager.GetUserAsync(principal);

        if (user == null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication failed"));

        if (!int.TryParse(request.NodeId[1..], out var nodeId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID"));

        return request.NodeId[0] switch
        {
            's' => await ListSubscription(nodeId, user, context.CancellationToken),
            'f' => await ListFolder(nodeId, user, context.CancellationToken),
            _ => throw new RpcException(new Status(StatusCode.NotFound, "Not Found"))
        };
    }

    private async Task<ListNodeReply> ListSubscription(int nodeId, AuthUser user, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var subscription = await context.Subscriptions
            .Include(static s => s.Videos)
            .SingleOrDefaultAsync(s => s.Id == nodeId, cancellationToken);

        if (subscription == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Not Found"));

        if (subscription.UserId != user.Id)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Authorization failed"));

        return new ListNodeReply()
        {
            Item =
            {
                subscription.Videos.Select(static v =>
                    new MediaItem()
                    {
                        Id = v.Id.ToString(),
                        DurationMillis = (long)v.DurationSpan.TotalMilliseconds,
                        Title = v.Name
                    }
                )
            }
        };
    }

    private async Task<ListNodeReply> ListFolder(int nodeId, AuthUser user, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var folder = await context.Folders
            .Include(static s => s.Subscriptions)
            .Include(static s => s.Subfolders)
            .SingleOrDefaultAsync(s => s.Id == nodeId, cancellationToken);

        if (folder == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Not Found"));

        if (folder.UserId != user.Id)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Authorization failed"));

        return new ListNodeReply()
        {
            Node =
            {
                folder.Subfolders.Select(static f =>
                        new MediaNode()
                        {
                            Id = $"f{f.Id}",
                            Title = f.Name
                        })
                    .Concat(folder.Subscriptions.Select(static s =>
                        new MediaNode()
                        {
                            Id = $"s{s.Id}",
                            Title = s.Name,
                        }))
            }
        };
    }
}

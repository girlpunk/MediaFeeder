using Google.Protobuf;
using Grpc.Core;
using MassTransit;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Helpers;
using MediaFeeder.PlaybackManager;
using MediaFeeder.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;

namespace MediaFeeder.Services;

public sealed class ApiService(
    IBus bus,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    UserManager<AuthUser> userManager,
    IServiceProvider serviceProvider,
    PlaybackSessionManager playbackSessionManager,
    ILogger<ApiService> logger
) : API.APIBase
{
    public override async Task ListFolder(ListFolderRequest request, IServerStreamWriter<FolderReply> responseStream, ServerCallContext context)
    {
        logger.LogError(userManager.GetType().ToString());

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

            if (request.IncludeUnwatchedCounts)
            {
                var videos = db.Videos
                    .Where(v => v.Subscription.ParentFolderId == folder.Id);

                folderReply.UnwatchedCounts = new UnwatchedCounts()
                {
                    UnwatchedCount = videos.Count(static v => !v.Watched),
                    UnwatchedDuration = videos.Where(static v => !v.Watched).Sum(static v => v.Duration) ?? 0
                };
            }

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

        if (request.IncludeUnwatchedCounts)
        {
            var videos = db.Videos
                .Where(v => v.Subscription.ParentFolderId == folder.Id);

            reply.UnwatchedCounts = new UnwatchedCounts()
            {
                UnwatchedCount = videos.Count(static v => !v.Watched),
                UnwatchedDuration = videos.Where(static v => !v.Watched).Sum(static v => v.Duration) ?? 0
            };
        }

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
                Id = subscription.Id
            });

        foreach (var subscription in subscriptions)
        {
            if (request.IncludeUnwatchedCounts)
            {
                var videos = db.Videos
                    .Where(v => v.SubscriptionId == subscription.Id);

                subscription.UnwatchedCounts = new UnwatchedCounts()
                {
                    UnwatchedCount = videos.Count(static v => !v.Watched),
                    UnwatchedDuration = videos.Where(static v => !v.Watched).Sum(static v => v.Duration) ?? 0
                };
            }

            await responseStream.WriteAsync(subscription, context.CancellationToken);
        }
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
            })
            .SingleOrDefaultAsync(context.CancellationToken);

        if (subscription == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        if (request.IncludeUnwatchedCounts)
        {
            var videos = db.Videos
                .Where(v => v.SubscriptionId == subscription.Id);

            subscription.UnwatchedCounts = new UnwatchedCounts()
            {
                UnwatchedCount = videos.Count(static v => !v.Watched),
                UnwatchedDuration = videos.Where(static v => !v.Watched).Sum(static v => v.Duration) ?? 0
            };
        }

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
                v.Description,
                Downloaded = v.DownloadedPath != null,
                v.Duration,
                v.New,
                Published = v.PublishDate, //?.ToUnixTimeSeconds(),
                v.VideoId,
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
            VideoId = video.VideoId,
        };

        if (video.Duration != null)
            reply.Duration = video.Duration.Value;

        if (video.Published != null)
            reply.Published = video.Published.Value.ToUnixTimeSeconds();

        if (video.Views != null)
            reply.Views = video.Views.Value;

        return reply;
    }

    public override async Task<DownloadReply> StartDownload(DownloadRequest request, ServerCallContext context)
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

        await bus.PublishWithGuid(contract, context.CancellationToken);
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

    public override async Task<SearchReply> Search(SearchRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos.SingleOrDefaultAsync(
            v => v.Subscription.UserId == user.Id
            && v.Subscription.Provider == request.Provider
            && v.VideoId == request.ProviderVideoId,
            context.CancellationToken);

        var reply = new SearchReply();
        if (video != null) reply.VideoId.Add(video.Id);
        return reply;
    }

    public override async Task<ShuffleReply> Shuffle(ShuffleRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var videos = await DoShuffle(
            db,
            user,
            request.DurationMinutes,
            request.HasFolderId ? request.FolderId : null,
            request.HasSubscriptionId ? request.SubscriptionId : null);
        var reply = new ShuffleReply();
        foreach (var video in videos)
        {
            reply.Id.Add(video.Id);
        }
        return reply;
    }

    private async Task<List<Video>> DoShuffle(MediaFeederDataContext db, AuthUser user, int? durationMinutes, int? folderId, int? subscriptionId)
    {
        var timeRemaining = TimeSpan.FromMinutes(durationMinutes ?? 60);
        List<Video> reply = [];
        List<Subscription> subscriptions;

        if (folderId != null)
            subscriptions = await db.Subscriptions
                .Where(s => s.ParentFolderId == folderId && s.UserId == user.Id)
                .OrderBy(static _ => EF.Functions.Random())
                .ToListAsync();
        else if (subscriptionId != null)
            subscriptions = [await db.Subscriptions.SingleAsync(s => s.Id == subscriptionId && s.UserId == user.Id)];
        else
            subscriptions = await db.Subscriptions
                .Where(s => s.UserId == user.Id)
                .OrderBy(static _ => EF.Functions.Random())
                .ToListAsync();

        var query = db.Videos
            .Where(v => v.Watched == false && subscriptions.Select(static s => s.Id).Contains(v.SubscriptionId));

        if (timeRemaining.TotalMinutes < 60)
            query = query.Where(v => v.Duration <= timeRemaining.TotalSeconds);

        var first = await query
            .OrderBy(static v => v.PublishDate)
            .FirstAsync();

        reply.Add(first);
        timeRemaining -= first.DurationSpan ?? TimeSpan.Zero;

        var idleLoops = 0;
        while (idleLoops <= 2)
        {
            var addedVideo = false;

            foreach (var subscription in subscriptions)
            {
                var video = await db.Videos
                    .Where(v => v.SubscriptionId == subscription.Id && v.Watched == false && !reply.Contains(v))
                    .OrderBy(static v => v.PublishDate)
                    .FirstOrDefaultAsync();

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

    public override async Task GetSubscriptionThumbnail(GetSubscriptionThumbnailRequest request, IServerStreamWriter<GetSubscriptionThumbnailReply> responseStream,
        ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var subscription = await db.Subscriptions
            .Where(subscription => subscription.UserId == user.Id && subscription.Id == request.Id)
            .SingleOrDefaultAsync(context.CancellationToken);

        if (subscription == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        if (subscription.Thumb == null)
            throw new RpcException(context.Status = new Status(StatusCode.Unavailable, "Not Downloaded"));

        await using var file = File.OpenRead(subscription.Thumb);

        var buffer = new byte[8 * 1024];
        int bytesRead;
        while ((bytesRead = await file.ReadAsync(buffer)) > 0)
        {
            var reply = new GetSubscriptionThumbnailReply()
            {
                Data = ByteString.CopyFrom(buffer, 0, bytesRead)
            };

            await responseStream.WriteAsync(reply);
        }
    }

    public override async Task GetVideoThumbnail(GetVideoThumbnailRequest request, IServerStreamWriter<GetVideoThumbnailReply> responseStream, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos
            .SingleOrDefaultAsync(v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == request.Id);

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        if (video.Thumb == null)
            throw new RpcException(context.Status = new Status(StatusCode.Unavailable, "Not Downloaded"));

        await using var file = File.OpenRead(video.Thumb);

        var buffer = new byte[8 * 1024];
        int bytesRead;
        while ((bytesRead = await file.ReadAsync(buffer)) > 0)
        {
            var reply = new GetVideoThumbnailReply()
            {
                Data = ByteString.CopyFrom(buffer, 0, bytesRead)
            };

            await responseStream.WriteAsync(reply);
        }
    }

    public override async Task GetVideo(GetVideoRequest request, IServerStreamWriter<GetVideoReply> responseStream, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos
            .SingleOrDefaultAsync(v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == request.Id);

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        if (video.DownloadedPath == null)
            throw new RpcException(context.Status = new Status(StatusCode.Unavailable, "Not Downloaded"));

        await using var file = File.OpenRead(video.DownloadedPath);

        var buffer = new byte[8 * 1024];
        int bytesRead;
        while ((bytesRead = await file.ReadAsync(buffer)) > 0)
        {
            var reply = new GetVideoReply()
            {
                Data = ByteString.CopyFrom(buffer, 0, bytesRead)
            };

            await responseStream.WriteAsync(reply);
        }
    }

    public override async Task PlaybackSession(IAsyncStreamReader<PlaybackSessionRequest> requestStream, IServerStreamWriter<PlaybackSessionReply> responseStream, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        // TODO pass some kinda init block to this so listeners only see the ready object?
        using var session = playbackSessionManager.NewSession(user);

        session.PlayPauseEvent += async () => await responseStream.WriteAsync(new PlaybackSessionReply { ShouldPlayPause = true }, context.CancellationToken);
        session.SkipEvent += async () => await responseStream.WriteAsync(new PlaybackSessionReply { ShouldSkip = true }, context.CancellationToken);
        session.WatchEvent += async () => await responseStream.WriteAsync(new PlaybackSessionReply { ShouldWatch = true }, context.CancellationToken);
        session.AddVideos += async minutes =>
        {
            if (session.SelectedFolderId == null) return;
            var videos = await DoShuffle(
                db,
                user,
                minutes,
                session.SelectedFolderId,
                null);
            session.AddToPlaylist(videos);
        };

        while (!context.CancellationToken.IsCancellationRequested)
        {
            if (await requestStream.MoveNext(context.CancellationToken))
            {
                switch (requestStream.Current.Action)
                {
                    case PlaybackSessionAction.PopNextVideo:
                        Video video = session.PopPlaylistHead();
                        if (video != null) responseStream.WriteAsync(new PlaybackSessionReply { NextVideoId = video.Id }, context.CancellationToken);
                        break;
                }

                if (requestStream.Current.HasTitle)
                    session.Title = requestStream.Current.Title;

                if (requestStream.Current.HasDuration)
                    session.CurrentPosition = requestStream.Current.Duration != null ? TimeSpan.FromSeconds(requestStream.Current.Duration) : null;

                if (requestStream.Current.HasLoaded)
                    session.Loaded = requestStream.Current.Loaded;

                if (requestStream.Current.HasProvider)
                {
                    if (requestStream.Current.Provider != null)
                    {
                        session.Provider = serviceProvider.GetServices<IProvider>()
                            .SingleOrDefault(provider => provider.ProviderIdentifier == requestStream.Current.Provider)
                            ?.Provider;
                    }
                    else
                    {
                        session.Provider = null;
                    }
                }

                if (requestStream.Current.HasQuality)
                    session.Quality = requestStream.Current.Quality;

                if (requestStream.Current.HasRate)
                    session.Rate = requestStream.Current.Rate;

                if (requestStream.Current.HasState)
                    session.State = requestStream.Current.State;

                if (requestStream.Current.HasVideoId)
                {
                    if (requestStream.Current.VideoId != null)
                    {
                        session.Video = db.Videos.Include(static v => v.Subscription).Single(v => v.Id == requestStream.Current.VideoId);
                    }
                    else
                    {
                        session.Video = null;
                    }
                }

                if (requestStream.Current.HasVolume)
                    session.Volume = requestStream.Current.Volume;

                if (requestStream.Current.EndSession) return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), context.CancellationToken);
        }
    }
}

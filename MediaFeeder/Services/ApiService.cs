using Google.Protobuf;
using Grpc.Core;
using MassTransit;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Filters;
using MediaFeeder.Helpers;
using MediaFeeder.PlaybackManager;
using MediaFeeder.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
                    .Where(v => v.Subscription!.ParentFolderId == folder.Id);

                folderReply.UnwatchedCounts = new UnwatchedCounts
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
                .Where(v => v.Subscription!.ParentFolderId == folder.Id);

            reply.UnwatchedCounts = new UnwatchedCounts
            {
                UnwatchedCount = videos.Count(static v => !v.Watched),
                UnwatchedDuration = videos.Where(static v => !v.Watched).Sum(static v => v.Duration) ?? 0
            };
        }

        return reply;
    }

    public override async Task<AddSubscriptionReply> AddSubscription(AddSubscriptionRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Subscription name cannot be empty");

        if (string.IsNullOrWhiteSpace(request.ChannelId))
            throw new InvalidOperationException("Channel ID cannot be empty");

        if (string.IsNullOrWhiteSpace(request.PlaylistId))
            throw new InvalidOperationException("Playlist ID cannot be empty");

        if (string.IsNullOrWhiteSpace(request.Provider))
            throw new InvalidOperationException("Provider cannot be empty");

        ArgumentOutOfRangeException.ThrowIfLessThan(request.FolderId, 0);

        var subscription = new Subscription
        {
            ChannelId = request.ChannelId,
            ChannelName = request.Name,
            Name = request.Name,
            ParentFolderId = request.FolderId,
            PlaylistId = request.PlaylistId,
            Provider = request.Provider ?? throw new InvalidOperationException(),
            Description = "",
            UserId = user.Id
        };

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(context.CancellationToken);

        return new AddSubscriptionReply { SubscriptionId = subscription.Id };
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
        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var video = await CheckAuthAndGetVideo(context, db, request.Id);

        DateTimeOffset? date = request is { HasWhenSeconds: true, WhenSeconds: > 0 }
            ? DateTimeOffset.FromUnixTimeSeconds(request.WhenSeconds)
            : null;
        video.MarkWatched(request.ActuallyWatched, date);

        await db.SaveChangesAsync(context.CancellationToken);
        return new WatchedReply();
    }

    public override async Task<SavePlaybackPositionReply> SavePlaybackPosition(SavePlaybackPositionRequest request, ServerCallContext context)
    {
        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var video = await CheckAuthAndGetVideo(context, db, request.Id);

        if (request.PositionSeconds < 0 || request.PositionSeconds > video.Duration)
            throw new RpcException(context.Status = new Status(StatusCode.InvalidArgument, "Invalid PositionSeconds."));

        video.PlaybackPosition = request.PositionSeconds;
        await db.SaveChangesAsync(context.CancellationToken);
        return new SavePlaybackPositionReply();
    }

    private async Task<Video?> CheckAuthAndGetVideo(ServerCallContext context, MediaFeederDataContext db, int videoId)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        var video = await db.Videos
            .Include(static v => v.Subscription)
            .SingleOrDefaultAsync(v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == videoId,
                context.CancellationToken);

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));
        return video;
    }

    public override async Task<SearchReply> Search(SearchRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var query = db.Videos.AsQueryable()
            .Where(v => v.Subscription!.UserId == user.Id);

        if (request.HasProvider)
            query = query.Where(v => v.Subscription.Provider == request.Provider);

        if (request.HasProviderVideoId)
            query = query.Where(v => v.VideoId == request.ProviderVideoId);

        if (request.HasFolderId)
        {
            var subfolderIds = await Data.db.Folder.RecursiveFolderIds(db, request.FolderId, user.Id);
            query = query.Where(v => subfolderIds.Contains(v.Subscription!.ParentFolderId));
        }

        if (request.HasStar)
            query = query.Where(v => v.Star == request.Star);

        var videos = await query
            .SortVideos(SortOrders.Oldest)
            .ToListAsync(context.CancellationToken);

        var reply = new SearchReply();
        foreach (var video in videos)
        {
            var found = new FoundVideo { VideoId = video.Id, Watched = video.Watched,  Star = video.Star };
            if (video.WatchedDate != null) found.WatchedWhenSeconds = video.WatchedDate.Value.ToUnixTimeSeconds();
            if (video.VideoId != null) found.ProviderVideoId = video.VideoId;
            reply.Videos.Add(found);
        }

        return reply;
    }

    public override async Task<ShuffleReply> Shuffle(ShuffleRequest request, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var dataContext = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var videos = await ShuffleHelper.Shuffle(
            dataContext,
            user,
            request.DurationMinutes,
            request.HasFolderId ? request.FolderId : null,
            request.HasSubscriptionId ? request.SubscriptionId : null,
            cancellationToken: context.CancellationToken);
        var reply = new ShuffleReply();
        foreach (var video in videos)
            reply.Id.Add(video.Id);

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
        while ((bytesRead = await file.ReadAsync(buffer, context.CancellationToken)) > 0)
        {
            var reply = new GetSubscriptionThumbnailReply()
            {
                Data = ByteString.CopyFrom(buffer, 0, bytesRead)
            };

            await responseStream.WriteAsync(reply, context.CancellationToken);
        }
    }

    public override async Task GetVideoThumbnail(GetVideoThumbnailRequest request, IServerStreamWriter<GetVideoThumbnailReply> responseStream, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos
            .SingleOrDefaultAsync(v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == request.Id, context.CancellationToken);

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        if (video.Thumb == null)
            throw new RpcException(context.Status = new Status(StatusCode.Unavailable, "Not Downloaded"));

        await using var file = File.OpenRead(video.Thumb);

        var buffer = new byte[8 * 1024];
        int bytesRead;
        while ((bytesRead = await file.ReadAsync(buffer, context.CancellationToken)) > 0)
        {
            var reply = new GetVideoThumbnailReply()
            {
                Data = ByteString.CopyFrom(buffer, 0, bytesRead)
            };

            await responseStream.WriteAsync(reply, context.CancellationToken);
        }
    }

    public override async Task GetVideo(GetVideoRequest request, IServerStreamWriter<GetVideoReply> responseStream, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos
            .SingleOrDefaultAsync(v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == request.Id, context.CancellationToken);

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        if (video.DownloadedPath == null)
            throw new RpcException(context.Status = new Status(StatusCode.Unavailable, "Not Downloaded"));

        await using var file = File.OpenRead(video.DownloadedPath);

        var buffer = new byte[8 * 1024];
        int bytesRead;
        while ((bytesRead = await file.ReadAsync(buffer, context.CancellationToken)) > 0)
        {
            var reply = new GetVideoReply()
            {
                Data = ByteString.CopyFrom(buffer, 0, bytesRead)
            };

            await responseStream.WriteAsync(reply, context.CancellationToken);
        }
    }

    public override async Task PlaybackSession(IAsyncStreamReader<PlaybackSessionRequest> requestStream, IServerStreamWriter<PlaybackSessionReply> responseStream, ServerCallContext context)
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        // TODO pass some kinda init block to this so listeners only see the ready object?
        using var session = playbackSessionManager.NewSession(user);

        session.PlayPauseEvent += async () =>
        {
            var reply = new PlaybackSessionReply { ShouldPlayPause = true };
            if (session.Video?.Id != null) reply.NextVideoId = session.Video.Id;
            if (session.CurrentPosition != null) reply.PlaybackPosition = (int)session.CurrentPosition.Value.TotalSeconds;
            await responseStream.WriteAsync(reply, context.CancellationToken);
        };
        session.SeekRelativeEvent += async seconds => await responseStream.WriteAsync(new PlaybackSessionReply { ShouldSeekRelativeSeconds = seconds }, context.CancellationToken);
        session.SkipEvent += async () => await responseStream.WriteAsync(new PlaybackSessionReply { ShouldSkip = true }, context.CancellationToken);
        session.WatchEvent += async () => await responseStream.WriteAsync(new PlaybackSessionReply { ShouldWatch = true }, context.CancellationToken);
        session.ChangeRateEvent += async (direction) => await responseStream.WriteAsync(new PlaybackSessionReply { ShouldChangeRate = direction ? 1 : -1 }, context.CancellationToken);
        session.ChangeVolumeEvent += async (direction) => await responseStream.WriteAsync(new PlaybackSessionReply { ShouldChangeVolume = direction ? 1 : -1 }, context.CancellationToken);
        session.ToggleSubtitleEvent += async () => await responseStream.WriteAsync(new PlaybackSessionReply { ShouldToggleSubtitles = true }, context.CancellationToken);
        session.AddVideos += async minutes =>
        {
            if (session.SelectedFolderId == null) return;

            var exclude = new List<Video>(session.Playlist);
            if (session.Video != null) exclude.Add(session.Video);

            await using var dataContext = await contextFactory.CreateDbContextAsync();
            var videos = await ShuffleHelper.Shuffle(
                dataContext,
                user,
                minutes,
                session.SelectedFolderId,
                null,
                exclude);
            session.AddToPlaylist(videos);
        };

        while (true)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (await requestStream.MoveNext(context.CancellationToken))
            {
                switch (requestStream.Current.Action)
                {
                    case PlaybackSessionAction.PopNextVideo:
                        var video = session.PopPlaylistHead();
                        if (video != null)
                        {
                            var reply = new PlaybackSessionReply { NextVideoId = video.Id };

                            // give our video object cos session does not update until playback starts.
                            var position = session.PlaybackPositionToRestore(video);
                            if (position != null) reply.PlaybackPosition = position.Value;

                            await responseStream.WriteAsync(reply, context.CancellationToken);
                        }

                        break;
                }

                if (requestStream.Current.HasTitle)
                    session.Title = requestStream.Current.Title;

                if (requestStream.Current.HasPosition)
                    session.CurrentPosition = requestStream.Current.Position != null ? TimeSpan.FromSeconds(requestStream.Current.Position) : null;

                if (requestStream.Current.HasLoaded)
                    session.Loaded = requestStream.Current.Loaded;

                if (requestStream.Current.HasProvider)
                {
                    if (requestStream.Current.Provider != null)
                        session.Provider = serviceProvider.GetServices<IProvider>()
                            .SingleOrDefault(provider => provider.ProviderIdentifier == requestStream.Current.Provider)
                            ?.Provider;
                    else
                        session.Provider = null;
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
                        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

                        session.Video = await db.Videos
                            .Include(static v => v.Subscription)
                            .SingleAsync(v => v.Id == requestStream.Current.VideoId, context.CancellationToken);
                    }
                    else
                    {
                        session.Video = null;
                    }
                }

                if (requestStream.Current.HasVolume)
                    session.Volume = requestStream.Current.Volume;

                if (requestStream.Current.HasSupportsRateChange)
                    session.SupportsRateChange = requestStream.Current.SupportsRateChange;

                if (requestStream.Current.HasSupportsVolumeChange)
                    session.SupportsVolumeChange = requestStream.Current.SupportsVolumeChange;

                if (requestStream.Current.HasSupportsSubtitles)
                    session.SupportsSubtitles = requestStream.Current.SupportsSubtitles;

                if (requestStream.Current.HasSubtitles)
                    session.Subtitles = requestStream.Current.Subtitles;

                if (requestStream.Current.EndSession)
                    return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), context.CancellationToken);
        }
    }
}

using System.Reflection;
using BlazorComponentUtilities;
using Google.Protobuf;
using Grpc.Core;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Filters;
using MediaFeeder.Helpers;
using MediaFeeder.PlaybackManager;
using MediaFeeder.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

namespace MediaFeeder.Services;

public sealed class ApiService(
    ITimeTickerManager<TimeTickerEntity> timeTicker,
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    UserManager<AuthUser> userManager,
    IServiceProvider serviceProvider,
    PlaybackSessionManager playbackSessionManager,
    ILogger<ApiService> logger
) : API.APIBase
{
    public override async Task ListFolder(
        ListFolderRequest request,
        IServerStreamWriter<FolderReply> responseStream,
        ServerCallContext context
    )
    {
        logger.LogDebug("Start API List Folder");
        logger.LogError(userManager.GetType().ToString());

        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var folders = db
            .Folders.Where(folder => folder.UserId == user.Id && folder.ParentId == null)
            .Select(static folder => new
            {
                folder.Name,
                folder.Id,
                ChildFolders = folder.Subfolders.Select(static f => f.Id).ToList(),
                ChildSubscriptions = folder.Subscriptions.Select(static s => s.Id).ToList(),
            });

        foreach (var folder in folders)
        {
            var folderReply = new FolderReply() { Name = folder.Name, Id = folder.Id };
            folderReply.ChildFolders.AddRange(folder.ChildFolders);
            folderReply.ChildSubscriptions.AddRange(folder.ChildSubscriptions);

            if (request.IncludeUnwatchedCounts)
            {
                var videos = db.Videos.Where(v => v.Subscription!.ParentFolderId == folder.Id);

                folderReply.UnwatchedCounts = new UnwatchedCounts
                {
                    UnwatchedCount = videos.Count(static v => !v.Watched),
                    UnwatchedDuration =
                        videos.Where(static v => !v.Watched).Sum(static v => v.Duration) ?? 0,
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

        var folder = await db
            .Folders.Select(static folder => new
            {
                folder.Name,
                folder.Id,
                folder.UserId,
                ChildFolders = folder.Subfolders.Select(static f => f.Id).ToList(),
                ChildSubscriptions = folder.Subscriptions.Select(static s => s.Id).ToList(),
            })
            .SingleOrDefaultAsync(
                f => f.Id == request.Id && f.UserId == user.Id,
                context.CancellationToken
            );

        if (folder == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        var reply = new FolderReply { Id = folder.Id, Name = folder.Name };
        reply.ChildFolders.AddRange(folder.ChildFolders);
        reply.ChildSubscriptions.AddRange(folder.ChildSubscriptions);

        if (request.IncludeUnwatchedCounts)
        {
            var videos = db.Videos.Where(v => v.Subscription!.ParentFolderId == folder.Id);

            reply.UnwatchedCounts = new UnwatchedCounts
            {
                UnwatchedCount = videos.Count(static v => !v.Watched),
                UnwatchedDuration =
                    videos.Where(static v => !v.Watched).Sum(static v => v.Duration) ?? 0,
            };
        }

        return reply;
    }

    public override async Task<AddSubscriptionReply> AddSubscription(
        AddSubscriptionRequest request,
        ServerCallContext context
    )
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
            UserId = user.Id,
        };

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(context.CancellationToken);

        return new AddSubscriptionReply { SubscriptionId = subscription.Id };
    }

    public override async Task ListSubscription(
        ListSubscriptionRequest request,
        IServerStreamWriter<SubscriptionReply> responseStream,
        ServerCallContext context
    )
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var subscriptions = db
            .Subscriptions.Where(subscription => subscription.UserId == user.Id)
            .Select(static subscription => new SubscriptionReply
            {
                Name = subscription.Name,
                Id = subscription.Id,
            });

        foreach (var subscription in subscriptions)
        {
            if (request.IncludeUnwatchedCounts)
            {
                var videos = db.Videos.Where(v => v.SubscriptionId == subscription.Id);

                subscription.UnwatchedCounts = new UnwatchedCounts()
                {
                    UnwatchedCount = videos.Count(static v => !v.Watched),
                    UnwatchedDuration =
                        videos.Where(static v => !v.Watched).Sum(static v => v.Duration) ?? 0,
                };
            }

            await responseStream.WriteAsync(subscription, context.CancellationToken);
        }
    }

    public override async Task<SubscriptionReply> Subscription(
        SubscriptionRequest request,
        ServerCallContext context
    )
    {
        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var subscription = await db
            .Subscriptions.Where(subscription =>
                subscription.UserId == user.Id && subscription.Id == request.Id
            )
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
            var videos = db.Videos.Where(v => v.SubscriptionId == subscription.Id);

            subscription.UnwatchedCounts = new UnwatchedCounts()
            {
                UnwatchedCount = videos.Count(static v => !v.Watched),
                UnwatchedDuration =
                    videos.Where(static v => !v.Watched).Sum(static v => v.Duration) ?? 0,
            };
        }

        return subscription;
    }

    public override async Task<VideoReply> Video(VideoRequest request, ServerCallContext context)
    {
        logger.LogDebug("Start API Get Video");

        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db
            .Videos.Include(static v => v.Subscription)
            .Where(v =>
                v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == request.Id
            )
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
                v.DownloadedPath,
                v.Subscription,
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

        // TODO replace this with a URL to a local download, if available (falling back to external URL)
        if (video.DownloadedPath != null)
            reply.MediaUrl = video.DownloadedPath;

        if (video.Subscription?.Provider != null)
            reply.Provider = video.Subscription.Provider;

        if (video.Duration != null)
            reply.Duration = video.Duration.Value;

        if (video.Published != null)
            reply.Published = video.Published.Value.ToUnixTimeSeconds();

        if (video.Views != null)
            reply.Views = video.Views.Value;

        return reply;
    }

    public override async Task<DownloadReply> StartDownload(
        DownloadRequest request,
        ServerCallContext context
    )
    {
        logger.LogDebug("Start API Start Download");

        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db
            .Videos.Include(static v => v.Subscription)
            .SingleOrDefaultAsync(
                v =>
                    v.Subscription != null
                    && v.Subscription.UserId == user.Id
                    && v.Id == request.Id,
                context.CancellationToken
            );

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        var videoProvider = serviceProvider
            .GetServices<IProvider>()
            .Single(provider => provider.ProviderIdentifier == video.Subscription?.Provider);

        await timeTicker.AddDownloadVideo(video.Id, videoProvider, logger, context.CancellationToken);

        return new DownloadReply { Status = DownloadStatus.InProgress };
    }

    public override async Task<WatchedReply> Watched(
        WatchedRequest request,
        ServerCallContext context
    )
    {
        logger.LogDebug("Start API Mark as Watched");

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var video = await CheckAuthAndGetVideo(context, db, request.Id);

        DateTimeOffset? date = request is { HasWhenSeconds: true, WhenSeconds: > 0 }
            ? DateTimeOffset.FromUnixTimeSeconds(request.WhenSeconds)
            : null;
        video.MarkWatched(request.ActuallyWatched, date);

        await db.SaveChangesAsync(context.CancellationToken);
        return new WatchedReply();
    }

    public override async Task<SavePlaybackPositionReply> SavePlaybackPosition(
        SavePlaybackPositionRequest request,
        ServerCallContext context
    )
    {
        logger.LogDebug("Start API Save Playback Position");

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);
        var video = await CheckAuthAndGetVideo(context, db, request.Id);

        if (request.PositionSeconds < 0 || request.PositionSeconds > video.Duration)
            throw new RpcException(
                context.Status = new Status(StatusCode.InvalidArgument, "Invalid PositionSeconds.")
            );

        video.PlaybackPosition = request.PositionSeconds;
        await db.SaveChangesAsync(context.CancellationToken);
        return new SavePlaybackPositionReply();
    }

    private async Task<Video> CheckAuthAndGetVideo(
        ServerCallContext context,
        MediaFeederDataContext db,
        int videoId
    )
    {
        logger.LogDebug("Start API Check Auth & Get Video");

        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        var video = await db
            .Videos.Include(static v => v.Subscription)
            .SingleOrDefaultAsync(
                v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == videoId,
                context.CancellationToken
            );

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        return video;
    }

    public override async Task<SearchReply> Search(SearchRequest request, ServerCallContext context)
    {
        logger.LogDebug("Start API Search");

        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var query = db.Videos.AsQueryable().Where(v => v.Subscription!.UserId == user.Id);

        if (request.HasProvider)
            query = query.Where(v => v.Subscription.Provider == request.Provider);

        if (request.HasProviderVideoId)
            query = query.Where(v => v.VideoId == request.ProviderVideoId);

        if (request.HasFolderId)
        {
            var subfolderIds = await Data.db.Folder.RecursiveFolderIds(
                db,
                request.FolderId,
                user.Id
            );
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
            var found = new FoundVideo
            {
                VideoId = video.Id,
                Watched = video.Watched,
                Star = video.Star,
            };
            if (video.WatchedDate != null)
                found.WatchedWhenSeconds = video.WatchedDate.Value.ToUnixTimeSeconds();
            if (video.VideoId != null)
                found.ProviderVideoId = video.VideoId;
            reply.Videos.Add(found);
        }

        return reply;
    }

    public override async Task<ShuffleReply> Shuffle(
        ShuffleRequest request,
        ServerCallContext context
    )
    {
        logger.LogDebug("Start API Shuffle");

        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var dataContext = await contextFactory.CreateDbContextAsync(
            context.CancellationToken
        );
        var videos = await ShuffleHelper.Shuffle(
            dataContext,
            user,
            request.DurationMinutes,
            request.HasFolderId ? request.FolderId : null,
            request.HasSubscriptionId ? request.SubscriptionId : null,
            cancellationToken: context.CancellationToken
        );
        var reply = new ShuffleReply();
        foreach (var video in videos)
            reply.Id.Add(video.Id);

        return reply;
    }

    public override async Task GetSubscriptionThumbnail(
        GetSubscriptionThumbnailRequest request,
        IServerStreamWriter<GetSubscriptionThumbnailReply> responseStream,
        ServerCallContext context
    )
    {
        logger.LogDebug("Start API Get Subscription Thumbnail");

        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var subscription = await db
            .Subscriptions.Where(subscription =>
                subscription.UserId == user.Id && subscription.Id == request.Id
            )
            .SingleOrDefaultAsync(context.CancellationToken);

        if (subscription == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        if (subscription.Thumb == null)
            throw new RpcException(
                context.Status = new Status(StatusCode.Unavailable, "Not Downloaded")
            );

        await using var file = File.OpenRead(subscription.Thumb);

        var buffer = new byte[8 * 1024];
        int bytesRead;
        while ((bytesRead = await file.ReadAsync(buffer, context.CancellationToken)) > 0)
        {
            var reply = new GetSubscriptionThumbnailReply()
            {
                Data = ByteString.CopyFrom(buffer, 0, bytesRead),
            };

            await responseStream.WriteAsync(reply, context.CancellationToken);
        }
    }

    public override async Task GetVideoThumbnail(
        GetVideoThumbnailRequest request,
        IServerStreamWriter<GetVideoThumbnailReply> responseStream,
        ServerCallContext context
    )
    {
        logger.LogDebug("Start API Get Video Thumbnail");

        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos.SingleOrDefaultAsync(
            v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == request.Id,
            context.CancellationToken
        );

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        if (video.Thumb == null)
            throw new RpcException(
                context.Status = new Status(StatusCode.Unavailable, "Not Downloaded")
            );

        await using var file = File.OpenRead(video.Thumb);

        var buffer = new byte[8 * 1024];
        int bytesRead;
        while ((bytesRead = await file.ReadAsync(buffer, context.CancellationToken)) > 0)
        {
            var reply = new GetVideoThumbnailReply()
            {
                Data = ByteString.CopyFrom(buffer, 0, bytesRead),
            };

            await responseStream.WriteAsync(reply, context.CancellationToken);
        }
    }

    public override async Task GetVideo(
        GetVideoRequest request,
        IServerStreamWriter<GetVideoReply> responseStream,
        ServerCallContext context
    )
    {
        logger.LogDebug("Start API Get Video");

        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        await using var db = await contextFactory.CreateDbContextAsync(context.CancellationToken);

        var video = await db.Videos.SingleOrDefaultAsync(
            v => v.Subscription != null && v.Subscription.UserId == user.Id && v.Id == request.Id,
            context.CancellationToken
        );

        if (video == null)
            throw new RpcException(context.Status = new Status(StatusCode.NotFound, "Not Found"));

        if (video.DownloadedPath == null)
            throw new RpcException(
                context.Status = new Status(StatusCode.Unavailable, "Not Downloaded")
            );

        await using var file = File.OpenRead(video.DownloadedPath);

        var buffer = new byte[8 * 1024];
        int bytesRead;
        while ((bytesRead = await file.ReadAsync(buffer, context.CancellationToken)) > 0)
        {
            var reply = new GetVideoReply() { Data = ByteString.CopyFrom(buffer, 0, bytesRead) };

            await responseStream.WriteAsync(reply, context.CancellationToken);
        }
    }

    public override async Task PlaybackSession(
        IAsyncStreamReader<PlaybackSessionRequest> requestStream,
        IServerStreamWriter<PlaybackSessionReply> responseStream,
        ServerCallContext context
    )
    {
        logger.LogDebug("Start API Playback Session");

        var user = await userManager.GetUserAsync(context.GetHttpContext().User);
        ArgumentNullException.ThrowIfNull(user);

        // Wait for initial message
        await requestStream.MoveNext(context.CancellationToken);

        string playerId;
        if (requestStream.Current.HasPlayerId) {
            playerId = requestStream.Current.PlayerId;
            logger.LogDebug("Got player ID {PlayerID}", playerId);
        } else {
            playerId = Guid.NewGuid().ToString();
            logger.LogDebug("Generating new player ID {PlayerID}", playerId);
        }

        // TODO pass some kinda init block to this so listeners only see the ready object?
        using var sessionReference = playbackSessionManager.NewSession(playerId, user);

        if (requestStream.Current.HasTitle)
            sessionReference.Session.Title = requestStream.Current.Title;

        sessionReference.StartPlayingVideo += async (video, positionSeconds) =>
        {
            logger.LogDebug("Sending callback start playing video");

            var reply = new PlaybackSessionReply { NextVideoId = video.Id };
            if (positionSeconds != null)
                reply.PlaybackPosition = positionSeconds.Value;
            await responseStream.WriteAsync(reply, context.CancellationToken);
        };

        sessionReference.PlayPauseEvent += async () =>
        {
            logger.LogDebug("Sending callback play pause event");

            var reply = new PlaybackSessionReply { ShouldPlayPause = true };
            if (sessionReference.Session.Video?.Id != null)
                reply.NextVideoId = sessionReference.Session.Video.Id;

            var p = 0;
            if (sessionReference.Session.CurrentPosition != null)
                p = (int)sessionReference.Session.CurrentPosition.Value.TotalSeconds;
            if (p <= 0)
                p = await sessionReference.Session.PlaybackPositionToRestore() ?? 0;
            if (p > 0)
                reply.PlaybackPosition = p;

            await responseStream.WriteAsync(reply, context.CancellationToken);
        };

        sessionReference.PauseIfPlayingEvent += async () => {
            logger.LogDebug("Sending callback pause if playing");
            await responseStream.WriteAsync(
                new PlaybackSessionReply { ShouldPauseIfPlaying = true },
                context.CancellationToken
            );
        };
        sessionReference.SeekRelativeEvent += async seconds => {
            logger.LogDebug("Sending callback seek relative");
            await responseStream.WriteAsync(
                new PlaybackSessionReply { ShouldSeekRelativeSeconds = seconds },
                context.CancellationToken
            );
        };
        sessionReference.ChangeRateEvent += async (direction) => {
            logger.LogDebug("Sending callback change playback rate");
            await responseStream.WriteAsync(
                new PlaybackSessionReply { ShouldChangeRate = direction ? 1 : -1 },
                context.CancellationToken
            );
        };
        sessionReference.ChangeVolumeEvent += async (direction) => {
            logger.LogDebug("Sending callback change volume");
            await responseStream.WriteAsync(
                new PlaybackSessionReply { ShouldChangeVolume = direction ? 1 : -1 },
                context.CancellationToken
            );
        };
        sessionReference.ToggleSubtitleEvent += async () => {
            logger.LogDebug("Sending callback toggle subtitles");
            await responseStream.WriteAsync(
                new PlaybackSessionReply { ShouldToggleSubtitles = true },
                context.CancellationToken
            );
        };

        // TODO move to PlaybackSession.
        sessionReference.AddVideos += async minutes =>
        {
            logger.LogDebug("Sending callback add videos");

            if (sessionReference.Session.SelectedFolderId == null)
                return;

            var exclude = new List<Video>(sessionReference.Session.Playlist);
            if (sessionReference.Session.Video != null)
                exclude.Add(sessionReference.Session.Video);

            await using var dataContext = await contextFactory.CreateDbContextAsync();
            var videos = await ShuffleHelper.Shuffle(
                dataContext,
                user,
                minutes,
                sessionReference.Session.SelectedFolderId,
                null,
                exclude
            );
            sessionReference.Session.AddToPlaylist(videos);
        };

        while (true)
        {
            logger.LogDebug("Waiting for event");
            context.CancellationToken.ThrowIfCancellationRequested();

            if (await requestStream.MoveNext(context.CancellationToken))
            {
                if (requestStream.Current.HasTitle)
                    sessionReference.Session.Title = requestStream.Current.Title;

                if (requestStream.Current.HasPosition)
                    sessionReference.Session.CurrentPosition =
                        requestStream.Current.Position != null
                            ? TimeSpan.FromSeconds(requestStream.Current.Position)
                            : null;

                if (requestStream.Current.HasLoaded)
                    sessionReference.Session.Loaded = requestStream.Current.Loaded;

                if (requestStream.Current.HasProvider)
                {
                    if (requestStream.Current.Provider != null)
                        sessionReference.Session.Provider = serviceProvider
                            .GetServices<IProvider>()
                            .SingleOrDefault(provider =>
                                provider.ProviderIdentifier == requestStream.Current.Provider
                            )
                            ?.Provider;
                    else
                        sessionReference.Session.Provider = null;
                }

                if (requestStream.Current.HasQuality)
                    sessionReference.Session.Quality = requestStream.Current.Quality;

                if (requestStream.Current.HasRate)
                    sessionReference.Session.Rate = requestStream.Current.Rate;

                if (requestStream.Current.HasState)
                    sessionReference.Session.State = requestStream.Current.State;

                if (requestStream.Current.HasVideoId)
                {
                    if (requestStream.Current.VideoId != null)
                    {
                        await using var db = await contextFactory.CreateDbContextAsync(
                            context.CancellationToken
                        );

                        sessionReference.Session.Video = await db
                            .Videos.Include(static v => v.Subscription)
                            .SingleAsync(
                                v => v.Id == requestStream.Current.VideoId,
                                context.CancellationToken
                            );
                    }
                    else
                    {
                        sessionReference.Session.Video = null;
                    }
                }

                if (requestStream.Current.HasVolume)
                    sessionReference.Session.Volume = requestStream.Current.Volume;

                if (requestStream.Current.HasSupportsRateChange)
                    sessionReference.Session.SupportsRateChange = requestStream.Current.SupportsRateChange;

                if (requestStream.Current.HasSupportsVolumeChange)
                    sessionReference.Session.SupportsVolumeChange = requestStream.Current.SupportsVolumeChange;

                if (requestStream.Current.HasSupportsSubtitles)
                    sessionReference.Session.SupportsSubtitles = requestStream.Current.SupportsSubtitles;

                if (requestStream.Current.HasSubtitles)
                    sessionReference.Session.Subtitles = requestStream.Current.Subtitles;

                if (requestStream.Current.HasBannerMessage)
                    sessionReference.Session.Message = requestStream.Current.BannerMessage.NullIfEmpty();

                // process actions after status updates so that status is up to date for whatever the actiond does.
                switch (requestStream.Current.Action)
                {
                    case PlaybackSessionAction.OnWatchedToEnd:
                        await sessionReference.Session.OnWatchedToEnd(requestStream.Current.VideoId);
                        break;
                }

                if (requestStream.Current.EndSession)
                    return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), context.CancellationToken);
        }
    }
}

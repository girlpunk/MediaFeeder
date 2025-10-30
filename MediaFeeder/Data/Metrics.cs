using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Data;

public sealed class Metrics : IDisposable
{
    internal const string MeterName = "MediaFeeder";
    private readonly Meter _meter;

    private ObservableGauge<int> PublishedGauge { get; }
    private ObservableGauge<int> PublishedDurationGauge { get; }
    private ObservableGauge<int> WatchedRecentlyGauge { get; }
    private ObservableGauge<int> WatchedRecentlyDurationGauge { get; }
    private ObservableGauge<int> SubscriptionsGauge { get; }
    private ObservableGauge<int> VideosGauge { get; }
    private ObservableGauge<int> WatchedGauge { get; }
    private ObservableGauge<int> FoldersGauge { get; }
    private ObservableGauge<int> FolderTrackedGauge { get; }
    private ObservableGauge<int> FolderUnwatchedGauge { get; }
    private ObservableGauge<int> FolderUnwatchedDurationGauge { get; }
    private ObservableGauge<int> FolderWatchedDurationGauge { get; }

    public Metrics(IMeterFactory meterFactory, IDbContextFactory<MediaFeederDataContext> contextFactory)
    {
        _meter = meterFactory.Create(MeterName);

        PublishedGauge = _meter.CreateObservableGauge(
            "videos-published",
            () =>
            {
                var lastHour = DateTime.UtcNow - TimeSpan.FromHours(1);
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Count(video => video.PublishDate >= lastHour));
            },
            "Video");

        PublishedDurationGauge = _meter.CreateObservableGauge(
            "videos-published-duration",
            () =>
            {
                var lastHour = DateTime.UtcNow - TimeSpan.FromHours(1);
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Where(video => video.PublishDate >= lastHour).Sum(video => video.Duration) ?? 0);
            },
            "Seconds");

        WatchedRecentlyGauge = _meter.CreateObservableGauge(
            "watched-recently",
            () =>
            {
                var lastHour = DateTime.UtcNow - TimeSpan.FromHours(1);
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Count(video => video.Watched && video.WatchedDate >= lastHour));
            },
            "Video");

        WatchedRecentlyDurationGauge = _meter.CreateObservableGauge(
            "watched-recently-duration",
            () =>
            {
                var lastHour = DateTime.UtcNow - TimeSpan.FromHours(1);
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Where(video => video.Watched && video.WatchedDate >= lastHour).Sum(video => video.Duration) ?? 0);
            },
            "Seconds");

        SubscriptionsGauge = _meter.CreateObservableGauge(
            "total-subscriptions",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Subscriptions.Count());
            },
            "Subscription");
        VideosGauge = _meter.CreateObservableGauge(
            "total-videos",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Count());
            },
            "Video");
        WatchedGauge = _meter.CreateObservableGauge(
            "watched-videos",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Count(static video => video.Watched));
            },
            "Video");
        FoldersGauge = _meter.CreateObservableGauge(
            "unwatched-videos",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Count(static video => !video.Watched));
            },
            "Video");

        FolderTrackedGauge = _meter.CreateObservableGauge(
            "folders-tracked",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return context.Videos
                    .GroupBy(static video => video.Subscription!.ParentFolderId)
                    .Select(static group => new Measurement<int>(
                        group.Count(),
                        new Dictionary<string, object?>
                        {
                            { "Key", group.Key },
                            { "Name", group.First().Subscription!.ParentFolder!.Name }
                        }))
                    .ToList();
            },
            "Videos");

        FolderUnwatchedGauge = _meter.CreateObservableGauge(
            "folders-unwatched",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return context.Videos
                    .GroupBy(static video => video.Subscription!.ParentFolderId)
                    .Select(static group => new Measurement<int>(
                        group.Count(static video => !video.Watched),
                        new Dictionary<string, object?>
                        {
                            { "Key", group.Key },
                            { "Name", group.First().Subscription!.ParentFolder!.Name }
                        }))
                    .ToList();
            },
            "Videos");

        FolderUnwatchedDurationGauge = _meter.CreateObservableGauge(
            "folders-unwatched-duration",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return context.Videos
                    .GroupBy(static video => video.Subscription!.ParentFolderId)
                    .Select(static group => new Measurement<int>(
                        group.Where(static video => !video.Watched)
                            .Sum(static video => video.Duration ?? 0),
                        new Dictionary<string, object?>
                        {
                            { "Key", group.Key },
                            { "Name", group.First().Subscription!.ParentFolder!.Name }
                        }))
                    .ToList();
            },
            "Seconds");

        FolderWatchedDurationGauge = _meter.CreateObservableGauge(
            "folders-watched-duration",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return context.Videos
                    .GroupBy(static video => video.Subscription!.ParentFolderId)
                    .Select(static group => new Measurement<int>(
                        group.Where(static video => video.Watched && video.WatchedDate != null)
                            .Sum(static video => video.Duration ?? 0),
                        new Dictionary<string, object?>
                        {
                            { "Key", group.Key },
                            { "Name", group.First().Subscription!.ParentFolder!.Name }
                        }))
                    .ToList();
            },
            "Seconds");
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}

using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Data;

public class Metrics
{
    public const string MeterName = "MediaFeeder";

    private ObservableGauge<int> PublishedGauge { get; }
    private ObservableGauge<int> SubscriptionsGauge { get; }
    private ObservableGauge<int> VideosGauge { get; }
    private ObservableGauge<int> WatchedGauge { get; }
    private ObservableGauge<int> FoldersGauge { get; }
    private ObservableGauge<int> FolderTrackedGauge { get; }
    private ObservableGauge<int> FolderUnwatchedGauge { get; }
    private ObservableGauge<int> FolderUnwatchedDurationGauge { get; }

    public Metrics(IMeterFactory meterFactory, IDbContextFactory<MediaFeederDataContext> contextFactory, ILogger<Metrics> logger)
    {
        var meter = meterFactory.Create(MeterName);

        PublishedGauge = meter.CreateObservableGauge(
            "videos-published",
            () =>
            {
                var lastHour = DateTime.UtcNow - TimeSpan.FromHours(1);
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Count(video => video.PublishDate >= lastHour));
            },
            "Video");

        SubscriptionsGauge = meter.CreateObservableGauge(
            "total-subscriptions",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Subscriptions.Count());
            },
            "Subscription");
        VideosGauge = meter.CreateObservableGauge(
            "total-videos",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Count());
            },
            "Video");
        WatchedGauge = meter.CreateObservableGauge(
            "watched-videos",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Count(static video => video.Watched));
            },
            "Video");
        FoldersGauge = meter.CreateObservableGauge(
            "unwatched-videos",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return new Measurement<int>(context.Videos.Count(static video => !video.Watched));
            },
            "Folder");

        FolderTrackedGauge = meter.CreateObservableGauge(
            "folders-tracked",
            () =>
            {
                logger.LogInformation("Started folders-tracked call");

                using var context = contextFactory.CreateDbContext();
                logger.LogInformation("Started folders-tracked call 1");
                var v = context.Videos
                    .GroupBy(static video => video.Subscription.ParentFolderId)
                    .Select(static group => new Measurement<int>(
                        group.Count(),
                        new Dictionary<string, object?>
                        {
                            { "Key", group.Key },
                            { "Name", group.First().Subscription.ParentFolder.Name }
                        }));
                logger.LogInformation("Started folders-tracked call 2");
                return v;
            },
            "Videos");

        FolderUnwatchedGauge = meter.CreateObservableGauge(
            "folders-unwatched",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return context.Videos
                    .GroupBy(static video => video.Subscription.ParentFolderId)
                    .Select(static group => new Measurement<int>(
                        group.Count(static video => !video.Watched),
                        new Dictionary<string, object?>
                        {
                            { "Key", group.Key },
                            { "Name", group.First().Subscription.ParentFolder.Name }
                        }));
            },
            "Videos");

        FolderUnwatchedDurationGauge = meter.CreateObservableGauge(
            "folders-unwatched-duration",
            () =>
            {
                using var context = contextFactory.CreateDbContext();
                return context.Videos
                    .GroupBy(static video => video.Subscription.ParentFolderId)
                    .Select(static group => new Measurement<int>(
                        group.Where(static video => !video.Watched)
                            .Sum(static video => video.Duration),
                        new Dictionary<string, object?>
                        {
                            { "Key", group.Key },
                            { "Name", group.First().Subscription.ParentFolder.Name }
                        }));
            },
            "Seconds");
    }
}

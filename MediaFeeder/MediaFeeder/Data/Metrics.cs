using System.Diagnostics.Metrics;

namespace MediaFeeder.Data;

public class Metrics
{
    private Counter<int> PublishedCounter { get; }
    private UpDownCounter<int> SubscriptionsCounter { get; }
    private Counter<int> VideosCounter { get; }
    private UpDownCounter<int> WatchedCounter { get; }
    private UpDownCounter<int> FoldersCounter { get; }

    public Metrics(IMeterFactory meterFactory, IConfiguration configuration)
    {
        var meter = meterFactory.Create("MediaFeeder");

        PublishedCounter = meter.CreateCounter<int>("videos-published", "Video");
        SubscriptionsCounter = meter.CreateUpDownCounter<int>("total-subscriptions", "Subscription");
        VideosCounter = meter.CreateCounter<int>("total-videos", "Video");
        WatchedCounter = meter.CreateUpDownCounter<int>("watched-videos", "Video");
        FoldersCounter = meter.CreateUpDownCounter<int>("total-folders", "Folder");
    }
}

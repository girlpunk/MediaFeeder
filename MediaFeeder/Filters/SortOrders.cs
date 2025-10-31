using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;

namespace MediaFeeder.Filters;

public enum SortOrders
{
    Newest,
    Oldest,
    PlaylistOrder,
    ReversePlaylistOrder,
    Popularity,
    TopRated,
    Longest,
    Shortest,
    WatchedRecently,
    WatchedHistorically,
    StaredRecently,
    StaredHistorically
}

public static class VideoExtensions
{
    public static IQueryable<Video> SortVideos(this IQueryable<Video> source, SortOrders sortOrder) =>
        sortOrder switch
        {
            SortOrders.Oldest => source.OrderBy(static v => v.PublishDate),
            SortOrders.Newest => source.OrderByDescending(static v => v.PublishDate),
            SortOrders.PlaylistOrder => source.OrderBy(static v => v.PlaylistIndex),
            SortOrders.ReversePlaylistOrder => source.OrderByDescending(static v => v.PlaylistIndex),
            SortOrders.Popularity => source.OrderByDescending(static v => v.Views),
            SortOrders.TopRated => source.OrderByDescending(static v => v.Rating),
            SortOrders.Longest => source.OrderByDescending(static v => v.Duration),
            SortOrders.Shortest => source.OrderBy(static v => v.Duration),
            SortOrders.WatchedRecently => source.Where(static v => v.WatchedDate != null).OrderByDescending(static v => v.WatchedDate),
            SortOrders.WatchedHistorically => source.Where(static v => v.WatchedDate != null).OrderBy(static v => v.WatchedDate),
            SortOrders.StaredRecently => source.Where(static v => v.Star).OrderByDescending(static v => v.StarDate),
            SortOrders.StaredHistorically => source.Where(static v => v.Star).OrderBy(static v => v.StarDate),
            _ => source
        };

    public static IQueryable<Video> SortVideos(this IQueryable<Video> source, DownloadOrder? downloadOrder) =>
        downloadOrder switch
        {
            DownloadOrder.Oldest => source.OrderBy(static v => v.PublishDate),
            DownloadOrder.Newest => source.OrderByDescending(static v => v.PublishDate),
            DownloadOrder.PlaylistOrder => source.OrderBy(static v => v.PlaylistIndex),
            DownloadOrder.ReversePlaylistOrder => source.OrderByDescending(static v => v.PlaylistIndex),
            DownloadOrder.Popularity => source.OrderByDescending(static v => v.Views),
            DownloadOrder.TopRated => source.OrderByDescending(static v => v.Rating),
            _ => source
        };
}

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
    TopRated
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

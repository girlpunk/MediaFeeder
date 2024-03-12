using Humanizer;
using MediaFeeder.Data;
using MediaFeeder.Models;
using MediaFeeder.Models.db;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Pages;

public class IndexModel : PageModel
{
    private readonly MediaFeederDataContext _context;

    public IList<YtManagerAppVideo> Videos { get; private set; } = default!;

    private string? Search { get; set; }
    private SortOrder Sort { get; set; } = SortOrder.Oldest;
    private ShowOnlyWatched ShowWatched { get; set; } = ShowOnlyWatched.NotWatched;
    private ShowOnlyDownloaded ShowDownloaded { get; set; } = ShowOnlyDownloaded.All;
    private int ResultsPerPage { get; set; } = 50;
    private int PageNumber { get; set; } = 1;
    public string TotalDuration { get; private set; } = "";

    [Parameter]
    public int? FolderId { get; set; }

    [Parameter]
    public int? SubscriptionId { get; set; }

    public IndexModel(MediaFeederDataContext context)
    {
        _context = context;
    }

    private IQueryable<YtManagerAppVideo> SelectedVideos
    {
        get
        {
            if (FolderId != null)
            {
                return _context.YtManagerAppVideos.Where(v => v.Subscription.ParentFolderId == FolderId);
            }

            if (SubscriptionId != null)
            {
                return _context.YtManagerAppVideos.Where(v => v.SubscriptionId == SubscriptionId);
            }

            return _context.YtManagerAppVideos;
        }
    }

    private IOrderedQueryable<YtManagerAppVideo> FilteredVideos
    {
        get
        {
            var result = SelectedVideos;

            result = ShowWatched switch
            {
                ShowOnlyWatched.Watched => result.Where(static v => v.Watched == true),
                ShowOnlyWatched.NotWatched => result.Where(static v => v.Watched == false),
                _ => result
            };

            result = ShowDownloaded switch
            {
                ShowOnlyDownloaded.Downloaded => result.Where(static v => v.DownloadedPath != null),
                ShowOnlyDownloaded.NotDownloaded => result.Where(static v => v.DownloadedPath == null),
                _ => result
            };

            if (!string.IsNullOrWhiteSpace(Search))
            {
                result = result.Where(v => v.Name.Contains(Search) || v.Description.Contains(Search));
            }

            return Sort switch
            {
                SortOrder.Newest => result.OrderByDescending(static v => v.PublishDate),
                SortOrder.Oldest => result.OrderBy(static v => v.PublishDate),
                SortOrder.Playlist => result.OrderByDescending(static v => v.PlaylistIndex),
                SortOrder.ReversePlaylist => result.OrderBy(static v => v.PlaylistIndex),
                SortOrder.Popularity => result.OrderByDescending(static v => v.Views),
                SortOrder.TopRated => result.OrderByDescending(static v => v.Rating),
                _ => result.OrderBy(static v => v.Id)
            };
        }
    }

    private IQueryable<YtManagerAppVideo> PagedVideos => FilteredVideos.Skip(ResultsPerPage * (PageNumber - 1)).Take(ResultsPerPage);

    public async Task OnGetAsync()
    {
        Videos = await PagedVideos.ToListAsync();
        TotalDuration = (await FilteredVideos.SumAsync(static v => v.Duration)).Seconds().Humanize();
    }
}
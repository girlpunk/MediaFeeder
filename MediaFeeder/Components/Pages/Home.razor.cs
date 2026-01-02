using AntDesign;
using Humanizer;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Filters;
using MediaFeeder.PlaybackManager;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Pages;

public sealed partial class Home
{
    [Parameter] public int? FolderId { get; set; }
    [Parameter] public int? SubscriptionId { get; set; }

    private List<Data.db.Video>? Videos { get; set; }

    [Inject] public required IDbContextFactory<MediaFeederDataContext> ContextFactory { get; set; }
    [Inject] public NavigationManager NavigationManager { get; init; } = null!;
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; } = null!;

    [Inject] public required UserManager<AuthUser> UserManager { get; set; }
    [Inject] public PlaybackSessionManager? SessionManager { get; set; }
    [Inject] public required MessageService MessageService { get; set; }

    public bool isMobile;
    public bool menuDrawOpen = false;

    private string? SearchValue { get; set; } = string.Empty;
    private SortOrders SortOrder { get; set; } = SortOrders.Oldest;
    private VideosShowOnly ShowFilters { get; set; } = VideosShowOnly.NotWatched;
    private int ResultsPerPage { get; set; } = 50;
    private int PageNumber { get; set; } = 1;
    private int ItemsAvailable { get; set; }
    private TimeSpan Duration { get; set; }
    private PlaybackSession? SelectedSession { get; set; }
    private bool VideoOnClickPreventDefault;
    private string Title { get; set; } = "MediaFeeder";
    private int FilterHash { get; set; }

    private SemaphoreSlim Updating { get; } = new(1);

    private bool UpdateHash()
    {
        var newHash = $"{FolderId}{SubscriptionId}{SortOrder}{ShowFilters.Humanize()}{SearchValue}".GetHashCode();

        if (newHash == FilterHash)
            return false;

        FilterHash = newHash;
        return true;
    }

    private async Task OnSortChange()
    {
        if (SortOrder is SortOrders.WatchedRecently or SortOrders.WatchedHistorically)
            ShowFilters |= VideosShowOnly.Watched;

         await OnParametersSetAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        await Update();
    }

    void HandleBreakpoint(BreakpointType breakpoint)
    {
        isMobile = breakpoint.IsIn(BreakpointType.Sm, BreakpointType.Xs, BreakpointType.Md);
    }

    void toggleMenuDraw()
    {
        this.menuDrawOpen = !this.menuDrawOpen;
    }

    void closeMenuDraw()
    {
        this.menuDrawOpen = false;
    }

    private async Task Update(bool force = false)
    {
        // if (force || !UpdateHash())
        //     return;

        closeMenuDraw();

        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);

        ArgumentNullException.ThrowIfNull(user);

        try
        {
            await Updating.WaitAsync();

            Videos = null;
            StateHasChanged();

            await using var context = await ContextFactory.CreateDbContextAsync();
            var source = context.Videos
                .AsQueryable()
                .Where(v => v.Subscription!.UserId == user.Id);

            if (FolderId != null)
            {
                var subfolderIds = await Folder.RecursiveFolderIds(context, FolderId.Value, user.Id);
                source = source.Where(v => subfolderIds.Contains(v.Subscription!.ParentFolderId));
                Title = (await context.Folders.FindAsync(FolderId))?.Name ?? "";
            }

            if (SubscriptionId != null)
            {
                source = source.Where(v => v.SubscriptionId == SubscriptionId);
                Title = (await context.Subscriptions.FindAsync(SubscriptionId))?.Name ?? "";
            }

            source = source.SortVideos(SortOrder);

            if (ShowFilters.HasFlag(VideosShowOnly.Watched) ^ ShowFilters.HasFlag(VideosShowOnly.NotWatched))
            {
                if (ShowFilters.HasFlag(VideosShowOnly.Watched)) source = source.Where(v => v.Watched);
                if (ShowFilters.HasFlag(VideosShowOnly.NotWatched)) source = source.Where(v => !v.Watched);
            }

            if (ShowFilters.HasFlag(VideosShowOnly.Downloaded) ^ ShowFilters.HasFlag(VideosShowOnly.NotDownloaded))
            {
                if (ShowFilters.HasFlag(VideosShowOnly.Downloaded)) source = source.Where(v => !string.IsNullOrWhiteSpace(v.DownloadedPath));
                if (ShowFilters.HasFlag(VideosShowOnly.NotDownloaded)) source = source.Where(v => string.IsNullOrWhiteSpace(v.DownloadedPath));
            }

            if (!string.IsNullOrWhiteSpace(SearchValue))
            {
                if (SearchValue.StartsWith("!"))
                {
                    var pattern = $"%{EscapeSearch(SearchValue[1..].Trim())}%";
                    source = source.Where(v =>
                        !EF.Functions.ILike(v.Name, pattern, "\\")
                        && !EF.Functions.ILike(v.Description, pattern, "\\")
                        && !v.Tags.Any(t => EF.Functions.ILike(t.Tag, pattern, "\\")));
                }
                else
                {
                    var pattern = $"%{EscapeSearch(SearchValue.Trim())}%";
                    source = source.Where(v =>
                        EF.Functions.ILike(v.Name, pattern, "\\")
                        || EF.Functions.ILike(v.Description, pattern, "\\")
                        || v.Tags.Any(t => EF.Functions.ILike(t.Tag, pattern, "\\")));
                }
            }

            ItemsAvailable = await source.CountAsync();
            Duration = TimeSpan.FromSeconds(await source.SumAsync(static v => v.Duration ?? 0));
            Videos = await source
                .Skip((PageNumber - 1) * ResultsPerPage)
                .Take(ResultsPerPage)
                .Include(static v => v.Subscription)
                .ToListAsync();
        }
        finally
        {
            Updating.Release();
        }
    }

    // TODO put this somewhere better?
    private String EscapeSearch(String term)
    {
        return term.Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("*", "%");
    }

    private Task PageChange(PaginationEventArgs paginationEventArgs)
    {
        PageNumber = paginationEventArgs.Page;
        ResultsPerPage = paginationEventArgs.PageSize;

        return Update();
    }

    private async Task OnSelectedSessionChanged()
    {
        VideoOnClickPreventDefault = SelectedSession != null;
    }

    private async Task OnVideoClick(Data.db.Video video)
    {
        if (SelectedSession == null)
            return;

        if (SelectedSession.AddToPlaylistIfNotPresent(video))
            await MessageService.SuccessAsync("Added to session playlist");
        else
            await MessageService.SuccessAsync("Already in session playlist");
    }

    private void Shuffle()
    {
        ArgumentNullException.ThrowIfNull(NavigationManager);
        var page = NavigationManager.Uri;

        if (page.EndsWith('/'))
            page += "shuffle";
        else
            page += "/shuffle";

        NavigationManager.NavigateTo(page);
    }

    private async Task MarkAllWatched()
    {
        ArgumentNullException.ThrowIfNull(Videos);

        await using var context = await ContextFactory.CreateDbContextAsync();
        foreach (var video in Videos) {
            context.Attach(video);
            video.Watched = true;
        }

        await context.SaveChangesAsync();
        await Update(true);
    }

    private void WatchAll()
    {
        ArgumentNullException.ThrowIfNull(Videos);
        ArgumentNullException.ThrowIfNull(NavigationManager);

        var videos = new Queue<MediaFeeder.Data.db.Video>(Videos);

        var url = $"video/{videos.Dequeue().Id}/{string.Join(',', videos.Select(static v => v.Id))}";
        NavigationManager.NavigateTo(url);
    }

    private string ShortDurationFormated()
    {
        var ret = "";
        if (Duration.Days > 0) ret += $"{Duration.Days}d";
        if (Duration.Hours > 0) ret += $" {Duration.Hours}h";
        if (Duration.Minutes > 0) ret += $" {Duration.Minutes}m";
        if (ret.Length < 1) ret = "0";
        return ret;
    }
}

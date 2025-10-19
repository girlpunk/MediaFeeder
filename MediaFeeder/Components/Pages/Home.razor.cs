using AntDesign;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Filters;
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

    public bool isMobile;
    public bool menuDrawOpen = false;

    private string? SearchValue { get; set; } = string.Empty;
    private SortOrders SortOrder { get; set; } = SortOrders.Oldest;
    private bool? ShowWatched { get; set; } = false;
    private bool? ShowDownloaded { get; set; }
    private int ResultsPerPage { get; set; } = 50;
    private int PageNumber { get; set; } = 1;
    private int ItemsAvailable { get; set; }
    private TimeSpan Duration { get; set; }
    private string Title { get; set; } = "MediaFeeder";
    private int FilterHash { get; set; }

    private SemaphoreSlim Updating { get; } = new(1);

    private bool UpdateHash()
    {
        var newHash = $"{FolderId}{SubscriptionId}{SortOrder}{ShowWatched}{ShowDownloaded}{SearchValue}".GetHashCode();

        if (newHash == FilterHash)
            return false;

        FilterHash = newHash;
        return true;
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
                source = source.Where(v => v.Subscription!.ParentFolderId == FolderId);
                Title = (await context.Folders.FindAsync(FolderId))?.Name ?? "";
            }

            if (SubscriptionId != null)
            {
                source = source.Where(v => v.SubscriptionId == SubscriptionId);
                Title = (await context.Subscriptions.FindAsync(SubscriptionId))?.Name ?? "";
            }

            source = source.SortVideos(SortOrder);

            if (ShowWatched != null)
                source = source.Where(v => v.Watched == ShowWatched);

            if (ShowDownloaded != null)
                source = source.Where(v => string.IsNullOrWhiteSpace(v.DownloadedPath) != ShowDownloaded);

            if (!string.IsNullOrWhiteSpace(SearchValue))
                source = source.Where(v => v.Name.Contains(SearchValue) || v.Description.Contains(SearchValue));

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

    private Task PageChange(PaginationEventArgs paginationEventArgs)
    {
        PageNumber = paginationEventArgs.Page;
        ResultsPerPage = paginationEventArgs.PageSize;

        return Update();
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
}

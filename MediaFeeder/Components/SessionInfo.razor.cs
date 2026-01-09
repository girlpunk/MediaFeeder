using AntDesign;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.PlaybackManager;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components;

public sealed partial class SessionInfo
{
    [Parameter] [EditorRequired] public PlaybackSession? Session { get; set; }

    [Inject] public required IDbContextFactory<MediaFeederDataContext> ContextFactory { get; set; }
    [Inject] public required MediaFeederDataContext Context { get; set; }
    [Inject] public required IMessageService MessageService { get; set; }
    [Inject] public required IServiceProvider ServiceProvider { get; set; }
    public bool isMobile;
    private List<Folder> _allFolders = [];
    private readonly SemaphoreSlim _loading = new(1);

    protected override async Task OnParametersSetAsync()
    {
        if (Session != null && !_allFolders.Any())
        {
            await _loading.WaitAsync();

            try
            {
                _allFolders = await Context.Folders
                    .AsNoTracking()
                    .Where(f => f.User == Session.User)
                    .Include(static f => f.Subfolders)
                    .Select(Folder.GetProjection(5))
                    .Where(static f => f.ParentId == null)
                    .OrderBy(static f => f.Name)
                    .ToListAsync();
            }
            finally
            {
                _loading.Release();
            }
        }

        if (Session != null)
            Session.UpdateEvent += () => InvokeAsync(StateHasChanged);
    }

    private async Task ToggleStar()
    {
        if (Session?.Video == null) return;

        await using var context = await ContextFactory.CreateDbContextAsync();
        context.Attach(Session.Video);
        Session.Video.Star = !Session.Video.Star;
        Session.Video.StarDate = DateTimeOffset.Now;
        await context.SaveChangesAsync();

        await MessageService.SuccessAsync($"Star {(Session.Video.Star ? "Added" : "Removed")}");
        StateHasChanged();
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (Session is { SelectedFolderId: null } && _allFolders.Any())
        {
            Session.SelectedFolderId = _allFolders.First().Id;
            StateHasChanged();
        }

        return base.OnAfterRenderAsync(firstRender);
    }

    private string? GetProviderUrl()
    {
        if (Session?.Video == null) return null;

        var provider = ServiceProvider.GetServices<IProvider>()
            .Single(provider => provider.ProviderIdentifier == Session.Video.Subscription!.Provider);

        // TODO ideally this would be in PlaybackSession.PlayNextInPlaylist, but issue with:
        // Cannot resolve scoped service 'System.Collections.Generic.IEnumerable`1[MediaFeeder.IProvider]' from root provider.
        Session.Provider ??= provider.Provider;

        return provider.GetUrl(Session.Video);
    }

    private TimeSpan Remaining()
    {
        if (Session == null)
            return TimeSpan.Zero;

        var remaining = TimeSpan.FromSeconds(Session.Playlist.Sum(static v => v.Duration ?? 0));

        if (Session.CurrentPosition != null && Session.Video?.DurationSpan != null)
            remaining += (TimeSpan) (Session.Video.DurationSpan - Session.CurrentPosition);

        return remaining;
    }

    void HandleBreakpoint(BreakpointType breakpoint)
    {
        isMobile = breakpoint.IsIn(BreakpointType.Sm, BreakpointType.Xs, BreakpointType.Md, BreakpointType.Lg);
    }
}
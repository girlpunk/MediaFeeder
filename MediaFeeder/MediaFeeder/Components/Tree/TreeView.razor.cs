using AntDesign;
using MediaFeeder.Components.Dialogs;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Tree;

public sealed partial class TreeView
{
    [Inject]
    public required IDbContextFactory<MediaFeederDataContext> ContextFactory { get; set; }

    public Dictionary<int, (int unwatched, int downloaded)>? UnwatchedCache { get; set; }

    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; }

    [Inject] public required UserManager<AuthUser> UserManager { get; set; }

    [Inject] public required ModalService ModalService { get; set; }
    [Inject] public required ILogger<TreeView> Logger { get; set; }

    [Parameter]
    [EditorRequired]
    public int? SelectedFolder { get; set; }

    [Parameter]
    [EditorRequired]
    public int? SelectedSubscription { get; set; }

    private List<Folder>? Folders { get; set; }

    // private SemaphoreSlim Updating { get; } = new(1);
    // private Tree<ITreeSelectable>? TreeRef { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || UnwatchedCache == null)
            await UpdateTree();

        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task UpdateTree()
    {
        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);
        ArgumentNullException.ThrowIfNull(user);

        await using var context = await ContextFactory.CreateDbContextAsync();

        UnwatchedCache = await context.Videos
            .TagWith("TreeView Unwatched & Downloaded")
            .Where(v => v.Subscription!.UserId == user.Id)
            .GroupBy(static v => v.SubscriptionId, static (k, g) => new
            {
                key = k,
                unwatched = g.Count(static v => !v.Watched),
                downloaded = g.Count(static v => v.IsDownloaded)
            })
            .ToDictionaryAsync(static g => g.key, static g => (g.unwatched, g.downloaded));

        Folders = await context.Folders
            .TagWith("TreeView Folders")
            .Where(f => f.UserId == user.Id && f.ParentId == null)
            .Include(static f => f.Subfolders)
            .Include(static f => f.Subscriptions)
            .ToListAsync();

        await InvokeAsync(StateHasChanged);
    }

    private void EditFolder(int? folder)
    {
        var modalConfig = new ModalOptions
        {
            AfterClose = async () => await UpdateTree(),
            Title = folder != null
                ? $"Edit Folder"
                : "Create Folder",
            DestroyOnClose = true,
        };

        ModalService.CreateModal<EditFolder, int?>(modalConfig, folder);
    }

    private void EditSubscription(int? subscription)
    {
        var modalConfig = new ModalOptions
        {
            Title = subscription != null
                ? $"Edit Subscription"
                : "Create Subscription",
            AfterClose = async () => await UpdateTree(),
            DestroyOnClose = true,
        };

        ModalService.CreateModal<EditSubscription, int?>(modalConfig, subscription);
    }

    private void EditSelected()
    {
        if (SelectedFolder != null)
        {
            Logger.LogInformation("Opening EditFolder for {}", SelectedFolder);
            EditFolder(SelectedFolder);
        }
        else if (SelectedSubscription != null)
        {
            Logger.LogInformation("Opening EditSubscription for {}", SelectedSubscription);
            EditSubscription(SelectedSubscription);
        }
        else
        {
            Logger.LogWarning("Edit clicked when both SelectedFolder and SelectedSubscription are null");
        }
    }
}

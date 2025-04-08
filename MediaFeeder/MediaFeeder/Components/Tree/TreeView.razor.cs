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

    [Inject] public required ILogger<TreeView> Logger { get; set; }

    public Dictionary<int, (int unwatched, int downloaded)>? UnwatchedCache { get; set; }

    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; }

    [Inject] public required UserManager<AuthUser> UserManager { get; set; }

    [Inject] public required ModalService ModalService { get; set; }


    private List<Folder>? Folders { get; set; }

    private SemaphoreSlim Updating { get; } = new(1);
    private Tree<ITreeSelectable>? TreeRef { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || UnwatchedCache == null)
        {
            Logger.LogInformation("Got lock, checking auth");
            var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(auth.User);

            ArgumentNullException.ThrowIfNull(user);

            Logger.LogInformation("Preparing queries");
            await using var context = await ContextFactory.CreateDbContextAsync();

            Logger.LogInformation("Staring unwatched");
            UnwatchedCache = await context.Videos
                .TagWith("TreeView Unwatched")
                .Where(v => v.Subscription!.UserId == user.Id)
                .GroupBy(static v => v.SubscriptionId, static (k, g) => new
                {
                    key = k,
                    unwatched = g.Count(static v => !v.Watched),
                    downloaded = g.Count(static v => v.IsDownloaded)
                })
                .ToDictionaryAsync(static g => g.key, static g => (unwatched: g.unwatched, downloaded: g.downloaded));

            Logger.LogInformation("merge done, getting folders");
            Folders = await context.Folders
                .TagWith("TreeView Folders")
                .Where(f => f.UserId == user.Id && f.ParentId == null)
                .Include(static f => f.Subfolders)
                .Include(static f => f.Subscriptions)
                .ToListAsync();

            // Logger.LogInformation("Filtering fonder");
            // adding this filter to above query fails with an error about lazy-load after the DbContext was disposed.
            // Folders = Folders.Where(static f => f.ParentId == null).ToList();

            Logger.LogInformation("Done!");
            await InvokeAsync(StateHasChanged);
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private void EditFolder(Folder? folder)
    {
        var modalConfig = new ModalOptions
        {
            AfterClose = () =>
            {
                Console.WriteLine("AfterClose");
                InvokeAsync(StateHasChanged);
                return Task.CompletedTask;
            },
            Title = folder != null
                ? $"Edit Folder {folder.Name}"
                : "Create Folder"
        };

        ModalService.CreateModal<EditFolder, int?>(modalConfig, folder?.Id);
    }

    private void EditSubscription(Subscription? subscription)
    {
        var modalConfig = new ModalOptions();
        ModalRef? modalRef = null;

        modalConfig.Title = "Basic Form";
        modalConfig.OnCancel = async _ =>
        {
            Console.WriteLine("OnCancel");
            await modalRef?.CloseAsync();
        };

        modalConfig.AfterClose = () =>
        {
            Console.WriteLine("AfterClose");
            InvokeAsync(StateHasChanged);
            return Task.CompletedTask;
        };

        modalRef = ModalService.CreateModal<EditSubscription, Subscription?>(modalConfig, subscription);

        modalRef.OnOpen = static () =>
        {
            Console.WriteLine("ModalRef OnOpen");
            return Task.CompletedTask;
        };

        modalRef.OnOk = static () =>
        {
            Console.WriteLine("ModalRef OnOk");
            return Task.CompletedTask;
        };

        modalRef.OnCancel = static () =>
        {
            Console.WriteLine("ModalRef OnCancel");
            return Task.CompletedTask;
        };

        modalRef.OnClose = static () =>
        {
            Console.WriteLine("ModalRef OnClose");
            return Task.CompletedTask;
        };
    }

    private void EditSelected()
    {
        switch (TreeRef?.SelectedData)
        {
            case Folder folder:
                EditFolder(folder);
                break;
            case Subscription subscription:
                EditSubscription(subscription);
                break;
        }
    }
}

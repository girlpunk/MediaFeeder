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


    private List<Folder>? Folders { get; set; }

    private SemaphoreSlim Updating { get; } = new(1);
    private Tree<ITreeSelectable>? TreeRef { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (UnwatchedCache == null && ContextFactory != null && Folders == null)
            try
            {
                await Updating.WaitAsync();

                var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                var user = await UserManager.GetUserAsync(auth.User);

                ArgumentNullException.ThrowIfNull(user);

                await using var context = await ContextFactory.CreateDbContextAsync();
                UnwatchedCache = context.Videos
                    .Where(v => !v.Watched && v.Subscription!.UserId == user.Id)
                    .GroupBy(static v => v.SubscriptionId)
                    .Select(static g => new
                    {
                        Id = g.Key,
                        Unwatched = g.Count(),
                    })
                    .ToDictionary(static g => g.Id, static g => (unwatched: g.Unwatched, downloaded: 0));

                Folders = context.Folders.Where(f => f.UserId == user.Id)
                    .Include(static f => f.Subfolders)
                    .Include(static f => f.Subscriptions)
                    .ToList();
            }
            finally
            {
                Updating.Release();
            }

        await base.OnParametersSetAsync();
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
                ? $"Edit Folder {folder?.Name}"
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

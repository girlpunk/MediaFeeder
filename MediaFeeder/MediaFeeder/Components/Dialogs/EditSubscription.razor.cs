using AntDesign;
using FluentValidation;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Dialogs;

public sealed partial class EditSubscription
{
    [Inject] public required MediaFeederDataContext Context { get; set; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; }
    [Inject] public required UserManager<AuthUser> UserManager { get; set; }
    private List<Folder> ExistingFolders { get; set; } = [];
    public required Form<SubscriptionForm> Form { get; set; }
    private SubscriptionForm? Subscription { get; set; }
    [Inject] public required ILogger<EditSubscription> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);

        ArgumentNullException.ThrowIfNull(user);

        if (Options == null)
        {
            Subscription = new SubscriptionForm();
        }
        else
        {
            Logger.LogInformation("Finding existing subscription");
            var subscription = await Context.Subscriptions.SingleAsync(f => f.Id == Options && f.UserId == user.Id);

            Subscription = new SubscriptionForm
            {
                AutoDownload = subscription.AutoDownload,
                AutomaticallyDeleteWatched = subscription.AutomaticallyDeleteWatched,
                ChannelId = subscription.ChannelId,
                ChannelName = subscription.ChannelName,
                DownloadLimit = subscription.DownloadLimit,
                DownloadOrder = subscription.DownloadOrder,
                Name = subscription.Name,
                ParentFolderId = subscription.ParentFolderId,
                PlaylistId = subscription.PlaylistId,
                Provider = subscription.Provider,
                RewritePlaylistIndices = subscription.RewritePlaylistIndices,
                DisableSync = subscription.DisableSync,
            };
        }

        ExistingFolders = await Context.Folders
            .Where(f => f.User == user)
            .Include(static f => f.Subfolders)
            .ToListAsync();

        await base.OnInitializedAsync();
    }

    /// <summary>
    /// when form is submitted, close the modal
    /// </summary>
    private async Task OnFinish(EditContext editContext)
    {
        ArgumentNullException.ThrowIfNull(Subscription);

        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);

        ArgumentNullException.ThrowIfNull(user);

        if (Options == null)
        {
            var subscription = new Subscription
            {
                AutoDownload = Subscription.AutoDownload,
                AutomaticallyDeleteWatched = Subscription.AutomaticallyDeleteWatched,
                ChannelId = Subscription.ChannelId,
                ChannelName = Subscription.ChannelName,
                DownloadLimit = Subscription.DownloadLimit,
                DownloadOrder = Subscription.DownloadOrder,
                Name = Subscription.Name,
                ParentFolderId = Subscription.ParentFolderId,
                PlaylistId = Subscription.PlaylistId,
                Provider = Subscription.Provider ?? throw new InvalidOperationException(),
                RewritePlaylistIndices = Subscription.RewritePlaylistIndices,
                Description = "",
                UserId = user.Id,
            };

            Context.Subscriptions.Add(subscription);
        }
        else
        {
            var subscription = Context.Subscriptions.Single(f => f.Id == Options && f.UserId == user.Id);

            subscription.AutoDownload = Subscription.AutoDownload;
            subscription.AutomaticallyDeleteWatched = Subscription.AutomaticallyDeleteWatched;
            subscription.ChannelId = Subscription.ChannelId;
            subscription.ChannelName = Subscription.ChannelName;
            subscription.DownloadLimit = Subscription.DownloadLimit;
            subscription.DownloadOrder = Subscription.DownloadOrder;
            subscription.Name = Subscription.Name;
            subscription.ParentFolderId = Subscription.ParentFolderId;
            subscription.PlaylistId = Subscription.PlaylistId;
            subscription.Provider = Subscription.Provider ?? throw new InvalidOperationException();
            subscription.RewritePlaylistIndices = Subscription.RewritePlaylistIndices;
            subscription.DisableSync = Subscription.DisableSync;
        }

        await Context.SaveChangesAsync();
        await FeedbackRef.CloseAsync();
    }

    public override Task OnFeedbackOkAsync(ModalClosingEventArgs args)
    {
        Form.Submit();
        args.Cancel = true;
        return Task.CompletedTask;
    }

    public class Validator : AbstractValidator<SubscriptionForm>
    {
        public Validator()
        {
            RuleFor(static f => f.Name).NotEmpty();
            RuleFor(static f => f.ParentFolderId).NotEqual(0);
            RuleFor(static f => f.PlaylistId).NotEmpty();
            RuleFor(static f => f.ChannelId).NotEmpty();
            RuleFor(static f => f.ChannelName).NotEmpty();
            RuleFor(static f => f.Provider).NotEmpty();
        }
    }

    public class SubscriptionForm
    {
        public string Name { get; set; } = "";
        public int ParentFolderId { get; set; }
        public string PlaylistId { get; set; } = "";
        public string ChannelId { get; set; } = "";
        public string ChannelName { get; set; } = "";
        public bool AutoDownload { get; set; }
        public int? DownloadLimit { get; set; }
        public DownloadOrder? DownloadOrder { get; set; }
        public bool AutomaticallyDeleteWatched { get; set; }
        public bool RewritePlaylistIndices { get; set; }
        public string? Provider { get; set; }
        public bool DisableSync { get; set; }
    }
}

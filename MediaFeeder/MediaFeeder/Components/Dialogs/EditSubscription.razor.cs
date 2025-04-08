using AntDesign;
using FluentValidation;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Components.Dialogs;

public partial class EditSubscription
{
    [Inject] public required MediaFeederDataContext Context { get; set; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; }
    [Inject] public required UserManager<AuthUser> UserManager { get; set; }
    private List<Folder> ExistingFolders { get; set; } = [];
    public required Form<Subscription> Form { get; set; }
    private Subscription? Subscription { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (Subscription == null)
        {
            var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(auth.User);

            ArgumentNullException.ThrowIfNull(user);

            if (Options == null)
            {
                Subscription = new Subscription
                {
                    Name = null!,
                    PlaylistId = null!,
                    Description = "",
                    ParentFolderId = 0,
                    UserId = user.Id,
                    ChannelId = null!,
                    ChannelName = null!,
                    Provider = null!
                };
                Context.Subscriptions.Add(Subscription);
            }
            else
            {
                Subscription = Context.Subscriptions.Single(f => f.Id == Options && f.UserId == user.Id);
            }

            ExistingFolders = await Context.Folders
                .Where(f => f.User == user)
                .Include(static f => f.Subfolders)
                .ToListAsync();
        }

        await base.OnParametersSetAsync();
    }

    /// <summary>
    /// when form is submitted, close the modal
    /// </summary>
    private async Task OnFinish(EditContext editContext)
    {
        await Context.SaveChangesAsync();
        await FeedbackRef.CloseAsync();
    }

    public override Task OnFeedbackOkAsync(ModalClosingEventArgs args)
    {
        Form.Submit();
        args.Cancel = true;
        return Task.CompletedTask;
    }

    public class Validator : AbstractValidator<Subscription>
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
}
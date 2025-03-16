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
    private List<Folder> ExistingFolders { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);

        ArgumentNullException.ThrowIfNull(user);

        if (Options == null)
        {
            Options = Context.Subscriptions.CreateProxy();

            Options.User = user;
        }
        else
        {
            // Context.Attach(Options);
        }

        ExistingFolders = await Context.Folders
            .Where(f => f.User == user)
            .Include(static f => f.Subfolders)
            .ToListAsync();

        await base.OnInitializedAsync();
    }

    private void OnFinish(EditContext editContext)
    {
        Context.SaveChanges();

        _ = FeedbackRef.CloseAsync();
    }
}
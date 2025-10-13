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

public partial class EditFolder
{
    [Inject] public required MediaFeederDataContext Context { get; set; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; }
    [Inject] public required UserManager<AuthUser> UserManager { get; set; }
    private List<Folder> ExistingFolders { get; set; } = [];
    public required Form<Folder> Form { get; set; }
    private Folder? Folder { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(auth.User);

        ArgumentNullException.ThrowIfNull(user);

        if (Options == null)
        {
            Folder = new Folder
            {
                Name = "",
                UserId = user.Id
            };
            Context.Folders.Add(Folder);
        }
        else
        {
            Folder = Context.Folders.Single(f => f.Id == Options && f.UserId == user.Id);
        }

        ExistingFolders = await Context.Folders
            .Where(f => f != Folder)
            .Include(static f => f.Subfolders)
            .Where(static f => f.ParentId == null)
            .ToListAsync();

        await base.OnInitializedAsync();
    }

    /// <summary>
    /// when form is submited, close the modal
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

    public class Validator : AbstractValidator<Folder>
    {
        public Validator()
        {
            RuleFor(static f => f.Name).NotEmpty();
        }
    }
}

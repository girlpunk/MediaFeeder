using Microsoft.AspNetCore.Identity;
using MediaFeeder.Data;
using MediaFeeder.Data.db;

namespace MediaFeeder.Components.Account;

internal sealed class IdentityUserAccessor(
    UserManager<AuthUser> userManager,
    IdentityRedirectManager redirectManager)
{
    public async Task<AuthUser> GetRequiredUserAsync(HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);

        if (user is null)
        {
            redirectManager.RedirectToWithStatus("Account/InvalidUser",
                $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.", context);
        }

        return user;
    }
}
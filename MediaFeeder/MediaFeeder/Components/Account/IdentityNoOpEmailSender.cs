using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace MediaFeeder.Components.Account;

// Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
internal sealed class IdentityNoOpEmailSender : IEmailSender<AuthUser>
{
    private readonly IEmailSender _emailSender = new NoOpEmailSender();

    public Task SendConfirmationLinkAsync(AuthUser user, string email, string confirmationLink)
    {
        return _emailSender.SendEmailAsync(email, "Confirm your email",
            $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");
    }

    public Task SendPasswordResetLinkAsync(AuthUser user, string email, string resetLink)
    {
        return _emailSender.SendEmailAsync(email, "Reset your password",
            $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");
    }

    public Task SendPasswordResetCodeAsync(AuthUser user, string email, string resetCode)
    {
        return _emailSender.SendEmailAsync(email, "Reset your password",
            $"Please reset your password using the following code: {resetCode}");
    }
}
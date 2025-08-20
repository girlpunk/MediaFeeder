using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace MediaFeeder.Data.Identity;

public class UserManager(
    IUserStore<AuthUser> store,
    IOptions<IdentityOptions> optionsAccessor,
    IPasswordHasher<AuthUser> passwordHasher,
    IEnumerable<IUserValidator<AuthUser>> userValidators,
    IEnumerable<IPasswordValidator<AuthUser>> passwordValidators,
    ILookupNormalizer keyNormalizer,
    IdentityErrorDescriber errors,
    IServiceProvider services,
    ILogger<UserManager> logger)
    : UserManager<AuthUser>(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer,
        errors,
        services, logger)
{
    public async Task<AuthUser?> GetUserAsync(ClaimsPrincipal principal)
    {
        _logger.LogDebug("GetUserAsync {principal}", principal);

        ArgumentNullException.ThrowIfNull(principal);
        var id = GetUserId(principal);

        if (id == null)
            return null;

        var findId = await FindByIdAsync(id);
        if (findId != null)
            return findId;

        return await FindByLoginAsync(OpenIdConnectDefaults.AuthenticationScheme, id);
    }

    public Task<TUser?> FindByIdAsync(string userId)
    {
        _logger.LogDebug("FindByIdAsync {userId}", userId);
        return base.FindByIdAsync(userId);
    }

    public Task<TUser?> FindByLoginAsync(string loginProvider, string providerKey)
    {
        _logger.LogDebug("FindByLoginAsync {loginProvider} {providerKey}", loginProvider, providerKey);
        return base.FindByLoginAsync(loginProvider, providerKey);
    }
}

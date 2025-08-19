using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using System;
using System.Text.Json;

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
    ILogger<UserManager<AuthUser>> logger)
    : UserManager<AuthUser>(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer,
        errors,
        services, logger)
{
    public async Task<TUser?> GetUserAsync(ClaimsPrincipal principal)
    {
        ArgumentNullThrowHelper.ThrowIfNull(principal);
        var id = GetUserId(principal);

        if (id == null)
            return null;

        var findId = await FindByIdAsync(id);
        if (findId != null)
            return findId;

        logger.logDebug(JsonSerializer.Serialize(principal));

        return await FindByLoginAsync("PROVIDER", id);
    }
}

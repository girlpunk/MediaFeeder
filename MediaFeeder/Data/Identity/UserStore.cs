using System.Globalization;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Data.Identity;

public sealed class UserStore(IDbContextFactory<MediaFeederDataContext> contextFactory, ILogger<UserStore> logger)
    : IUserEmailStore<AuthUser>, IUserLoginStore<AuthUser>
{
    public void Dispose()
    {
        logger.LogDebug("Dispose");
    }

    public Task<string> GetUserIdAsync(AuthUser user, CancellationToken cancellationToken)
    {
        logger.LogDebug("GetUserIdAsync {user}", user);
        return Task.FromResult(user.Id.ToString("D", CultureInfo.InvariantCulture));
    }

    public Task<string?> GetUserNameAsync(AuthUser user, CancellationToken cancellationToken)
    {
        logger.LogDebug("GetUserNameAsync {user}", user);
        return Task.FromResult<string?>(user.Username);
    }

    public Task SetUserNameAsync(AuthUser user, string? userName, CancellationToken cancellationToken)
    {
        logger.LogDebug("SetUserNameAsync {user} {userName}", user, userName);

        if (userName != null)
            user.Username = userName;

        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(AuthUser user, CancellationToken cancellationToken)
    {
        logger.LogDebug("GetNormalizedUserNameAsync {user}", user);
        return Task.FromResult<string?>(user.UserName);
    }

    public Task SetNormalizedUserNameAsync(AuthUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        logger.LogDebug("SetNormalizedUserNameAsync {user} {normalizedName}", user, normalizedName);

        user.Username = normalizedName ?? "";
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(AuthUser user, CancellationToken cancellationToken)
    {
        logger.LogDebug("CreateAsync {user}", user);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        user.DateJoined = DateTimeOffset.Now;
        db.AuthUsers.Add(user);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await Task.FromResult(IdentityResult.Success).ConfigureAwait(false);
    }

    public async Task<IdentityResult> UpdateAsync(AuthUser user, CancellationToken cancellationToken)
    {
        logger.LogDebug("UpdateAsync {user}", user);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        db.AuthUsers.Update(user);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await Task.FromResult(IdentityResult.Success).ConfigureAwait(false);
    }

    public async Task<IdentityResult> DeleteAsync(AuthUser user, CancellationToken cancellationToken)
    {
        logger.LogDebug("DeleteAsync {user}", user);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.Remove(user);

        var i = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await Task.FromResult(i == 1 ? IdentityResult.Success : IdentityResult.Failed()).ConfigureAwait(false);
    }

    public async Task<AuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        logger.LogDebug("FindByIdAsync {userId}", userId);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        if (!int.TryParse(userId, out var id))
            return null;

        return await db.AuthUsers
            .SingleOrDefaultAsync(
                u => u.Id == id,
                cancellationToken
            );
    }

    public async Task<AuthUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        logger.LogDebug("FindByNameAsync {normalizedUserName}", normalizedUserName);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.AuthUsers
            .SingleOrDefaultAsync(
                p => p.Username.ToUpper() == normalizedUserName || p.Email.ToUpper() == normalizedUserName,
                cancellationToken);
    }

    public Task<string?> GetEmailAsync(AuthUser user, CancellationToken cancellationToken)
    {
        logger.LogDebug("GetEmailAsync {user}", user);
        return Task.FromResult<string?>(user.Email);
    }

    public Task SetEmailAsync(AuthUser user, string? email, CancellationToken cancellationToken)
    {
        logger.LogDebug("SetEmailAsync {user} {email}", user, email);

        if (email != null)
            user.Email = email;

        return Task.CompletedTask;
    }

    public Task<bool> GetEmailConfirmedAsync(AuthUser user, CancellationToken cancellationToken)
    {
        logger.LogDebug("GetEmailConfirmedAsync {user}", user);
        return Task.FromResult(true);
    }

    public Task SetEmailConfirmedAsync(AuthUser user, bool confirmed, CancellationToken cancellationToken)
    {
        logger.LogDebug("SetEmailConfirmedAsync {user} {confirmed}", user, confirmed);
        return Task.CompletedTask;
    }

    public async Task<AuthUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        logger.LogDebug("FindByEmailAsync {normalizedEmail}", normalizedEmail);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.AuthUsers.SingleOrDefaultAsync(user => user.Email == normalizedEmail,
            cancellationToken);
    }

    public Task<string?> GetNormalizedEmailAsync(AuthUser user, CancellationToken cancellationToken)
    {
        logger.LogDebug("GetNormalizedEmailAsync {user}", user);
        return Task.FromResult<string?>(user.Email);
    }

    public Task SetNormalizedEmailAsync(AuthUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        logger.LogDebug("SetNormalizedEmailAsync {user} {normalizedEmail}", user, normalizedEmail);

        user.Email = normalizedEmail ?? "";
        return Task.CompletedTask;
    }

    public async Task AddLoginAsync(AuthUser user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        logger.LogDebug("AddLoginAsync {user} {login}", user, login);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var provider = new AuthProvider()
        {
            LoginProvider = login.LoginProvider,
            ProviderKey = login.ProviderKey,
            UserId = user.Id,
        };

        db.AuthProviders.Add(provider);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task RemoveLoginAsync(AuthUser user, string loginProvider, string providerKey,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("RemoveLoginAsync {user} {loginProvider} {providerKey}", user, loginProvider, providerKey);
        throw new NotSupportedException();
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(AuthUser user, CancellationToken cancellationToken)
    {
        logger.LogDebug("GetLoginsAsync {user}", user);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.AuthProviders.Where(p => p.UserId == user.Id)
            .Select(static provider => new UserLoginInfo(provider.LoginProvider, provider.ProviderKey, provider.ToString()))
            .ToListAsync(cancellationToken);
    }

    public async Task<AuthUser?> FindByLoginAsync(string loginProvider, string providerKey,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("FindByLoginAsync {loginProvider} {providerKey}", loginProvider, providerKey);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var pair = await db.AuthProviders.SingleOrDefaultAsync(
                provider =>
                    provider.LoginProvider == loginProvider &&
                    provider.ProviderKey == providerKey, cancellationToken)
            .ConfigureAwait(false);

        if (pair == null)
            return null;

        await db.Entry(pair)
            .Reference(static b => b.User)
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);

        return pair.User;
    }
}

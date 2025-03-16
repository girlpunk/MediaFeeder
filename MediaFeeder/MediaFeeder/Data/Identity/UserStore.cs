using System.Globalization;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Data.Identity;

public sealed class UserStore(IDbContextFactory<MediaFeederDataContext> contextFactory)
    : IUserEmailStore<AuthUser>, IUserLoginStore<AuthUser>
{
    public void Dispose()
    {
    }

    public Task<string> GetUserIdAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString("D", CultureInfo.InvariantCulture));

    public Task<string?> GetUserNameAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.Username);

    public Task SetUserNameAsync(AuthUser user, string? userName, CancellationToken cancellationToken)
    {
        if (userName != null)
            user.Username = userName;

        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName);

    public Task SetNormalizedUserNameAsync(AuthUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.Username = normalizedName ?? "";
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(AuthUser user, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        user.DateJoined = DateTimeOffset.Now;
        db.AuthUsers.Add(user);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await Task.FromResult(IdentityResult.Success).ConfigureAwait(false);
    }

    public Task<IdentityResult> UpdateAsync(AuthUser user, CancellationToken cancellationToken) => throw new NotSupportedException(nameof(UpdateAsync));

    public async Task<IdentityResult> DeleteAsync(AuthUser user, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.Remove(user);

        var i = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await Task.FromResult(i == 1 ? IdentityResult.Success : IdentityResult.Failed()).ConfigureAwait(false);
    }

    public async Task<AuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
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
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.AuthUsers
            .SingleOrDefaultAsync(
                p => p.Username.ToUpper() == normalizedUserName || p.Email.ToUpper() == normalizedUserName,
                cancellationToken);
    }

    public Task<string?> GetEmailAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.Email);

    public Task SetEmailAsync(AuthUser user, string? email, CancellationToken cancellationToken)
    {
        if (email != null)
            user.Email = email;

        return Task.CompletedTask;
    }

    public Task<bool> GetEmailConfirmedAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task SetEmailConfirmedAsync(AuthUser user, bool confirmed, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<AuthUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.AuthUsers.SingleOrDefaultAsync(user => user.Email == normalizedEmail,
            cancellationToken);
    }

    public Task<string?> GetNormalizedEmailAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.Email);

    public Task SetNormalizedEmailAsync(AuthUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.Email = normalizedEmail ?? "";
        return Task.CompletedTask;
    }

    public async Task AddLoginAsync(AuthUser user, UserLoginInfo login, CancellationToken cancellationToken)
    {
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
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(AuthUser user, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.AuthProviders.Where(p => p.UserId == user.Id)
            .Select(static provider => new UserLoginInfo(provider.LoginProvider, provider.ProviderKey, provider.ToString()))
            .ToListAsync(cancellationToken);
    }

    public async Task<AuthUser?> FindByLoginAsync(string loginProvider, string providerKey,
        CancellationToken cancellationToken)
    {
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
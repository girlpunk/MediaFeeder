using MediaFeeder.Models.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Data.Identity
{
    public sealed class UserStore : IUserEmailStore<AuthUser>, IUserLoginStore<AuthUser>
    {
        private readonly MediaFeederDataContext _db;

        public UserStore(MediaFeederDataContext db)
        {
            this._db = db;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
                _db.Dispose();
        }

        public Task<string> GetUserIdAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());

        public Task<string> GetUserNameAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult(user.Username);

        public Task SetUserNameAsync(AuthUser user, string userName, CancellationToken cancellationToken)
        {
            user.Username = userName;
            return Task.CompletedTask;
        }

        public Task<string> GetNormalizedUserNameAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult(user.Username);

        public Task SetNormalizedUserNameAsync(AuthUser user, string normalizedName, CancellationToken cancellationToken)
        {
            user.Username = normalizedName;
            return Task.CompletedTask;
        }

        public async Task<IdentityResult> CreateAsync(AuthUser user, CancellationToken cancellationToken)
        {
            _db.Add(user);

            await _db.SaveChangesAsync(cancellationToken);

            return await Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(AuthUser user, CancellationToken cancellationToken) => throw new NotImplementedException(nameof(UpdateAsync));

        public async Task<IdentityResult> DeleteAsync(AuthUser user, CancellationToken cancellationToken)
        {
            _db.Remove(user);

            var i = await _db.SaveChangesAsync(cancellationToken);

            return await Task.FromResult(i == 1 ? IdentityResult.Success : IdentityResult.Failed());
        }

#pragma warning disable CS8613
        public async Task<AuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
#pragma warning restore CS8613
        {
            if (int.TryParse(userId, out var id))
                return await _db.AuthUsers.FindAsync(new object?[] { id }, cancellationToken: cancellationToken);

            return await Task.FromResult((AuthUser?)null);
        }

#pragma warning disable CS8613
        public async Task<AuthUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
#pragma warning restore CS8613
        {
            return await _db.AuthUsers
                .SingleOrDefaultAsync(p => string.Equals(p.Username, normalizedUserName, StringComparison.CurrentCultureIgnoreCase), cancellationToken);
        }

        public Task<string> GetEmailAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);

        public Task SetEmailAsync(AuthUser user, string email, CancellationToken cancellationToken)
        {
            user.Email = email;
            return Task.CompletedTask;
        }

        public Task<bool> GetEmailConfirmedAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task SetEmailConfirmedAsync(AuthUser user, bool confirmed, CancellationToken cancellationToken) => Task.CompletedTask;

#pragma warning disable CS8613
        public async Task<AuthUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
#pragma warning restore CS8613
        {
            return await _db.AuthUsers.SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken: cancellationToken);
        }

        public Task<string> GetNormalizedEmailAsync(AuthUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);

        public Task SetNormalizedEmailAsync(AuthUser user, string normalizedEmail, CancellationToken cancellationToken)
        {
            user.Email = normalizedEmail;
            return Task.CompletedTask;
        }

        public Task AddLoginAsync(AuthUser user, UserLoginInfo login, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task RemoveLoginAsync(AuthUser user, string loginProvider, string providerKey, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IList<UserLoginInfo>> GetLoginsAsync(AuthUser user, CancellationToken cancellationToken) => throw new NotImplementedException();

#pragma warning disable CS8613
        public async Task<AuthUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
#pragma warning restore CS8613
        {
            var pair = await _db.AuthProviders.SingleOrDefaultAsync(
                provider =>
                    provider.LoginProvider == loginProvider &&
                    provider.ProviderKey == providerKey, cancellationToken);

            if (pair == null)
                return null;

            await _db.Entry(pair)
                .Reference(static b => b.User)
                .LoadAsync(cancellationToken);

            return pair.User;
        }
    }
}

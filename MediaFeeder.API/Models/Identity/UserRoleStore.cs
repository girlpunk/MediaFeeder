using MediaFeeder.API.Models.db;
using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.API.Models.Identity;

public class UserRoleStore : IUserRoleStore<AuthUser>
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Task<string> GetUserIdAsync(AuthUser user, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<string?> GetUserNameAsync(AuthUser user, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task SetUserNameAsync(AuthUser user, string? userName, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<string?> GetNormalizedUserNameAsync(AuthUser user, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task SetNormalizedUserNameAsync(AuthUser user, string? normalizedName, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<IdentityResult> CreateAsync(AuthUser user, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<IdentityResult> UpdateAsync(AuthUser user, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<IdentityResult> DeleteAsync(AuthUser user, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<AuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<AuthUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task AddToRoleAsync(AuthUser user, string roleName, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task RemoveFromRoleAsync(AuthUser user, string roleName, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<IList<string>> GetRolesAsync(AuthUser user, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<bool> IsInRoleAsync(AuthUser user, string roleName, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<IList<AuthUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken) => throw new NotImplementedException();
}

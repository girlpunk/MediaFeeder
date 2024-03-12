using MediaFeeder.API.Models.db;
using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.API.Models.Identity;

public class ApplicationRoleManager : RoleManager<AuthGroup>
{
    public ApplicationRoleManager(IRoleStore<AuthGroup> store, IEnumerable<IRoleValidator<AuthGroup>> roleValidators, ILookupNormalizer keyNormalizer, IdentityErrorDescriber errors, ILogger<RoleManager<AuthGroup>> logger)
        : base(store, roleValidators, keyNormalizer, errors, logger)
    {
    }
}

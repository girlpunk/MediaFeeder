using MediaFeeder.Models.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace MediaFeeder.Data.Identity;

public class ApplicationRoleStore : RoleStore<AuthGroup, MediaFeederDataContext, int, AuthUserGroup, AuthGroupPermission>
{
    public ApplicationRoleStore(MediaFeederDataContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
    }
}

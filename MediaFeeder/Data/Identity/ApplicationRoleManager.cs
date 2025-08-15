using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Identity;

namespace MediaFeeder.Data.Identity;

public class ApplicationRoleManager(
    IRoleStore<AuthGroup> store,
    IEnumerable<IRoleValidator<AuthGroup>> roleValidators,
    ILookupNormalizer keyNormalizer,
    IdentityErrorDescriber errors,
    ILogger<RoleManager<AuthGroup>> logger)
    : RoleManager<AuthGroup>(store, roleValidators, keyNormalizer, errors, logger);

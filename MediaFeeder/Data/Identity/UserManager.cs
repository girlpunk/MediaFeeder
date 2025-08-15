using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

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
        services, logger);

using MediaFeeder.API.Models.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MediaFeeder.API.Models.Identity;

public class UserManager : UserManager<AuthUser>
{
    public UserManager(IUserStore<AuthUser> store, IOptions<IdentityOptions> optionsAccessor, IPasswordHasher<AuthUser> passwordHasher, IEnumerable<IUserValidator<AuthUser>> userValidators, IEnumerable<IPasswordValidator<AuthUser>> passwordValidators, ILookupNormalizer keyNormalizer, IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<AuthUser>> logger) : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
    {
    }
}

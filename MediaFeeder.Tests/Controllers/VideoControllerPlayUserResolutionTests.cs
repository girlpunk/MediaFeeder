using System.Net;
using System.Security.Claims;
using MediaFeeder.Data;
using MediaFeeder.Data.Identity;
using MediaFeeder.Data.db;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace MediaFeeder.Controllers;

// Verifies bug med-30 (bugs/medium/med-30-play-log-leak-and-500.md)
// A bearer whose sub claim does not resolve to a local user must receive a 403 from
// GET /api/Video/{id}/play, not a 500. Today an unresolvable (or unparseable) sub makes
// UserManager.GetUserAsync return null and the action calls
// ArgumentNullException.ThrowIfNull(user), surfacing as an unhandled 500 instead of
// Forbid().
public sealed class VideoControllerPlayUserResolutionTests
{
    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions = new DbContextOptionsBuilder<MediaFeederDataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task Play_SubResolvesToNoLocalUser_Returns403InsteadOf500()
    {
        var controller = CreateController(CreatePrincipal("999", videoClaimId: 1));

        await Should.NotThrowAsync(() => controller.Play(1));
        var result = await controller.Play(1);

        GetStatusCode(result).ShouldBe((int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Play_UnparseableSub_Returns403InsteadOf500()
    {
        var controller = CreateController(CreatePrincipal("not-a-user-id", videoClaimId: 1));

        await Should.NotThrowAsync(() => controller.Play(1));
        var result = await controller.Play(1);

        GetStatusCode(result).ShouldBe((int)HttpStatusCode.Forbidden);
    }

    private VideoController CreateController(ClaimsPrincipal user) =>
        new VideoController(
            CreateContextFactory(),
            CreateUserManager(),
            CreateConfiguration(),
            Substitute.For<ILogger<VideoController>>()
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };

    private static int? GetStatusCode(IActionResult result) => result switch
    {
        ForbidResult => (int)HttpStatusCode.Forbidden,
        StatusCodeResult statusResult => statusResult.StatusCode,
        ObjectResult objectResult => objectResult.StatusCode,
        _ => null,
    };

    private static ClaimsPrincipal CreatePrincipal(string userId, int? videoClaimId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
        };

        if (videoClaimId.HasValue)
            claims.Add(new("Video", videoClaimId.Value.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"));
    }

    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal))
        .Build();

    private static IDbContextFactory<MediaFeederDataContext> CreateContextFactory(
        DbContextOptions<MediaFeederDataContext> contextOptions
    )
    {
        return new TestDbContextFactory(contextOptions);
    }

    private IDbContextFactory<MediaFeederDataContext> CreateContextFactory() => CreateContextFactory(_contextOptions);

    private UserManager CreateUserManager()
    {
        var store = new UserStore(CreateContextFactory(), Substitute.For<ILogger<UserStore>>());

        return new UserManager(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<AuthUser>(),
            Array.Empty<IUserValidator<AuthUser>>(),
            Array.Empty<IPasswordValidator<AuthUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager>>()
        );
    }
}

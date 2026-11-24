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

// Verifies bug med-02 (bugs/medium/med-02-403-claims-leak-play-endpoint.md)
// A 403 from GET /api/Video/{id}/play must never contain the serialized ClaimsPrincipal
// of the caller. Today, when the token's Video claim is missing or does not match the
// requested id, the response body embeds "Full User: <json of the whole principal>",
// so any holder of a single-video token can exfiltrate the owner's complete claim set
// (sub, email, roles) just by probing a foreign video id.
public sealed class VideoControllerPlayClaimsLeakTests
{
    private const string ProbeEmail = "alice@example.com";

    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions = new DbContextOptionsBuilder<MediaFeederDataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task Play_VideoClaimDoesNotMatchRequestedId_ReturnsPlain403WithoutCallerClaims()
    {
        var userId = await SeedUserAsync("alice");
        var controller = CreateController(CreatePrincipal(userId.ToString(), videoClaimId: 1));

        var result = await controller.Play(2);

        GetStatusCode(result).ShouldBe((int)HttpStatusCode.Forbidden);

        var body = GetBody(result);
        body.ShouldNotContain("Full User");
        body.ShouldNotContain(ProbeEmail);
        body.ShouldNotContain("nameidentifier");
    }

    [Fact]
    public async Task Play_MissingVideoClaim_ReturnsPlain403WithoutCallerClaims()
    {
        var userId = await SeedUserAsync("alice");
        var controller = CreateController(CreatePrincipal(userId.ToString()));

        var result = await controller.Play(1);

        GetStatusCode(result).ShouldBe((int)HttpStatusCode.Forbidden);

        var body = GetBody(result);
        body.ShouldNotContain("Full User");
        body.ShouldNotContain(ProbeEmail);
        body.ShouldNotContain("nameidentifier");
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

    private static ClaimsPrincipal CreatePrincipal(string userId, int? videoClaimId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new(ClaimTypes.Email, ProbeEmail),
        };

        if (videoClaimId.HasValue)
            claims.Add(new("Video", videoClaimId.Value.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"));
    }

    private static string GetBody(IActionResult result) => result switch
    {
        ObjectResult objectResult => objectResult.Value?.ToString() ?? "",
        _ => "",
    };

    private static int? GetStatusCode(IActionResult result) => result switch
    {
        ForbidResult => (int)HttpStatusCode.Forbidden,
        StatusCodeResult statusResult => statusResult.StatusCode,
        ObjectResult objectResult => objectResult.StatusCode,
        _ => null,
    };

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

    private async Task<int> SeedUserAsync(string username)
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var user = new AuthUser
        {
            Id = (await db.AuthUsers.MaxAsync(static u => u.Id)) + 1,
            Username = username,
            Email = username + "@example.com",
        };

        db.AuthUsers.Add(user);
        await db.SaveChangesAsync(CancellationToken.None);
        return user.Id;
    }
}

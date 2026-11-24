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

// Verifies bug med-29 (bugs/medium/med-29-status-401-403-inconsistency.md)
// "Authenticated but not allowed" must be answered with 403 everywhere, reserving
// 401 for unauthenticated callers. Today an authorized non-owner calling
// GET /api/Subscriptions/{id} receives 401 (Unauthorized); the TV client maps 401 to
// "re-authenticate", producing an unrecoverable re-auth loop on multi-user servers.
public sealed class SubscriptionsControllerNonOwnerStatusTests
{
    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions = new DbContextOptionsBuilder<MediaFeederDataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task GetSubscription_AuthorizedNonOwner_ReturnsForbiddenNotUnauthorized()
    {
        var ownerId = await SeedUserAsync("alice");
        var intruderId = await SeedUserAsync("bob");
        var subscriptionId = await SeedSubscriptionAsync(ownerId, "Alice Channel");

        var controller = CreateController(CreatePrincipal(intruderId.ToString()));

        var result = await controller.GetSubscription(subscriptionId);

        GetStatusCode(result.Result).ShouldBe((int)HttpStatusCode.Forbidden);
    }

    private SubscriptionsController CreateController(ClaimsPrincipal user) =>
        new SubscriptionsController(
            CreateContextFactory(),
            CreateUserManager(),
            CreateConfiguration()
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };

    private static int? GetStatusCode(IActionResult? result) => result switch
    {
        ForbidResult => (int)HttpStatusCode.Forbidden,
        StatusCodeResult statusResult => statusResult.StatusCode,
        ObjectResult objectResult => objectResult.StatusCode,
        _ => null,
    };

    private static ClaimsPrincipal CreatePrincipal(string userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
        };

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

    private async Task<int> SeedSubscriptionAsync(int userId, string name)
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = new Subscription
        {
            Name = name,
            PlaylistId = "playlist-" + name,
            Description = "",
            ParentFolderId = 1,
            UserId = userId,
            ChannelId = "channel-" + name,
            ChannelName = name,
            Provider = "YouTube",
        };

        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(CancellationToken.None);
        return subscription.Id;
    }
}

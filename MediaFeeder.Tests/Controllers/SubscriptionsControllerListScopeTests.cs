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

// Verifies bug sec-08 (bugs/security/sec-08-android-tv-renders-cross-user-subscriptions.md)
// Server portion only: GET /api/Subscriptions must return only the caller's own
// subscriptions. Today it resolves no user at all and returns every subscription of
// every user — which the new Android TV app renders as its home screen on a shared TV
// (all accounts' names, channel/playlist ids, thumbnails become visible).
public sealed class SubscriptionsControllerListScopeTests
{
    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions = new DbContextOptionsBuilder<MediaFeederDataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task GetSubscriptions_ReturnsOnlySubscriptionsOwnedByCaller()
    {
        var callerId = await SeedUserAsync("alice");
        var strangerId = await SeedUserAsync("bob");
        await SeedSubscriptionAsync(callerId, "Alice Channel");
        await SeedSubscriptionAsync(strangerId, "Bob Channel");

        var controller = CreateController(CreatePrincipal(callerId.ToString()));

        var result = await controller.GetSubscriptions();

        var objectResult = result.Result.ShouldBeAssignableTo<ObjectResult>();
        var value = objectResult.Value.ShouldNotBeNull();
        var subscriptions = value
            .ShouldBeAssignableTo<IEnumerable<Subscription>>()
            .Select(static s => s.Name)
            .ToList();

        subscriptions.ShouldContain("Alice Channel");
        subscriptions.ShouldNotContain("Bob Channel");
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

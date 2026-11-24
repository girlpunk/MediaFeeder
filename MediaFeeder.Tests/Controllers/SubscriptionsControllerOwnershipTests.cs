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

// Verifies bug sec-01 (bugs/security/sec-01-subscriptions-crud-missing-ownership.md)
// The SubscriptionsController CRUD endpoints must verify that the caller owns the
// subscription. Today PUT/DELETE operate on any user's subscription (with no
// ownership check) and POST accepts a client-supplied UserId, letting any API token
// read-mutate-delete or steal other users' subscriptions.
public sealed class SubscriptionsControllerOwnershipTests
{
    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions = new DbContextOptionsBuilder<MediaFeederDataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task DeleteSubscription_ByNonOwner_DoesNotDeleteOthersSubscription()
    {
        var ownerId = await SeedUserAsync("alice");
        var intruderId = await SeedUserAsync("bob");
        var subscriptionId = await SeedSubscriptionAsync(ownerId, "Alice Channel");

        var controller = CreateController(CreatePrincipal(intruderId.ToString()));

        var result = await controller.DeleteSubscription(subscriptionId);

        GetStatusCode(result).ShouldBeOneOf((int)HttpStatusCode.Forbidden, (int)HttpStatusCode.NotFound);

        await using var db = new MediaFeederDataContext(_contextOptions);
        (await db.Subscriptions.AnyAsync(s => s.Id == subscriptionId)).ShouldBeTrue();
    }

    [Fact]
    public async Task PutSubscription_ByNonOwner_DoesNotModifyOthersSubscription()
    {
        var ownerId = await SeedUserAsync("alice");
        var intruderId = await SeedUserAsync("bob");
        var subscriptionId = await SeedSubscriptionAsync(ownerId, "Alice Channel");

        var controller = CreateController(CreatePrincipal(intruderId.ToString()));

        var payload = new Subscription
        {
            Id = subscriptionId,
            Name = "Stolen By Bob",
            PlaylistId = "playlist-alice",
            Description = "",
            ParentFolderId = 1,
            UserId = ownerId,
            ChannelId = "channel-alice",
            ChannelName = "Alice Channel",
            Provider = "YouTube",
        };

        var result = await controller.PutSubscription(subscriptionId, payload);

        GetStatusCode(result).ShouldBeOneOf((int)HttpStatusCode.Forbidden, (int)HttpStatusCode.NotFound);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = await db.Subscriptions.SingleAsync(s => s.Id == subscriptionId);
        subscription.Name.ShouldBe("Alice Channel");
    }

    [Fact]
    public async Task PostSubscription_WithForeignUserId_CreatesSubscriptionOwnedByCaller()
    {
        var ownerId = await SeedUserAsync("alice");
        var callerId = await SeedUserAsync("bob");

        var controller = CreateController(CreatePrincipal(callerId.ToString()));

        var payload = new Subscription
        {
            Name = "Spoofed Subscription",
            PlaylistId = "playlist-spoofed",
            Description = "",
            ParentFolderId = 1,
            UserId = ownerId,
            ChannelId = "channel-spoofed",
            ChannelName = "Spoofed Subscription",
            Provider = "YouTube",
        };

        await controller.PostSubscription(payload);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = await db.Subscriptions.SingleAsync(static s => s.Name == "Spoofed Subscription");
        subscription.UserId.ShouldBe(callerId);
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

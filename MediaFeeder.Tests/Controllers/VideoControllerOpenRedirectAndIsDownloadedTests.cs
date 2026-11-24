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

// Verifies bug low-24 (bugs/low/low-24-preexisting-open-redirect-and-isdownloaded.md)
// (1) GET /api/Video/{id}/play must not 302 the authenticated player to an arbitrary
//     URL stored in Video.DownloadedPath (provider-originated enclosure URLs), an open
//     redirect of the TV session; (2) Video.IsDownloaded must reflect
//     DownloadedPath != null — today it is an uninitialized get-only auto-property, so
//     the server always serializes "isDownloaded": false.
public sealed class VideoControllerOpenRedirectAndIsDownloadedTests
{
    private const string EvilUrl = "https://evil.example.com/phish";
    private const string LocalPath = "/media/videos/abc123.mp4";

    private readonly DbContextOptions<MediaFeederDataContext> _contextOptions = new DbContextOptionsBuilder<MediaFeederDataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task Play_DownloadedPathIsExternalProviderUrl_DoesNotRedirectPlayerToThatUrl()
    {
        var userId = await SeedUserAsync("alice");
        var subscriptionId = await SeedSubscriptionAsync(userId);
        var videoId = await SeedVideoAsync(subscriptionId, EvilUrl);

        var controller = CreateController(CreatePrincipal(userId.ToString(), videoClaimId: videoId));

        var result = await controller.Play(videoId);

        if (result is RedirectResult redirect)
        {
            redirect.Url.ShouldNotBe(EvilUrl);
        }
    }

    [Fact]
    public async Task Video_WithDownloadedPath_IsDownloadedIsTrue()
    {
        var userId = await SeedUserAsync("alice");
        var subscriptionId = await SeedSubscriptionAsync(userId);
        await SeedVideoAsync(subscriptionId, LocalPath);

        await using var db = new MediaFeederDataContext(_contextOptions);
        var video = await db.Videos.SingleAsync(static v => v.DownloadedPath == LocalPath);

        video.IsDownloaded.ShouldBeTrue();
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

    private async Task<int> SeedSubscriptionAsync(int userId)
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var subscription = new Subscription
        {
            Name = "Alice Channel",
            PlaylistId = "playlist-alice",
            Description = "",
            ParentFolderId = 1,
            UserId = userId,
            ChannelId = "channel-alice",
            ChannelName = "Alice Channel",
            Provider = "YouTube",
        };

        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(CancellationToken.None);
        return subscription.Id;
    }

    private async Task<int> SeedVideoAsync(int subscriptionId, string? downloadedPath)
    {
        await using var db = new MediaFeederDataContext(_contextOptions);
        var video = new Video
        {
            VideoId = "yt-video-" + subscriptionId,
            Name = "Test Video",
            Description = "",
            UploaderName = "Uploader",
            SubscriptionId = subscriptionId,
            DownloadedPath = downloadedPath,
        };

        db.Videos.Add(video);
        await db.SaveChangesAsync(CancellationToken.None);
        return video.Id;
    }
}

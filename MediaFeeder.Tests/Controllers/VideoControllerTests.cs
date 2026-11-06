using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;
using MediaFeeder.Data.Identity;
using MediaFeeder.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace MediaFeeder.Controllers;

public sealed class VideoControllerTests : IDisposable
{
    private const string AuthSecret = "unit-test-playback-secret-key-0123456789-abcdef";

    private readonly DirectoryInfo _mediaRoot = Directory.CreateTempSubdirectory();

    public void Dispose() => _mediaRoot.Delete(true);

    [Fact]
    public async Task Play_ApiTokenOwner_ServesFileWithRange()
    {
        var filePath = Path.Join(_mediaRoot.FullName, "video-1.mp4");
        File.WriteAllBytes(filePath, [0x00, 0x01, 0x02]);

        var context = CreateContext(context =>
        {
            AddUser(context, CreateUser(1));
            AddSubscription(context, 1, 1);
            AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), filePath);
        });
        var controller = CreateVideoController(context, CreatePrincipal(1), CreateUser(1));

        var result = await controller.Play(1);

        var physicalFile = result.ShouldBeOfType<PhysicalFileResult>();
        physicalFile.FileName.ShouldBe(filePath);
        physicalFile.EnableRangeProcessing.ShouldBeTrue();
    }

    [Fact]
    public async Task Play_ApiTokenNotOwner_ReturnsForbid()
    {
        var context = CreateContext(static context =>
        {
            AddUser(context, CreateUser(1));
            AddUser(context, CreateUser(2));
            AddSubscription(context, 1, 1);
            AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        });
        var controller = CreateVideoController(context, CreatePrincipal(2), CreateUser(2));

        var result = await controller.Play(1);

        result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Play_ScopedTokenValid_ServesFile()
    {
        var filePath = Path.Join(_mediaRoot.FullName, "video-1.mp4");
        File.WriteAllBytes(filePath, [0x00, 0x01, 0x02]);

        var context = CreateContext(context =>
        {
            AddUser(context, CreateUser(1));
            AddUser(context, CreateUser(2));
            AddSubscription(context, 1, 1);
            AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), filePath);
        });
        var controller = CreateVideoController(context, CreateScopedPrincipal(2, 1), CreateUser(2));

        var result = await controller.Play(1);

        var physicalFile = result.ShouldBeOfType<PhysicalFileResult>();
        physicalFile.FileName.ShouldBe(filePath);
    }

    [Fact]
    public async Task Play_UnknownVideo_ReturnsNotFound()
    {
        var context = CreateContext(static context =>
        {
            AddUser(context, CreateUser(1));
            AddSubscription(context, 1, 1);
            AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        });
        var controller = CreateVideoController(context, CreatePrincipal(1), CreateUser(1));

        var result = await controller.Play(999);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Play_NotDownloaded_ReturnsPreconditionFailed()
    {
        var context = CreateContext(static context =>
        {
            AddUser(context, CreateUser(1));
            AddSubscription(context, 1, 1);
            AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        });
        var controller = CreateVideoController(context, CreatePrincipal(1), CreateUser(1));

        var result = await controller.Play(1);

        GetStatusCode(result).ShouldBe((int)HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Play_ExternalPath_Redirects()
    {
        const string ExternalUrl = "https://example.com/video.mp4";

        var context = CreateContext(static context =>
        {
            AddUser(context, CreateUser(1));
            AddSubscription(context, 1, 1);
            AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), ExternalUrl);
        });
        var controller = CreateVideoController(context, CreatePrincipal(1), CreateUser(1));

        var result = await controller.Play(1);

        var redirect = result.ShouldBeOfType<RedirectResult>();
        redirect.Url.ShouldBe(ExternalUrl);
    }

    [Fact]
    public async Task PlayToken_UnknownVideo_ReturnsNotFound()
    {
        var context = CreateContext(static context =>
        {
            AddUser(context, CreateUser(1));
            AddSubscription(context, 1, 1);
        });
        var controller = CreateVideoController(context, CreatePrincipal(1), CreateUser(1));

        var result = await controller.PlayToken(999);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PlayToken_NoUser_ReturnsForbid()
    {
        var context = CreateContext(static context =>
        {
            AddUser(context, CreateUser(2));
            AddSubscription(context, 1, 2);
            AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        });
        var controller = CreateVideoController(context, CreatePrincipal(1), null);

        var result = await controller.PlayToken(1);

        result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task PlayToken_NotOwner_ReturnsForbid()
    {
        var context = CreateContext(static context =>
        {
            AddUser(context, CreateUser(1));
            AddUser(context, CreateUser(2));
            AddSubscription(context, 1, 2);
            AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        });
        var controller = CreateVideoController(context, CreatePrincipal(1), CreateUser(1));

        var result = await controller.PlayToken(1);

        result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task PlayToken_Owner_ReturnsJwtWithDownloadRoleAndVideoClaim()
    {
        var context = CreateContext(static context =>
        {
            AddUser(context, CreateUser(1));
            AddSubscription(context, 1, 1);
            AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        });
        var controller = CreateVideoController(context, CreatePrincipal(1), CreateUser(1));

        var result = await controller.PlayToken(1);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var token = ExtractToken(okResult.Value);

        var payload = DecodeJwtPayload(token);
        payload.RootElement.GetProperty("iss").GetString().ShouldBe("MediaFeeder");
        payload.RootElement.GetProperty("sub").GetString().ShouldBe("1");
        payload.RootElement.GetProperty("Video").GetString().ShouldBe("1");

        var roleClaim = payload
            .RootElement
            .EnumerateObject()
            .Single(static property => property.Name == ClaimTypes.Role);
        roleClaim.Value.GetString().ShouldBe("Download");
    }

    private static VideoController CreateVideoController(
        MediaFeederDataContext context,
        ClaimsPrincipal principal,
        AuthUser? resolvedUser
    )
    {
        var contextFactory = new FakeDbContextFactory(context);
        var userManager = new FakeUserManager(resolvedUser);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Auth:issuer"] = "MediaFeeder",
                ["Auth:secret"] = AuthSecret,
            })
            .Build();

        var tokenHelper = new TokenHelper(configuration);
        var logger = new FakeLogger<VideoController>();

        var controller = new VideoController(contextFactory, userManager, configuration, tokenHelper, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
        return controller;
    }

    private static MediaFeederDataContext CreateContext(Action<MediaFeederDataContext> seed)
    {
        var context = new MediaFeederDataContext(
            new DbContextOptionsBuilder<MediaFeederDataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options
        );
        seed(context);
        context.SaveChanges();
        return context;
    }

    private static ClaimsPrincipal CreatePrincipal(int userId) => new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Bearer")
    );

    private static ClaimsPrincipal CreateScopedPrincipal(int userId, int videoId) => new ClaimsPrincipal(
        new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, "Download"),
                new Claim("Video", videoId.ToString()),
            ],
            "Bearer"
        )
    );

    private static AuthUser CreateUser(int id) => new AuthUser
    {
        Id = id,
        Username = $"user-{id}",
        Email = $"user-{id}@example.com",
    };

    private static void AddUser(MediaFeederDataContext context, AuthUser user)
    {
        context.AuthUsers.Add(user);
    }

    private static Subscription AddSubscription(MediaFeederDataContext context, int id, int userId)
    {
        var subscription = new Subscription
        {
            Id = id,
            Name = $"channel-{id}",
            PlaylistId = $"playlist-{id}",
            Description = $"Description of channel {id}",
            ParentFolderId = 1,
            UserId = userId,
            ChannelId = $"channel-{id}",
            ChannelName = $"Channel {id}",
            Provider = "youtube",
            WatchOrder = WatchOrder.OldestFirst,
        };
        context.Subscriptions.Add(subscription);
        return subscription;
    }

    private static Video AddVideo(
        MediaFeederDataContext context,
        int id,
        int subscriptionId,
        DateTimeOffset publishDate,
        string? downloadedPath = null
    )
    {
        var video = new Video
        {
            Id = id,
            VideoId = $"yt-{id}",
            Name = $"Video {id}",
            Description = $"Description of video {id}",
            SubscriptionId = subscriptionId,
            UploaderName = "Uploader",
            PublishDate = publishDate,
            DownloadedPath = downloadedPath,
        };
        context.Videos.Add(video);
        return video;
    }

    private static string ExtractToken(object? value)
    {
        value.ShouldNotBeNull();
        var token = JsonSerializer.SerializeToElement(value).GetProperty("token").GetString();
        return token ?? throw new InvalidOperationException("The response does not contain a 'token' property");
    }

    private static JsonDocument DecodeJwtPayload(string jwt)
    {
        var segments = jwt.Split('.');
        segments.Length.ShouldBe(3);

        var payload = segments[1].Replace('-', '+').Replace('_', '/');
        var padded = payload.PadRight(((payload.Length + 3) / 4) * 4, '=');
        return JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
    }

    private static int GetStatusCode(IActionResult result) => result switch
    {
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        ObjectResult objectResult when objectResult.StatusCode is not null => objectResult.StatusCode!.Value,
        _ => throw new InvalidOperationException(
            $"Expected a result carrying a status code, got {result.GetType().Name}"
        ),
    };

    private sealed class FakeDbContextFactory(MediaFeederDataContext context)
        : IDbContextFactory<MediaFeederDataContext>
    {
        public MediaFeederDataContext CreateDbContext() => context;

        public Task<MediaFeederDataContext> CreateDbContextAsync(CancellationToken cancellationToken)
            => Task.FromResult(context);
    }

    // Net10's shared framework no longer ships KeyNormalizer/NullServiceProvider/NullLogger,
    // so the fakes below stand in for them.
    private sealed class FakeLookupNormalizer : ILookupNormalizer
    {
        public string NormalizeKey(string? key) => key ?? string.Empty;

        public string NormalizeName(string? name) => name ?? string.Empty;

        public string NormalizeEmail(string? email) => email ?? string.Empty;
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class FakeLogger<TCategory> : ILogger<TCategory>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
            where TState : notnull
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeUserManager(AuthUser? user)
        : UserManager(
            Substitute.For<IUserStore<AuthUser>>(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<AuthUser>(),
            Array.Empty<IUserValidator<AuthUser>>(),
            Array.Empty<IPasswordValidator<AuthUser>>(),
            new FakeLookupNormalizer(),
            new IdentityErrorDescriber(),
            new FakeServiceProvider(),
            new FakeLogger<UserManager>()
        )
    {
        public override Task<AuthUser?> GetUserAsync(ClaimsPrincipal principal)
        {
            if (principal is null)
                return Task.FromResult<AuthUser?>(null);
            return Task.FromResult(user);
        }
    }
}

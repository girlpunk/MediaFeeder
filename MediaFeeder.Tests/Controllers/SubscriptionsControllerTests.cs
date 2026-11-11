using System.Diagnostics;
using System.Security.Claims;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Enums;
using MediaFeeder.Data.Identity;
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

public sealed class SubscriptionsControllerTests
{
    [Fact]
    public async Task GetSubscriptionVideos_UnknownId_ReturnsNotFound()
    {
        var user = CreateUser(1);
        var controller = CreateController(
            CreatePrincipal(1),
            user,
            static context =>
            {
                AddUser(context, CreateUser(1));
                AddSubscription(context, 1, 1);
            }
        );

        var result = await controller.GetSubscriptionVideos(999);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSubscriptionVideos_NoUser_ReturnsForbid()
    {
        var controller = CreateController(
            CreatePrincipal(1),
            null,
            static context =>
            {
                AddUser(context, CreateUser(2));
                AddSubscription(context, 1, 2);
                AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
            }
        );

        var result = await controller.GetSubscriptionVideos(1);

        result.ShouldBeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetSubscriptionVideos_NotOwner_ReturnsUnauthorized()
    {
        var user = CreateUser(1);
        var controller = CreateController(
            CreatePrincipal(1),
            user,
            static context =>
            {
                AddUser(context, CreateUser(1));
                AddUser(context, CreateUser(2));
                AddSubscription(context, 1, 2);
            }
        );

        var result = await controller.GetSubscriptionVideos(1);

        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetSubscriptionVideos_OldestFirst_OrdersPublishDateAscending()
    {
        var user = CreateUser(1);
        var controller = CreateController(
            CreatePrincipal(1),
            user,
            static context =>
            {
                AddUser(context, CreateUser(1));
                AddSubscription(context, 1, 1, WatchOrder.OldestFirst);
                AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
                AddVideo(context, 2, 1, new DateTimeOffset(2024, 3, 2, 0, 0, 0, TimeSpan.Zero));
                AddVideo(context, 3, 1, new DateTimeOffset(2024, 2, 20, 0, 0, 0, TimeSpan.Zero));
            }
        );

        var result = await controller.GetSubscriptionVideos(1);

        ExtractVideos(result).Select(static video => video.Id).ToList().ShouldBe([1, 3, 2]);
    }

    [Fact]
    public async Task GetSubscriptionVideos_NewestFirst_OrdersPublishDateDescending()
    {
        var user = CreateUser(1);
        var controller = CreateController(
            CreatePrincipal(1),
            user,
            static context =>
            {
                AddUser(context, CreateUser(1));
                AddSubscription(context, 1, 1, WatchOrder.NewestFirst);
                AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
                AddVideo(context, 2, 1, new DateTimeOffset(2024, 3, 2, 0, 0, 0, TimeSpan.Zero));
                AddVideo(context, 3, 1, new DateTimeOffset(2024, 2, 20, 0, 0, 0, TimeSpan.Zero));
            }
        );

        var result = await controller.GetSubscriptionVideos(1);

        ExtractVideos(result).Select(static video => video.Id).ToList().ShouldBe([2, 3, 1]);
    }

    [Fact]
    public async Task GetSubscriptionVideos_Owner_ReturnsAllVideos()
    {
        var user = CreateUser(1);
        var controller = CreateController(
            CreatePrincipal(1),
            user,
            static context =>
            {
                AddUser(context, CreateUser(1));
                AddSubscription(context, 1, 1, WatchOrder.NewestFirst);
                AddVideo(context, 1, 1, new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
                AddVideo(context, 2, 1, new DateTimeOffset(2024, 3, 2, 0, 0, 0, TimeSpan.Zero));
                AddVideo(context, 3, 1, new DateTimeOffset(2024, 2, 20, 0, 0, 0, TimeSpan.Zero));
            }
        );

        var result = await controller.GetSubscriptionVideos(1);

        var videos = ExtractVideos(result);
        videos.Count.ShouldBe(3);
        videos.Select(static video => video.Id).OrderBy(static id => id).ShouldBe([1, 2, 3]);
    }

    private static SubscriptionsController CreateController(
        ClaimsPrincipal principal,
        AuthUser? resolvedUser,
        Action<MediaFeederDataContext> seed
    )
    {
        var context = new MediaFeederDataContext(
            new DbContextOptionsBuilder<MediaFeederDataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options
        );
        seed(context);
        context.SaveChanges();

        var contextFactory = new FakeDbContextFactory(context);
        var userManager = new FakeUserManager(resolvedUser);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal))
            .Build();

        var controller = new SubscriptionsController(contextFactory, userManager, configuration);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
        return controller;
    }

    private static List<Video> ExtractVideos(IActionResult result)
    {
        var okResult = result.ShouldBeOfType<OkObjectResult>();
        return okResult.Value.ShouldBeAssignableTo<IEnumerable<Video>>().ToList();
    }

    private static ClaimsPrincipal CreatePrincipal(int userId) => new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Bearer")
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

    private static Subscription AddSubscription(
        MediaFeederDataContext context,
        int id,
        int userId,
        WatchOrder watchOrder = WatchOrder.OldestFirst
    )
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
            WatchOrder = watchOrder,
        };
        context.Subscriptions.Add(subscription);
        return subscription;
    }

    private static Video AddVideo(
        MediaFeederDataContext context,
        int id,
        int subscriptionId,
        DateTimeOffset publishDate
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
        };
        context.Videos.Add(video);
        return video;
    }

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

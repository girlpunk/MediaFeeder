using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentValidation;
using Google.Apis.Services;
using MassTransit;
using MassTransit.Logging;
using Mediafeeder;
using MediaFeeder;
using MediaFeeder.Components;
using MediaFeeder.Components.Account;
using MediaFeeder.Components.Dialogs;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Identity;
using MediaFeeder.Helpers;
using MediaFeeder.PlaybackManager;
using MediaFeeder.Providers;
using MediaFeeder.Providers.RSS;
using MediaFeeder.Providers.Youtube;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Logging;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using MediaFeeder.Services;
using MediaFeeder.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Quartz;
using ResQueue;
using ResQueue.Enums;

var builder = WebApplication.CreateBuilder(args);

var certificatePath = builder.Configuration.GetValue<string?>("CERTIFICATE_PATH");
if (certificatePath != null)
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ConfigureEndpointDefaults(lo => lo.UseHttps(new HttpsConnectionAdapterOptions()
        {
            ServerCertificate = X509Certificate2.CreateFromPemFile(certificatePath, Path.ChangeExtension(certificatePath, "key")) //X509CertificateLoader.LoadCertificateFromFile(certificatePath)
        }));
    });

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        if (builder.Environment.IsDevelopment())
            options.DetailedErrors = true;
    })
    .AddAuthenticationStateSerialization();

builder.Services.Configure<ForwardedHeadersOptions>(static options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
});

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
if (builder.Environment.IsDevelopment())
    builder.Services.AddGrpcReflection();

builder.Services.AddGrpcClient<YTDownloader.YTDownloaderClient>(o =>
{
    o.Address = builder.Configuration.GetValue<Uri>("YTSM_DOWNLOADER_ADDRESS");
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(static options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddOpenIdConnect(
        OpenIdConnectDefaults.AuthenticationScheme,
        "Authentik", options =>
        {
            builder.Configuration.GetSection("Auth").Bind(options);
            options.Scope.Add("email");

            options.CorrelationCookie.Name = "MediaFeeder-OIDC-Correlation";
            options.NonceCookie.Name = "MediaFeeder-OIDC-Nonce";
        })
    .AddIdentityCookies();

builder.Services.AddAuthentication()
    .AddJwtBearer(
        JwtBearerDefaults.AuthenticationScheme,
        options =>
        {
            var authSettings = builder.Configuration.GetSection("Auth");
            var selfIssuer = authSettings.GetValue<string>("issuer", "MediaFeeder");

            options.TokenValidationParameters.ValidIssuers = [selfIssuer];
            options.TokenValidationParameters.ValidAudience = selfIssuer;
            options.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authSettings.GetValue<string>("secret") ??
                                                                throw new InvalidOperationException()));

            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;

            options.EventsType = typeof(AppJwtBearerEvents);
        });

builder.Services.AddSingleton<AppJwtBearerEvents>();

builder.Services.AddAuthorization(static options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
        .Build();

    options.AddPolicy("Thumbnails", static policyBuilder =>
    {
        policyBuilder
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme, JwtBearerDefaults.AuthenticationScheme)
            // .RequireAssertion(static c =>
            // {
            //     Console.WriteLine(JsonSerializer.Serialize(c.User));
            //     return c.User.Identity?.AuthenticationType == OpenIdConnectDefaults.AuthenticationScheme || c.User.IsInRole("API");
            // })
            .Build();
    });

    options.AddPolicy("API", static policyBuilder =>
    {
        policyBuilder
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            // .RequireRole("API")
            .Build();
    });
});

builder.Services.AddDataProtection()
    .SetApplicationName("MediaFeeder")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Join(builder.Configuration.GetValue<string>("MediaRoot") ?? throw new InvalidOperationException(), "dpkeys")));

builder.Services.AddPooledDbContextFactory<MediaFeederDataContext>((sp, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, static o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    options.AddInterceptors(new SlowQueryDetectionHelper(sp.GetRequiredService<ILogger<SlowQueryDetectionHelper>>()));
});

builder.Services.AddScoped<MediaFeederDataContext>(static p => p.GetRequiredService<IDbContextFactory<MediaFeederDataContext>>().CreateDbContext());

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<AuthUser>(static options => options.SignIn.RequireConfirmedAccount = true)
    .AddUserManager<UserManager>()
    .AddUserStore<UserStore>()
    .AddRoles<AuthGroup>()
    .AddRoleManager<ApplicationRoleManager>()
    .AddRoleStore<RoleStore>()
    .AddEntityFrameworkStores<MediaFeederDataContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<AuthUser>, IdentityNoOpEmailSender>();

var useDatabaseMessageQueue = builder.Configuration.GetValue("UseDatabaseMessageQueue", false);

builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService();

if (useDatabaseMessageQueue)
{
    builder.Services.AddOptions<SqlTransportOptions>().Configure(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    });
    builder.Services.AddPostgresMigrationHostedService();
}

builder.Services.AddMassTransit(config =>
{
    config.AddOpenTelemetry();

    var schedulerEndpoint = new Uri("queue:scheduler");

    config.AddMessageScheduler(schedulerEndpoint);

    if (useDatabaseMessageQueue)
    {
        config.UsingPostgres((context, cfg) => ConfigureMessageQueue(context, cfg, schedulerEndpoint));
    }
    else
    {
        config.UsingInMemory((context, cfg) => ConfigureMessageQueue(context, cfg, schedulerEndpoint));
    }

    config.AddConsumer<SynchroniseAllConsumer>();

    config.AddConsumer<YoutubeSubscriptionSynchroniseConsumer>();
    config.AddConsumer<YoutubeVideoSynchroniseConsumer>();
    config.AddConsumer<YoutubeActualVideoSynchroniseConsumer>();
    config.AddConsumer<YouTubeDownloadVideoConsumer>();

    config.AddConsumer<RSSSubscriptionSynchroniseConsumer>();
});

if (useDatabaseMessageQueue)
{
    builder.Services.AddResQueue(static o => o.SqlEngine = ResQueueSqlEngine.Postgres);
    builder.Services.AddResQueueMigrationsHostedService();
}

builder.Logging.AddOpenTelemetry(static logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddSingleton<Metrics>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(static r =>
    {
        r.AddService("MediaFeeder",
            serviceVersion: FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion,
            serviceInstanceId: Environment.MachineName);
        r.AddContainerDetector()
            .AddEnvironmentVariableDetector()
            .AddHostDetector()
            .AddOperatingSystemDetector()
            .AddProcessDetector()
            .AddProcessRuntimeDetector()
            .AddTelemetrySdk();
    })
    .WithMetrics(static (metrics) =>
    {
        metrics.AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddAspNetCoreInstrumentation()
            // .AddHttpClientInstrumentation()
            .AddNpgsqlInstrumentation()
            .AddMeter(MassTransit.Monitoring.InstrumentationOptions.MeterName)
            .AddMeter(Metrics.MeterName)
            .AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        if (builder.Environment.IsDevelopment())
            tracing.SetSampler(new AlwaysOnSampler());

        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddGrpcClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddNpgsql()
            .AddSource(DiagnosticHeaders.DefaultListenerName);
    });

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MediaFeederDataContext>("DB");

builder.Services.AddGrpcHealthChecks()
    .AddDbContextCheck<MediaFeederDataContext>("DB-GRPC");

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpClient("retry")
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5)));
builder.Services.AddSingleton<SystemNetClientFactory>();
builder.Services.AddScoped<IProvider, YoutubeProvider>();
builder.Services.AddScoped<Utils>();
builder.Services.AddScoped<Google.Apis.YouTube.v3.YouTubeService>(sp =>
    new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer
    {
        ApplicationName = "MediaFeeder",
        ApiKey = builder.Configuration.GetValue<string>("youtube_api_key"),
        HttpClientFactory = sp.GetRequiredService<SystemNetClientFactory>(),
    }));

builder.Services.AddScoped<IProvider, SonarrProvider>();
builder.Services.AddScoped<IProvider, RSSProvider>();

builder.Services.AddSingleton<PlaybackSessionManager>();
builder.Services.AddSingleton<TokenHelper>();

builder.Services.AddTransient<AbstractValidator<Folder>, EditFolder.Validator>();

builder.Services.AddAntDesign();
builder.Services.AddControllers();

builder.Services.AddOutputCache();

var app = builder.Build();

app.UsePathBase(app.Configuration.GetValue<string>("base_path", "/"));

app.Services.GetRequiredService<Metrics>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    IdentityModelEventSource.ShowPII = true;
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error", true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseRouting();
app.UseForwardedHeaders();

app.UseHealthChecks("/healthz");
app.MapPrometheusScrapingEndpoint()
    .AllowAnonymous();
app.MapGrpcHealthChecksService();

app.UseHttpsRedirection();
app.MapStaticAssets();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.UseOutputCache();

if (useDatabaseMessageQueue)
{
    app.UseResQueue("resqueue", static options =>
    {
        // Highly recommended for production
        options.RequireAuthorization();
    });
}

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapGrpcService<MediaToadService>().RequireAuthorization("API");
app.MapGrpcService<ApiService>().RequireAuthorization("API");

if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

await using (var context = await app.Services.GetRequiredService<IDbContextFactory<MediaFeederDataContext>>().CreateDbContextAsync())
{
    await context.Database.MigrateAsync();
}

app.Run();
return;

static void ConfigureMessageQueue<TBus>(IBusRegistrationContext context, IBusFactoryConfigurator<TBus> cfg, Uri schedulerEndpoint) where TBus : IReceiveEndpointConfigurator
{
    cfg.UseMessageScheduler(schedulerEndpoint);
    cfg.ConfigureEndpoints(context);
    cfg.UseConcurrencyLimit(1);
    cfg.UseInMemoryScheduler();
    cfg.UseMessageRetry(static r =>
        r.Exponential(15, TimeSpan.FromMinutes(15), TimeSpan.FromDays(2), TimeSpan.FromHours(1)));
    cfg.UseCircuitBreaker(static cb =>
    {
        cb.TrackingPeriod = TimeSpan.FromMinutes(5);
        cb.TripThreshold = 15;
        cb.ActiveThreshold = 10;
        cb.ResetInterval = TimeSpan.FromHours(1);
        // cb.Handle<>();
        cb.Ignore<HttpRequestException>();
    });
}

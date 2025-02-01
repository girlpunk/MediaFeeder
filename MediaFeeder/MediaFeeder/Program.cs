using System.Diagnostics;
using System.Net;
using System.Reflection;
using Google.Apis.Services;
using MassTransit;
using MassTransit.Logging;
using MediaFeeder;
using MediaFeeder.Components;
using MediaFeeder.Components.Account;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Identity;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using OpenTelemetry.Resources;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Quartz;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        if (builder.Environment.IsDevelopment())
            options.DetailedErrors = true;
    })
    .AddAuthenticationStateSerialization();

builder.Services.AddGrpc();

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
        })
    .AddIdentityCookies();


builder.Services.AddAuthorization(static options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddDataProtection()
    .SetApplicationName("MediaFeeder")
    .PersistKeysToFileSystem(new DirectoryInfo(@"/media/dpkeys/"));

builder.Services.AddDbContextFactory<MediaFeederDataContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, static o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
});

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

builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService();
builder.Services.AddMassTransit(static config =>
{
    config.AddOpenTelemetry();

    var schedulerEndpoint = new Uri("queue:scheduler");

    config.AddMessageScheduler(schedulerEndpoint);
    config.UsingInMemory((context, cfg) =>
    {
        cfg.UseMessageScheduler(schedulerEndpoint);
        cfg.ConfigureEndpoints(context);
    });

    config.AddConsumer<SynchroniseAllConsumer>();

    config.AddConsumer<YoutubeSubscriptionSynchroniseConsumer>();
    config.AddConsumer<YoutubeVideoSynchroniseConsumer>();
    config.AddConsumer<YoutubeActualVideoSynchroniseConsumer>();

    config.AddConsumer<RSSSubscriptionSynchroniseConsumer>();
});

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

builder.Services.AddAntDesign();
builder.Services.AddControllers();

var app = builder.Build();

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

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.All,
    KnownNetworks = { new IPNetwork(new IPAddress(0), 0) }
});

app.UseHealthChecks("/healthz");
app.MapPrometheusScrapingEndpoint()
    .AllowAnonymous();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

var cacheMaxAgeOneWeek = (60 * 60 * 24 * 7).ToString();

var mediaRoot = app.Configuration.GetValue<string>("MediaRoot");
if (mediaRoot != null)
    app.UseStaticFiles(new StaticFileOptions()
    {
        FileProvider = new PhysicalFileProvider(mediaRoot),
        RequestPath = new PathString("/media"),
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append(
                "Cache-Control", $"public, max-age={cacheMaxAgeOneWeek}");
        }
    });

var tvRoot = app.Configuration.GetValue<string>("TVRoot");
if (tvRoot != null)
    app.UseStaticFiles(new StaticFileOptions()
    {
        FileProvider = new PhysicalFileProvider(tvRoot),
        RequestPath = new PathString("/tv"),
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.Append(
                "Cache-Control", $"public, max-age={cacheMaxAgeOneWeek}");
        }
    });

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapGrpcService<MediaToadService>();
app.MapGrpcHealthChecksService();

app.Run();

using System.Net;
using MediaFeeder;
using MediaFeeder.Components;
using MediaFeeder.Components.Account;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Identity;
using MediaFeeder.Providers;
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
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
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
            //options.MetadataAddress =  "https://authentik.home.foxocube.xyz/application/o/mediafeeder/";
            //options.ClientId = "bz2cp1KiWnFio7N7rGp3wtcuehbhWc17sSE9Nedk";
            options.Scope.Add("email");
        })
    .AddIdentityCookies();

// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options =>
//     {
//         builder.Configuration.GetSection("Auth").Bind(options);
//     });

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

builder.Logging.AddOpenTelemetry(static logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(static metrics =>
    {
        metrics.AddRuntimeInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        if (builder.Environment.IsDevelopment())
            tracing.SetSampler(new AlwaysOnSampler());

        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddNpgsql();
    });

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MediaFeederDataContext>("DB");

builder.Services.AddGrpcHealthChecks()
    .AddDbContextCheck<MediaFeederDataContext>("DB-GRPC");

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();


builder.Services.AddScoped<IProvider, YoutubeProvider>();
builder.Services.AddScoped<IProvider, SonarrProvider>();
builder.Services.AddScoped<IProvider, RSSProvider>();

// builder.Services.AddFluentUIComponents(options =>
// {
//     options.ValidateClassNames = false;
// });

builder.Services.AddAntDesign();
builder.Services.AddControllers();

var app = builder.Build();

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
app.MapPrometheusScrapingEndpoint();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

var mediaRoot = app.Configuration.GetValue<string>("MediaRoot");
if (mediaRoot != null)
    app.UseStaticFiles(new StaticFileOptions()
    {
        FileProvider = new PhysicalFileProvider(mediaRoot),
        RequestPath = new PathString("/media")
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

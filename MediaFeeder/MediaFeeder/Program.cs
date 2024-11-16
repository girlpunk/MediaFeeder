using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MediaFeeder.Client.Pages;
using MediaFeeder.Components;
using MediaFeeder.Components.Account;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.FluentUI.AspNetCore.Components;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddOpenIdConnect(
        OpenIdConnectDefaults.AuthenticationScheme,
        "Authentik",
        options =>
    {
        options.MetadataAddress = "https://authentik.home.foxocube.xyz/application/o/mediafeeder/";
        options.ClientId = "bz2cp1KiWnFio7N7rGp3wtcuehbhWc17sSE9Nedk";
        options.Scope.Add("email");
    })
    .AddIdentityCookies();

// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options =>
//     {
//         builder.Configuration.GetSection("Auth").Bind(options);
//     });

builder.Services.AddDbContextFactory<MediaFeederDataContext>(options => {
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<AuthUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddUserManager<UserManager>()
    .AddUserStore<UserStore>()
    .AddRoles<AuthGroup>()
    .AddRoleManager<ApplicationRoleManager>()
    .AddRoleStore<RoleStore>()
    .AddEntityFrameworkStores<MediaFeederDataContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<AuthUser>, IdentityNoOpEmailSender>();

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
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
    .AddDbContextCheck<MediaFeederDataContext>();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddFluentUIComponents(options =>
{
    options.ValidateClassNames = false;
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHealthChecks("/healthz");
app.MapPrometheusScrapingEndpoint();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MediaFeeder.Client._Imports).Assembly);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();

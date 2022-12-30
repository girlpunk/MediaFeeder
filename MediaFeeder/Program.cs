using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MediaFeeder.Areas.Identity;
using MediaFeeder.Data;
using MediaFeeder.Data.Identity;
using MediaFeeder.Models;
using MediaFeeder.Models.db;
using MediaFeeder.Providers.Youtube;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("MEDIAFEEDER_");

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// builder.Services.AddDbContext<MediaFeederDataContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContextFactory<MediaFeederDataContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<AuthUser, AuthGroup>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddDefaultTokenProviders()
    .AddSignInManager()
    .AddDefaultUI();

builder.Services.AddTransient<IUserStore<AuthUser>, UserStore>();
builder.Services.AddTransient<IRoleStore<AuthGroup>, RoleStore>();
builder.Services.AddTransient<IUserRoleStore<AuthUser>, UserRoleStore>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<UserManager<AuthUser>, UserManager>();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddOpenIdConnect(
        OpenIdConnectDefaults.AuthenticationScheme,
        "Authentik",
        options =>
        {
            options.MetadataAddress =
                "https://authentik.home.foxocube.xyz/application/o/mediafeeder-dev/.well-known/openid-configuration";
            options.ClientId = "f4d8ca51f4a5bd6e5ccb620d5ce12fe42ad466ad";
            options.ClientSecret = "e236b78c8a5d28fb3c4bbe2750cb1da103436d53d212a9744db35815db92b9b28396caa6a41d6ef9104e47ecd1907382f49301b7bf503abb314f7fd4a7cbcbbb";
            options.ResponseType = "code";
            options.SaveTokens = true;
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.UsePkce = true;
            options.CallbackPath = new PathString("/signin-oidc");
            options.Authority = "https://authentik.home.foxocube.xyz/";
        }
    );

builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();

builder.Services.AddSingleton<IProvider, YoutubeProvider>();

var app = builder.Build();

await using (var context = await app.Services.GetRequiredService<IDbContextFactory<MediaFeederDataContext>>().CreateDbContextAsync())
{
    await context.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto
    });

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

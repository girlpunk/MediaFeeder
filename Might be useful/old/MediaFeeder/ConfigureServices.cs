using MediaFeeder.Data;
using MediaFeeder.Models.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.Design;
using System.Net.Http.Headers;
using System.Reflection;
using FluentValidation;
using Hangfire;
using Hangfire.MemoryStorage;
using MediaFeeder.Areas.Identity;
using MediaFeeder.Behaviours;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using MediaFeeder.Data.Identity;
using MediaFeeder.Filters;
using MediaFeeder.Services;
using MediatR.Pipeline;
using WorkflowCore.Interface;

namespace MediaFeeder;

public static class ConfigureServices
{
    public static IServiceCollection AddRazorServices(this IServiceCollection services, ConfigurationManager configuration)
    {
        services.AddHealthChecks();
        services.AddControllers();
        services.AddRazorPages(/*options => {
                options.Conventions.AddPageRoute("/AspNetCore/Welcome", "");
            }*/)
            .AddMvcOptions(options =>
            {
                options.Filters.Add<ApiExceptionFilterAttribute>();
            })
            .AddViewLocalization()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null;

            })
            .AddRazorRuntimeCompilation();
        services.AddSignalR();
        return services;
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<MediaFeederDataContext>(options => 
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(MediaFeederDataContext).Assembly.FullName))
            );
        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddSingleton<CurrentUserService>();
        // services.AddScoped<IApplicationDbContext>(provider => provider.GetService<MediaFeederDataContext>());
        services.AddScoped<DomainEventService>();

        services.AddIdentity<AuthUser, AuthGroup>()
            .AddDefaultTokenProviders();
        // .AddSignInManager()
        // .AddDefaultUI();

        services.AddTransient<IUserStore<AuthUser>, UserStore>();
        services.AddTransient<IRoleStore<AuthGroup>, RoleStore>();
        services.AddTransient<IUserRoleStore<AuthUser>, UserRoleStore>();
        services.AddTransient<UserManager<AuthUser>, UserManager>();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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

        services.Configure<IdentityOptions>(options =>
        {
            // Default SignIn settings.
            options.SignIn.RequireConfirmedAccount = true;
            // Default Lockout settings.
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        });

        services.ConfigureApplicationCookie(options => {
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.LoginPath = "/Identity/Account/Login";
            options.LogoutPath = "/Identity/Account/Logout";
            options.AccessDeniedPath = "/Identity/Account/AccessDenied";
        });
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromDays(30));

        services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();

        // services.AddLocalization(options => options.ResourcesPath = LocalizationConstants.ResourcesPath);
        // services.AddScoped<RequestLocalizationCookiesMiddleware>();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            // options.AddSupportedUICultures(LocalizationConstants.SupportedLanguages.Select(x => x.Code).ToArray());
            options.FallBackToParentUICultures = true;
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediatR(config => {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            config.AddOpenBehavior(typeof(RequestExceptionProcessorBehavior<,>));
            config.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
            config.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            // config.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));
            config.AddOpenBehavior(typeof(CachingBehaviour<,>));
            config.AddOpenBehavior(typeof(CacheInvalidationBehaviour<,>));
            config.AddOpenBehavior(typeof(PerformanceBehaviour<,>));


        });

        services.AddLazyCache();
        services.AddHangfire(options =>
        {
            options.UseMemoryStorage();
        });

        services.AddHangfireServer(options => {
            options.WorkerCount = 1;
        });

        // services.AddHttpClient("ocr", c =>
        //     {
        //         c.BaseAddress = new Uri("https://paddleocr.i247365.net/predict/ocr_system");
        //         c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //     })
        //     .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(1000))); ;
        return services;
    }

    public static IServiceCollection AddWorkflow(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddWorkflow(x => x.UsePostgreSQL(configuration.GetConnectionString("DefaultConnection"), true, true));

        return services;
    }

    public static IApplicationBuilder UseWorkflow(this IApplicationBuilder app)
    {
        var host = app.ApplicationServices.GetService<IWorkflowHost>();
        // host.RegisterWorkflow<DocmentApprovalWorkflow, ApprovalData>();
        host.Start();
        return app;
    }
}

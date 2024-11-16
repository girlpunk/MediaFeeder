using MediaFeeder.API;
using MediaFeeder.API.Models.db;
using MediaFeeder.API.Models.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

const string myAllowSpecificOrigins = "_myAllowSpecificOrigins";

var builder = WebApplication.CreateBuilder(args);

IdentityModelEventSource.ShowPII = true;

builder.Services.AddCors(options =>
{
    options.AddPolicy(myAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});


builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddRuntimeInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        if (builder.Environment.IsDevelopment())
            // We want to view all traces in development
            tracing.SetSampler(new AlwaysOnSampler());

        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MediaFeederDataContext>();

builder.Services.AddProblemDetails();

builder.Services.AddDbContext<MediaFeederDataContext>(options => {
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

builder.Services.AddIdentityCore<AuthUser>()
    .AddUserManager<UserManager>()
    .AddUserStore<UserStore>()
    .AddRoles<AuthGroup>()
    .AddRoleManager<ApplicationRoleManager>()
    .AddRoleStore<RoleStore>()
    .AddEntityFrameworkStores<MediaFeederDataContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        builder.Configuration.GetSection("Auth").Bind(options);
    });



builder.Services.AddAuthorization();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo());

    var securityDefinition = new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        In = ParameterLocation.Header,
        OpenIdConnectUrl = builder.Configuration.GetSection("Auth").GetValue<Uri>("MetadataAddress")
    };

    c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityDefinition);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement() {
    {
        securityDefinition, Array.Empty<string>()
    }});
});

var app = builder.Build();

app.UseCors(myAllowSpecificOrigins);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.OAuthConfigObject.ClientId = builder.Configuration.GetSection("Auth").GetValue<string>("audience");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseExceptionHandler();

app.MapPrometheusScrapingEndpoint();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();

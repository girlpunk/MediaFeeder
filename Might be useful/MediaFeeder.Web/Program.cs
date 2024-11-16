using MediaFeeder.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.FluentUI.AspNetCore.Components;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddRuntimeInstrumentation()
            .AddMeter(
                "Microsoft.AspNetCore.Hosting",
                "Microsoft.AspNetCore.Server.Kestrel",
                "System.Net.Http");
    })
    .WithTracing(tracing =>
    {
        if (builder.HostEnvironment.IsDevelopment())
            // We want to view all traces in development
            tracing.SetSampler(new AlwaysOnSampler());

        tracing.AddHttpClientInstrumentation();
    });

var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

if (useOtlpExporter)
{
    builder.Services.Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter());
    builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
    builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
}

builder.Services.ConfigureHttpClientDefaults(http =>
{
    // Turn on resilience by default
    http.AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout = new HttpTimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        o.TotalRequestTimeout = new HttpTimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromMinutes(2.5)
        };
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(1);
    });
});

builder.Services.AddScoped<MediaFeederAuthorizationMessageHandler>();

builder.Services.AddHttpClient<FolderApiClient>(client => client.BaseAddress = builder.Configuration.GetValue<Uri>("api"))
    .AddHttpMessageHandler<MediaFeederAuthorizationMessageHandler>();

builder.Services.AddHttpClient<SubscriptionApiClient>(client => client.BaseAddress = builder.Configuration.GetValue<Uri>("api"))
    .AddHttpMessageHandler<MediaFeederAuthorizationMessageHandler>();

builder.Services.AddOidcAuthentication(options =>
{
    // Configure your authentication provider options here.
    // For more information, see https://aka.ms/blazor-standalone-auth
    builder.Configuration.Bind("Local", options.ProviderOptions);
});

builder.Services.AddFluentUIComponents();

await builder.Build().RunAsync().ConfigureAwait(false);

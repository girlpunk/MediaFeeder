using Hangfire;
using Microsoft.EntityFrameworkCore;
using MediaFeeder.Data;
using MediaFeeder.Models;
using MediaFeeder.Providers.Youtube;
using Microsoft.AspNetCore.HttpOverrides;
using MediaFeeder;
using MediaFeeder.Hubs;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("MEDIAFEEDER_");

builder.Services.AddRazorServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddWorkflow(builder.Configuration);

// builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IProvider, YoutubeProvider>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseHsts();
    
}

await using (var scope = app.Services.CreateAsyncScope())
await using (var context = scope.ServiceProvider.GetRequiredService<MediaFeederDataContext>())
{
    await context.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
// app.UseExceptionHandler("/Error");
// app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire/index");
app.UseWorkflow();

app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto
    });

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapRazorPages();
    endpoints.MapHub<SignalRHub>(SignalR.HubUrl);
});

await app.RunAsync();

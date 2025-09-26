using MediaFeeder.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Diagnostics;
using MassTransit;
using MediaFeeder.Helpers;
using MediaFeeder.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace MediaFeeder.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatsController(IDbContextFactory<MediaFeederDataContext> contextFactory) : ControllerBase
{
    // GET: api/<StatsController>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get()
    {
        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var lastHour = DateTime.UtcNow - TimeSpan.FromHours(1);
        var newPublished = await context.Videos.CountAsync(video => video.PublishDate >= lastHour);
        var newWatched   = await context.Videos.CountAsync(video => video.Watched && video.WatchedDate >= lastHour);

        var subscriptions = await context.Subscriptions.CountAsync();
        var trackedVideos = await context.Videos.CountAsync();
        var watchedVideos = await context.Videos.CountAsync(static video => video.Watched);
        var newUnwatched = await context.Videos.CountAsync(static video => !video.Watched);

        var folders = await context.Videos
            .GroupBy(static video => video.Subscription!.ParentFolderId)
            .Select(static group => new
            {
                group.Key,
                group.First().Subscription!.ParentFolder!.Name,
                Tracked = group.Count(),
                UnwatchedCount = group.Count(static video => !video.Watched),
                UnwatchedDuration = group
                    .Where(static video => !video.Watched)
                    .Sum(static video => video.Duration)
            })
            .ToDictionaryAsync(static group => group.Key, static group => group);

        return Ok(new
        {
            Published = newPublished,
            Subscriptions = subscriptions,
            VideosTracked = trackedVideos,
            VideosWatched = watchedVideos,
            VideosUnwatched = newUnwatched,
            Folders = folders,
            Version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion,
        });
    }

    [HttpGet("sync")]
    [AllowAnonymous]
    public async Task<IActionResult> Sync([FromServices] IBus bus)
    {
        var contract = new SynchroniseAllContract();

        await bus.PublishWithGuid(contract, HttpContext.RequestAborted);

        return Ok();
    }
}

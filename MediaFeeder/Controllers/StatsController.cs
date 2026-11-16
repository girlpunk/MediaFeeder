namespace MediaFeeder.Controllers;

using System.Diagnostics;
using System.Reflection;
using Data;
using Data.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tasks;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces.Managers;

[Route("api/[controller]")]
[ApiController]
public class StatsController(IDbContextFactory<MediaFeederDataContext> contextFactory)
    : ControllerBase
{
    /// <summary>
    /// GET: api/Stats
    /// </summary>
    /// <returns>System Statistics</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<StatsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StatsResponse>> Get()
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        var lastHour = DateTimeOffset.UtcNow - TimeSpan.FromHours(1);
        var newPublished = await context.Videos.CountAsync(video => video.PublishDate >= lastHour);
        var newWatched = await context.Videos.CountAsync(video =>
            video.Watched && video.WatchedDate >= lastHour
        );

        var subscriptions = await context.Subscriptions.AsNoTracking().CountAsync();
        var trackedVideos = await context.Videos.AsNoTracking().CountAsync();
        var watchedVideos = await context.Videos.AsNoTracking().CountAsync(static video => video.Watched);
        var newUnwatched = await context.Videos.AsNoTracking().CountAsync(static video => !video.Watched);

        var folders = await context.Videos.AsNoTracking()
            .GroupBy(static video => video.Subscription!.ParentFolderId)
            .Select(static group => new StatsFolder(
                    group.Key,
                    group.First().Subscription!.ParentFolder!.Name,
                    Tracked: group.Count(),
                    UnwatchedCount: group.Count(static video => !video.Watched),
                    UnwatchedDuration: group
                        .Where(static video => !video.Watched)
                        .Sum(static video => video.Duration)
                )
            )
            .ToDictionaryAsync(static group => group.Key, static group => group);

        var results = new StatsResponse(
            Published: newPublished,
            Watched: newWatched,
            Subscriptions: subscriptions,
            VideosTracked: trackedVideos,
            VideosWatched: watchedVideos,
            VideosUnwatched: newUnwatched,
            Folders: folders,
            Version: FileVersionInfo
                .GetVersionInfo(Assembly.GetExecutingAssembly().Location)
                .ProductVersion
        );
        return Ok(
            results
        );
    }

    [HttpGet("sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [AllowAnonymous]
    public async Task<IActionResult> Sync([FromServices] ITimeTickerManager<TimeTickerEntity> timeTicker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeTicker);

        await timeTicker.AddAsync<SynchroniseAllConsumer>(DateTime.Now, cancellationToken);

        return Ok();
    }
}

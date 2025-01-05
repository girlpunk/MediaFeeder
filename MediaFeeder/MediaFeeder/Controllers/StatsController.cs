using MediaFeeder.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Diagnostics;
using MassTransit;
using MediaFeeder.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace MediaFeeder.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatsController(MediaFeederDataContext context) : ControllerBase
{
    private MediaFeederDataContext Context { get; } = context;

    // GET: api/<StatsController>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var lastHour = DateTime.UtcNow - TimeSpan.FromHours(1);
        var newPublished = await Context.Videos.CountAsync(video => video.PublishDate >= lastHour);

        var subscriptions = await Context.Subscriptions.CountAsync();
        var trackedVideos = await Context.Videos.CountAsync();
        var watchedVideos = await Context.Videos.CountAsync(static video => video.Watched);
        var newUnwatched = await Context.Videos.CountAsync(static video => !video.Watched);

        var folders = await Context.Videos
            .GroupBy(static video => video.Subscription.ParentFolderId)
            .Select(static group => new
            {
                Key = group.Key ?? -1,
                group.First().Subscription.ParentFolder.Name,
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
    public async Task<IActionResult> Sync([FromServices] IBus bus)
    {
        var contract = new SynchroniseAllContract();

        await bus.Send(contract);

        return Ok();
    }
}

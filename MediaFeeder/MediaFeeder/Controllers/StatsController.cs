using MediaFeeder.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        var newPublished = await Context.YtManagerAppVideos.CountAsync(video => video.PublishDate >= lastHour);

        var subscriptions = await Context.YtManagerAppSubscriptions.CountAsync();
        var trackedVideos = await Context.YtManagerAppVideos.CountAsync();
        var watchedVideos = await Context.YtManagerAppVideos.CountAsync(video => video.Watched);
        var newUnwatched = await Context.YtManagerAppVideos.CountAsync(video => !video.Watched);

        var folders = await Context.YtManagerAppVideos
            .GroupBy(video => video.Subscription.ParentFolderId)
            .Select(group => new
            {
                Key = group.Key ?? -1,
                group.First().Subscription.ParentFolder.Name,
                Tracked = group.Count(),
                UnwatchedCount = group.Count(video => !video.Watched),
                UnwatchedDuration = group
                    .Where(video => !video.Watched)
                    .Sum(video => video.Duration)
            })
            .ToDictionaryAsync(group => group.Key, group => group);

        return Ok(new
        {
            Published = newPublished,
            Subscriptions = subscriptions,
            VideosTracked = trackedVideos,
            VideosWatched = watchedVideos,
            VideosUnwatched = newUnwatched,
            Folders = folders
        });
    }
}
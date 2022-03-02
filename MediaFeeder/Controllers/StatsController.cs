using MediaFeeder.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace MediaFeeder.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatsController : ControllerBase
    {
        private IDbContextFactory<MediaFeederDataContext> _contextFactory { get; set; }

        public StatsController(IDbContextFactory<MediaFeederDataContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // GET: api/<StatsController>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            using var db = await _contextFactory.CreateDbContextAsync();

            var lastHour = DateTime.UtcNow - TimeSpan.FromHours(1);
            var new_published = await db.YtManagerAppVideos.CountAsync(video => video.PublishDate >= lastHour);
            
            var subscriptions = await db.YtManagerAppSubscriptions.CountAsync();
            var tracked_videos = await db.YtManagerAppVideos.CountAsync();
            var watched_videos = await db.YtManagerAppVideos.CountAsync(video => video.Watched);
            var new_unwatched = await db.YtManagerAppVideos.CountAsync(video => !video.Watched);

            var folders = await db.YtManagerAppVideos
                .GroupBy(video => video.Subscription.ParentFolderId)
                .Select(group => new {
                    Key = group.Key ?? -1,
                    Name = group.First().Subscription.ParentFolder.Name,
                    Tracked = group.Count(),
                    UnwatchedCount = group.Count(video => !video.Watched),
                    UnwatchedDuration = group
                        .Where(video => !video.Watched)
                        .Sum(video => video.Duration)
                })
                .ToDictionaryAsync(group => group.Key, group => group);

            return Ok(new
            {
                Published = new_published,
                Subscriptions = subscriptions,
                VideosTracked = tracked_videos,
                VideosWatched = watched_videos,
                VideosUnwatched = new_unwatched,
                Folders = folders
            });
        }
    }
}

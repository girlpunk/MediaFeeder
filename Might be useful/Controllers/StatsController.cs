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
        private MediaFeederDataContext _context { get; set; }

        public StatsController(MediaFeederDataContext context)
        {
            _context = context;
        }

        // GET: api/<StatsController>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var lastHour = DateTime.UtcNow - TimeSpan.FromHours(1);
            var new_published = await _context.YtManagerAppVideos.CountAsync(video => video.PublishDate >= lastHour);
            
            var subscriptions = await _context.YtManagerAppSubscriptions.CountAsync();
            var tracked_videos = await _context.YtManagerAppVideos.CountAsync();
            var watched_videos = await _context.YtManagerAppVideos.CountAsync(video => video.Watched);
            var new_unwatched = await _context.YtManagerAppVideos.CountAsync(video => !video.Watched);

            var folders = await _context.YtManagerAppVideos
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

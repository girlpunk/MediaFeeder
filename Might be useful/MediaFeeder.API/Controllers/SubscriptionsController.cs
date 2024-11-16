using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediaFeeder.API.Models.db;
using MediaFeeder.API.Models.Identity;
using MediaFeeder.DTOs.DTOs;

namespace MediaFeeder.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubscriptionsController(MediaFeederDataContext context, UserManager userManager) : ControllerBase
    {
        // GET: api/Subscriptions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<YtManagerAppSubscription>>> GetYtManagerAppSubscriptions()
        {
            return await context.YtManagerAppSubscriptions.ToListAsync(HttpContext.RequestAborted);
        }

        // GET: api/Subscriptions/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SubscriptionGet>> GetYtManagerAppSubscription(int id)
        {
            var ytManagerAppSubscription = await context.YtManagerAppSubscriptions.FindAsync([id], HttpContext.RequestAborted);

            if (ytManagerAppSubscription == null)
                return NotFound();

            var user = await userManager.GetUserAsync(HttpContext.User);
            if (user == null)
                return Forbid();

            if (ytManagerAppSubscription.UserId != user.Id)
                return Unauthorized();

            return new SubscriptionGet
            {
                Name = ytManagerAppSubscription.Name,
                Id = ytManagerAppSubscription.Id,
                Thumb = ytManagerAppSubscription.Thumb,
                Thumbnail = ytManagerAppSubscription.Thumbnail,
                Unwatched = ytManagerAppSubscription.YtManagerAppVideos.Count(v => !v.Watched),
            };
        }

        // PUT: api/Subscriptions/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutYtManagerAppSubscription(int id, YtManagerAppSubscription ytManagerAppSubscription)
        {
            if (id != ytManagerAppSubscription.Id)
            {
                return BadRequest();
            }

            context.Entry(ytManagerAppSubscription).State = EntityState.Modified;

            try
            {
                await context.SaveChangesAsync(HttpContext.RequestAborted);
            }
            catch (DbUpdateConcurrencyException) when (!YtManagerAppSubscriptionExists(id))
            {
                return NotFound();
            }

            return NoContent();
        }

        // POST: api/Subscriptions
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<YtManagerAppSubscription>> PostYtManagerAppSubscription(YtManagerAppSubscription ytManagerAppSubscription)
        {
            context.YtManagerAppSubscriptions.Add(ytManagerAppSubscription);
            await context.SaveChangesAsync(HttpContext.RequestAborted);

            return CreatedAtAction("GetYtManagerAppSubscription", new { id = ytManagerAppSubscription.Id }, ytManagerAppSubscription);
        }

        // DELETE: api/Subscriptions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteYtManagerAppSubscription(int id)
        {
            var ytManagerAppSubscription = await context.YtManagerAppSubscriptions.FindAsync([id], HttpContext.RequestAborted);
            if (ytManagerAppSubscription == null)
            {
                return NotFound();
            }

            context.YtManagerAppSubscriptions.Remove(ytManagerAppSubscription);
            await context.SaveChangesAsync(HttpContext.RequestAborted);

            return NoContent();
        }

        private bool YtManagerAppSubscriptionExists(int id)
        {
            return context.YtManagerAppSubscriptions.Any(e => e.Id == id);
        }
    }
}

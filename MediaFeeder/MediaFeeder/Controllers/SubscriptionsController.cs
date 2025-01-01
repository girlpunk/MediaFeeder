using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SubscriptionsController(MediaFeederDataContext context, UserManager userManager) : ControllerBase
{
    // GET: api/Subscriptions
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Subscription>>> GetSubscriptions()
    {
        return await context.Subscriptions.ToListAsync(HttpContext.RequestAborted);
    }

    // GET: api/Subscriptions/5
    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetSubscription(int id)
    {
        var subscription =
            await context.Subscriptions
                .Include(static v => v.Videos)
                .SingleOrDefaultAsync(v => v.Id == id, HttpContext.RequestAborted);

        if (subscription == null)
            return NotFound();

        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Forbid();

        if (subscription.UserId != user.Id)
            return Unauthorized();

        return new
        {
            subscription.Name,
            subscription.Id,
            subscription.Thumb,
            subscription.Thumbnail,
            Unwatched = subscription.Videos.Count(static v => !v.Watched)
        };
    }

    // PUT: api/Subscriptions/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{id}")]
    public async Task<IActionResult> PutSubscription(int id,
        Subscription subscription)
    {
        if (id != subscription.Id) return BadRequest();

        context.Entry(subscription).State = EntityState.Modified;

        try
        {
            await context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException) when (!SubscriptionExists(id))
        {
            return NotFound();
        }

        return NoContent();
    }

    // POST: api/Subscriptions
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPost]
    public async Task<ActionResult<Subscription>> PostSubscription(
        Subscription subscription)
    {
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return CreatedAtAction("GetSubscription", new { id = subscription.Id },
            subscription);
    }

    // DELETE: api/Subscriptions/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSubscription(int id)
    {
        var subscription =
            await context.Subscriptions.FindAsync([id], HttpContext.RequestAborted);
        if (subscription == null) return NotFound();

        context.Subscriptions.Remove(subscription);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    private bool SubscriptionExists(int id)
    {
        return context.Subscriptions.Any(e => e.Id == id);
    }
}
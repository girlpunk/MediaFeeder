using System.Net;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders.Physical;

namespace MediaFeeder.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SubscriptionsController(IDbContextFactory<MediaFeederDataContext> contextFactory, UserManager userManager, IConfiguration configuration) : ControllerBase
{
    // GET: api/Subscriptions
    [HttpGet]
    [Authorize(Policy = "API")]
    public async Task<ActionResult<IEnumerable<Subscription>>> GetSubscriptions()
    {
        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);
        return await context.Subscriptions.ToListAsync(HttpContext.RequestAborted);
    }

    // GET: api/Subscriptions/5
    [HttpGet("{id}")]
    [Authorize(Policy = "API")]
    public async Task<ActionResult<object>> GetSubscription(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

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
            Unwatched = subscription.Videos.Count(static v => !v.Watched)
        };
    }

    // PUT: api/Subscriptions/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{id}")]
    [Authorize(Policy = "API")]
    public async Task<IActionResult> PutSubscription(int id,
        Subscription subscription)
    {
        if (id != subscription.Id) return BadRequest();

        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        context.Entry(subscription).State = EntityState.Modified;

        try
        {
            await context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await SubscriptionExists(id))
                return NotFound();

            throw;
        }

        return NoContent();
    }

    // POST: api/Subscriptions
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPost]
    [Authorize(Policy = "API")]
    public async Task<ActionResult<Subscription>> PostSubscription(
        Subscription subscription)
    {
        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return CreatedAtAction("GetSubscription", new { id = subscription.Id },
            subscription);
    }

    // DELETE: api/Subscriptions/5
    [HttpDelete("{id}")]
    [Authorize(Policy = "API")]
    public async Task<IActionResult> DeleteSubscription(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var subscription =
            await context.Subscriptions.FindAsync([id], HttpContext.RequestAborted);
        if (subscription == null) return NotFound();

        context.Subscriptions.Remove(subscription);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    private async Task<bool> SubscriptionExists(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);
        return context.Subscriptions.Any(e => e.Id == id);
    }

    [HttpGet("{id:int}/thumbnail")]
    [OutputCache(Duration = 60 * 60 * 24)]
    [ResponseCache(Duration = 60 * 60 * 24)]
    [Authorize(Policy = "Thumbnails")]
    public async Task<IActionResult> Thumbnail(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        ArgumentNullException.ThrowIfNull(user);

        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var subscription = await context.Subscriptions.SingleOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, HttpContext.RequestAborted);

        if (subscription == null)
            return NotFound();

        if (subscription.Thumb == null)
            return StatusCode((int)HttpStatusCode.PreconditionFailed, "Not Downloaded");

        if (subscription.Thumb.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return Redirect(subscription.Thumb);

        try
        {
            var relativePath = subscription.Thumb;
            var absolutePath = Path.Join(configuration.GetValue<string>("MediaRoot"), relativePath);
            var stream = new PhysicalFileInfo(new FileInfo(absolutePath)).CreateReadStream();
            new FileExtensionContentTypeProvider().TryGetContentType(subscription.Thumb, out var mimeType);
            return File(stream, mimeType ?? "application/octet-stream");
        }
        catch (IOException)
        {
            if (System.IO.File.Exists(subscription.Thumb))
                System.IO.File.Delete(subscription.Thumb);

            subscription.Thumb = null;
            await context.SaveChangesAsync(HttpContext.RequestAborted);

            return StatusCode((int)HttpStatusCode.PreconditionFailed, "Not Downloaded");
        }
    }
}
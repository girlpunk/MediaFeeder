using System.Net;
using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders.Physical;

namespace MediaFeeder.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "API", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SubscriptionsController(MediaFeederDataContext context, UserManager userManager) : ControllerBase
{
    // GET: api/Subscriptions
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Subscription>>> GetSubscriptions() => await context.Subscriptions.ToListAsync(HttpContext.RequestAborted);

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

    [HttpGet("{id:int}/thumbnail")]
    [OutputCache(Duration = 60 * 60 * 24)]
    [ResponseCache(Duration = 60 * 60 * 24)]
    public async Task<IActionResult> Thumbnail(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        ArgumentNullException.ThrowIfNull(user);

        var subscription = await context.Subscriptions.SingleOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, HttpContext.RequestAborted);

        if (subscription == null)
            return NotFound();

        if (subscription.Thumb == null)
            return StatusCode((int)HttpStatusCode.PreconditionFailed, "Not Downloaded");

        if (subscription.Thumb.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return Redirect(subscription.Thumb);

        try
        {
            var stream = (new PhysicalFileInfo(new FileInfo(subscription.Thumb))).CreateReadStream();
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
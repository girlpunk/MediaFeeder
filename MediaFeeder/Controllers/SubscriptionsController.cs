namespace MediaFeeder.Controllers;

using System.Net;
using Data;
using Data.db;
using Data.DTOs;
using Data.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders.Physical;

[Route("api/[controller]")]
[ApiController]
public class SubscriptionsController(
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    UserManager userManager,
    IConfiguration configuration
) : ControllerBase
{
    /// <summary>
    /// GET: api/Subscriptions
    /// </summary>
    /// <returns>List of Subscriptions</returns>
    [HttpGet]
    [Authorize(Policy = "API")]
    [ProducesResponseType<IEnumerable<Subscription>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Subscription>>> GetSubscriptions()
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );
        return await context.Subscriptions.Where(s => s.User == user).ToListAsync(HttpContext.RequestAborted);
    }

    /// <summary>
    /// GET: api/Subscriptions/5
    /// </summary>
    /// <param name="id">ID of the subscription</param>
    /// <returns>Subscription Details</returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = "API")]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponse>> GetSubscription(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        var subscription = await context
            .Subscriptions.Include(static v => v.Videos)
            .SingleOrDefaultAsync(v => v.Id == id && v.User == user, HttpContext.RequestAborted);

        if (subscription == null)
            return NotFound();

        return new SubscriptionResponse(
            subscription.Name,
            subscription.Id,
            Unwatched: subscription.Videos.Count(static v => !v.Watched)
        );
    }

    /// <summary>
    /// PUT: api/Subscriptions/5
    /// </summary>
    /// <param name="id">ID of the subscription</param>
    /// <param name="subscription">Subscription Details</param>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "API")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PutSubscription(int id, Subscription subscription)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        var dbSubscription = await context
            .Subscriptions.Include(static v => v.Videos)
            .SingleOrDefaultAsync(v => v.Id == id && v.User == user, HttpContext.RequestAborted);

        if (dbSubscription == null)
            return NotFound();

        ArgumentNullException.ThrowIfNull(subscription);

        if (id != subscription.Id)
            return BadRequest();

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

    /// <summary>
    /// POST: api/Subscriptions
    /// </summary>
    /// <param name="subscription">Subscription Details</param>
    [HttpPost]
    [Authorize(Policy = "API")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<Subscription>(StatusCodes.Status201Created)]
    public async Task<ActionResult<Subscription>> PostSubscription(Subscription subscription)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        ArgumentNullException.ThrowIfNull(subscription);

        if (subscription.UserId != user.Id)
            return Forbid();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return CreatedAtAction("GetSubscription", new { id = subscription.Id }, subscription);
    }

    /// <summary>
    /// DELETE: api/Subscriptions/5
    /// </summary>
    /// <param name="id">ID of the subscription</param>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "API")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSubscription(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        var subscription = await context.Subscriptions.Where(s => s.Id == id && s.User == user)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);
        if (subscription == null)
            return NotFound();

        context.Subscriptions.Remove(subscription);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    /// <summary>
    /// Check if a subscription exists
    /// </summary>
    /// <param name="id">ID of the subscription</param>
    private async Task<bool> SubscriptionExists(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );
        return context.Subscriptions.Any(e => e.Id == id);
    }

    [HttpGet("{id:int}/thumbnail")]
    [OutputCache(Duration = 60 * 60 * 24)]
    [ResponseCache(Duration = 60 * 60 * 24)]
    [Authorize(Policy = "Thumbnails")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Thumbnail(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        var subscription = await context.Subscriptions.SingleOrDefaultAsync(
            s => s.Id == id && s.UserId == user.Id,
            HttpContext.RequestAborted
        );

        if (subscription == null)
            return NotFound();

        if (subscription.Thumb == null)
            return StatusCode((int) HttpStatusCode.PreconditionFailed, "Not Downloaded");

        if (subscription.Thumb.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return Redirect(subscription.Thumb);

        try
        {
            var relativePath = subscription.Thumb;
            var absolutePath = Path.Join(configuration.GetValue<string>("MediaRoot"), relativePath);
            var stream = new PhysicalFileInfo(new FileInfo(absolutePath)).CreateReadStream();
            new FileExtensionContentTypeProvider().TryGetContentType(
                subscription.Thumb,
                out var mimeType
            );
            return File(stream, mimeType ?? "application/octet-stream");
        }
        catch (IOException)
        {
            if (System.IO.File.Exists(subscription.Thumb))
                System.IO.File.Delete(subscription.Thumb);

            subscription.Thumb = null;
            await context.SaveChangesAsync(HttpContext.RequestAborted);

            return StatusCode((int) HttpStatusCode.PreconditionFailed, "Not Downloaded");
        }
    }
}

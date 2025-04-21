using MediaFeeder.Data;
using MediaFeeder.Data.db;
using MediaFeeder.Data.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaFeeder.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "API")]
public class FoldersController(IDbContextFactory<MediaFeederDataContext> contextFactory, UserManager userManager) : ControllerBase
{
    // GET: api/Folders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<int>>> GetFolders()
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        ArgumentNullException.ThrowIfNull(user);

        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        return await context.Folders
            .Where(folder => folder.UserId == user.Id && folder.ParentId == null)
            .Select(static folder => folder.Id)
            .ToListAsync(HttpContext.RequestAborted);
    }

    // GET: api/Folders/5
    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetFolder(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        ArgumentNullException.ThrowIfNull(user);

        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var folder = await context.Folders
            .Include(static folder => folder.Subfolders)
            .Include(static folder => folder.Subscriptions)
            .SingleOrDefaultAsync(f => f.Id == id && f.UserId == user.Id, HttpContext.RequestAborted);

        if (folder == null)
            return NotFound();

        return new
        {
            folder.Name,
            folder.Id,
            ChildFolders = folder.Subfolders.Select(static f => f.Id).ToList(),
            ChildSubscriptions = folder.Subscriptions.Select(static s => s.Id).ToList()
        };
    }

    // PUT: api/Folders/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{id}")]
    public async Task<IActionResult> PutFolder(int id,
        Folder folder)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        ArgumentNullException.ThrowIfNull(user);

        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var dbFolder =
            await context.Folders.SingleOrDefaultAsync(f => f.Id == id && f.UserId == user.Id,
                HttpContext.RequestAborted);

        if (dbFolder == null)
            return NotFound();

        if (id != folder.Id) return BadRequest();

        context.Entry(folder).State = EntityState.Modified;

        try
        {
            await context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await FolderExists(id)) return NotFound();

            throw;
        }

        return NoContent();
    }

    // POST: api/Folders
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPost]
    public async Task<ActionResult<Folder>> PostFolder(
        Folder folder)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Forbid();

        if (folder.UserId != user.Id)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        context.Folders.Add(folder);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return CreatedAtAction("GetFolder", new { id = folder.Id },
            folder);
    }

    // DELETE: api/Folders/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFolder(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        ArgumentNullException.ThrowIfNull(user);

        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var folder =
            await context.Folders.SingleOrDefaultAsync(f => f.Id == id && f.UserId == user.Id,
                HttpContext.RequestAborted);

        if (folder == null) return NotFound();

        context.Folders.Remove(folder);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    private async Task<bool> FolderExists(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        return context.Folders.Any(e => e.Id == id);
    }
}
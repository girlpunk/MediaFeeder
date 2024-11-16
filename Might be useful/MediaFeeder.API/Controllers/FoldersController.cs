using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediaFeeder.API.Models.db;
using MediaFeeder.API.Models.Identity;
using MediaFeeder.DTOs.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace MediaFeeder.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class FoldersController(MediaFeederDataContext context, UserManager userManager) : ControllerBase
{
    // GET: api/Folders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<int>>> GetYtManagerAppSubscriptionFolders()
    {
        var user = await userManager.GetUserAsync(HttpContext.User);

        return await context.YtManagerAppSubscriptionFolders
            .Where(folder => folder.User == user && folder.Parent == null)
            .Select(folder => folder.Id)
            .ToListAsync(HttpContext.RequestAborted);
    }

    // GET: api/Folders/5
    [HttpGet("{id}")]
    public async Task<ActionResult<FolderGet>> GetYtManagerAppSubscriptionFolder(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);

        var ytManagerAppSubscriptionFolder = await context.YtManagerAppSubscriptionFolders.SingleOrDefaultAsync(f => f.Id == id && f.User == user, HttpContext.RequestAborted);

        if (ytManagerAppSubscriptionFolder == null)
            return NotFound();

        return new FolderGet
        {
            Name = ytManagerAppSubscriptionFolder.Name,
            Id = ytManagerAppSubscriptionFolder.Id,
            ChildFolders = ytManagerAppSubscriptionFolder.InverseParent.Select(f => f.Id).ToList(),
            ChildSubscriptions = ytManagerAppSubscriptionFolder.YtManagerAppSubscriptions.Select(s => s.Id).ToList(),
        };
    }

    // PUT: api/Folders/5
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPut("{id}")]
    public async Task<IActionResult> PutYtManagerAppSubscriptionFolder(int id, YtManagerAppSubscriptionFolder ytManagerAppSubscriptionFolder)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        var folder = await context.YtManagerAppSubscriptionFolders.SingleOrDefaultAsync(f => f.Id == id && f.User == user, HttpContext.RequestAborted);

        if (folder == null)
            return NotFound();

        if (id != ytManagerAppSubscriptionFolder.Id)
        {
            return BadRequest();
        }

        context.Entry(ytManagerAppSubscriptionFolder).State = EntityState.Modified;

        try
        {
            await context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!YtManagerAppSubscriptionFolderExists(id))
            {
                return NotFound();
            }

            throw;
        }

        return NoContent();
    }

    // POST: api/Folders
    // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
    [HttpPost]
    public async Task<ActionResult<YtManagerAppSubscriptionFolder>> PostYtManagerAppSubscriptionFolder(YtManagerAppSubscriptionFolder ytManagerAppSubscriptionFolder)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Forbid();

        ytManagerAppSubscriptionFolder.User ??= user;

        if(ytManagerAppSubscriptionFolder.User != user)
            return Unauthorized();

        context.YtManagerAppSubscriptionFolders.Add(ytManagerAppSubscriptionFolder);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return CreatedAtAction("GetYtManagerAppSubscriptionFolder", new { id = ytManagerAppSubscriptionFolder.Id }, ytManagerAppSubscriptionFolder);
    }

    // DELETE: api/Folders/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteYtManagerAppSubscriptionFolder(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        var ytManagerAppSubscriptionFolder = await context.YtManagerAppSubscriptionFolders.SingleOrDefaultAsync(f => f.Id == id && f.User == user, HttpContext.RequestAborted);

        if (ytManagerAppSubscriptionFolder == null)
        {
            return NotFound();
        }

        context.YtManagerAppSubscriptionFolders.Remove(ytManagerAppSubscriptionFolder);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    private bool YtManagerAppSubscriptionFolderExists(int id)
    {
        return context.YtManagerAppSubscriptionFolders.Any(e => e.Id == id);
    }
}
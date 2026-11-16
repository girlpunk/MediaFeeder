namespace MediaFeeder.Controllers;

using Data;
using Data.db;
using Data.DTOs;
using Data.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "API")]
public class FoldersController(
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    UserManager userManager
) : ControllerBase
{
    /// <summary>
    /// GET: api/Folders
    /// </summary>
    /// <returns>List of folders</returns>
    [HttpGet]
    [ProducesResponseType<IEnumerable<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<int>>> GetFolders()
    {
        var user = await userManager.GetUserAsync(HttpContext.User);

        if (user == null)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        return await context.Folders.AsNoTracking()
            .Where(folder => folder.UserId == user.Id && folder.ParentId == null)
            .Select(static folder => folder.Id)
            .ToListAsync(HttpContext.RequestAborted);
    }

    /// <summary>
    /// GET: api/Folders/5
    /// </summary>
    /// <param name="id">ID of the folder to retrieve</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType<FolderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FolderResponse>> GetFolder(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        var folder = await context.Folders.AsNoTracking()
            .Include(static folder => folder.Subfolders)
            .Include(static folder => folder.Subscriptions)
            .SingleOrDefaultAsync(
                f => f.Id == id && f.UserId == user.Id,
                HttpContext.RequestAborted
            );

        if (folder == null)
            return NotFound();

        return new FolderResponse(
            folder.Name,
            folder.Id,
            folder.Subfolders.Select(static f => f.Id).ToList(),
            folder.Subscriptions.Select(static s => s.Id).ToList()
        );
    }

    /// <summary>
    /// PUT: api/Folders/5
    /// </summary>
    /// <param name="id">ID of the folder being edited</param>
    /// <param name="folder">Details of the folder being edited</param>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PutFolder(int id, Folder folder)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        var dbFolder = await context.Folders.SingleOrDefaultAsync(
            f => f.Id == id && f.UserId == user.Id,
            HttpContext.RequestAborted
        );

        if (dbFolder == null)
            return NotFound();

        ArgumentNullException.ThrowIfNull(folder);

        if (id != folder.Id)
            return BadRequest();

        context.Entry(folder).State = EntityState.Modified;

        try
        {
            await context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await FolderExists(id))
                return NotFound();

            throw;
        }

        return NoContent();
    }

    /// <summary>
    /// POST: api/Folders
    /// </summary>
    /// <param name="folder">Folder details</param>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<Folder>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Folder>> PostFolder(Folder folder)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        ArgumentNullException.ThrowIfNull(folder);

        if (folder.UserId != user.Id)
            return Forbid();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        context.Folders.Add(folder);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return CreatedAtAction("GetFolder", new { id = folder.Id }, folder);
    }

    /// <summary>
    /// DELETE: api/Folders/5
    /// </summary>
    /// <param name="id">ID of the folder to be deleted</param>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteFolder(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        var folder = await context.Folders.SingleOrDefaultAsync(
            f => f.Id == id && f.UserId == user.Id,
            HttpContext.RequestAborted
        );

        if (folder == null)
            return NotFound();

        context.Folders.Remove(folder);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    /// <summary>
    /// Check if a folder exists
    /// </summary>
    /// <param name="id">ID of the folder</param>
    /// <returns>If the folder exists</returns>
    private async Task<bool> FolderExists(int id)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );

        return context.Folders.AsNoTracking().Any(e => e.Id == id);
    }
}

namespace MediaFeeder.Controllers;

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Data;
using Data.Identity;
using Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders.Physical;

[ApiController]
[Route("api/[controller]/{id:int}")]
public partial class VideoController(
    IDbContextFactory<MediaFeederDataContext> contextFactory,
    UserManager userManager,
    IConfiguration configuration,
    ILogger<VideoController> logger
) : ControllerBase
{
    /// <summary>
    /// JSON serializer that ignores loops
    /// </summary>
    private static readonly JsonSerializerOptions IgnoreCycles = new() { ReferenceHandler = ReferenceHandler.IgnoreCycles };

    [HttpGet("play")]
    [Authorize(Roles = "Download", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<PhysicalFileResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PhysicalFileResult>> Play(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        var scopeClaim = HttpContext.User.Claims.SingleOrDefault(static c => c.Type == "Video");
        if (scopeClaim == null || scopeClaim.Value != id.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            var serialized = JsonSerializer.Serialize(
                HttpContext.User,
                IgnoreCycles
            );

            LogMissingClaim(logger, scopeClaim);

            return StatusCode(
                (int) HttpStatusCode.Forbidden,
                $"The video claim does not contain '{scopeClaim}' or was not found. Full User: {serialized}"
            );
        }

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );
        var video = await context.Videos.AsNoTracking()
            .SingleOrDefaultAsync(v => v.Id == id && v.Subscription!.UserId == user.Id, HttpContext.RequestAborted);

        if (video == null)
            return NotFound();

        if (video.DownloadedPath == null)
            return StatusCode((int) HttpStatusCode.PreconditionFailed, "Not Downloaded");

        if (video.DownloadedPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return Redirect(video.DownloadedPath);

        new FileExtensionContentTypeProvider().TryGetContentType(
            video.DownloadedPath,
            out var mimeType
        );
        return PhysicalFile(video.DownloadedPath, mimeType ?? "application/octet-stream", true);
    }

    /// <summary>
    /// Log that the required claim to play the video is missing
    /// </summary>
    /// <param name="logger">Destination</param>
    /// <param name="claim">Missing claim</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "The video claim does not contain '{claim}' or was not found")]
    private static partial void LogMissingClaim(ILogger logger, Claim? claim);

    [HttpGet("thumbnail")]
    [OutputCache(Duration = 60 * 60 * 24 * 7)]
    [ResponseCache(Duration = 60 * 60 * 24 * 7)]
    [Authorize(Policy = "Thumbnails")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Thumbnail(int id)
    {
        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        await using var context = await contextFactory.CreateDbContextAsync(
            HttpContext.RequestAborted
        );
        var video = await context.Videos.SingleOrDefaultAsync(
            v => v.Id == id && v.Subscription!.UserId == user.Id,
            HttpContext.RequestAborted
        );

        if (video == null)
            return NotFound();

        if (video.Thumb == null)
        {
            return StatusCode(
                (int) HttpStatusCode.PreconditionFailed,
                "Not Downloaded - no path saved"
            );
        }

        if (video.Thumb.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return Redirect(video.Thumb);

        try
        {
            var relativePath = video.Thumb;
            var absolutePath = Path.Join(configuration.GetValue<string>("MediaRoot"), relativePath);
            var stream = new PhysicalFileInfo(new FileInfo(absolutePath)).CreateReadStream();
            new FileExtensionContentTypeProvider().TryGetContentType(video.Thumb, out var mimeType);
            return File(stream, mimeType ?? "application/octet-stream");
        }
        catch (IOException e)
        {
            if (System.IO.File.Exists(video.Thumb))
                System.IO.File.Delete(video.Thumb);

            video.Thumb = null;
            await context.SaveChangesAsync(HttpContext.RequestAborted);

            return StatusCode(
                (int) HttpStatusCode.PreconditionFailed,
                $"Not Downloaded - {e.Message}"
            );
        }
    }

    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [HttpGet("play/token")]
    [Authorize(Policy = "API")]
    public async Task<IActionResult> PlayToken(int id, [FromServices] TokenHelper tokenHelper)
    {
        ArgumentNullException.ThrowIfNull(tokenHelper);
        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);

        var user = await userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Forbid();

        var video = await context.Videos
            .Include(static v => v.Subscription)
            .SingleOrDefaultAsync(
                v => v.Id == id && v.Subscription!.User == user,
                HttpContext.RequestAborted
            );

        if (video == null)
            return NotFound();

        return Ok(new { token = tokenHelper.GeneratePlaybackJwt(user, id) });
    }
}

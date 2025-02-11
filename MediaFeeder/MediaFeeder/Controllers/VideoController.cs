using System.Net;
using MediaFeeder.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders.Physical;

namespace MediaFeeder.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoController(IDbContextFactory<MediaFeederDataContext> contextFactory) : ControllerBase
{
    [HttpGet("{id:int} play")]
    [AllowAnonymous]
    public async Task<IActionResult> Play(int id)
    {
        //TODO: Authentication
        //TODO: Authorisation

        await using var context = await contextFactory.CreateDbContextAsync(HttpContext.RequestAborted);
        var video = await context.Videos.SingleAsync(v => v.Id == id);

        if (video.DownloadedPath == null)
            return StatusCode((int)HttpStatusCode.PreconditionFailed, "Not Downloaded");

        var stream = (new PhysicalFileInfo(new FileInfo(video.DownloadedPath)))
            .CreateReadStream();

        new FileExtensionContentTypeProvider().TryGetContentType(video.DownloadedPath, out var mimeType);

        return File(stream, mimeType ?? "application/octet-stream", true);
    }
}

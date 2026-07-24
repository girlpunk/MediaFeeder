namespace MediaFeeder.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
[Authorize(Policy = "API")]
public class UserController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<string> Get()
    {
        var b = new
        {
            User.Identity,
            OtherIdentities = User.Identities,
            User.Claims,
        };

        return Ok(b);
    }
}

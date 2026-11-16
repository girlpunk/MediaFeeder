namespace MediaFeeder.Controllers;

using Data.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
[Authorize(Policy = "API")]
public class UserController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public ActionResult<UserResponse> Get()
    {
        var b = new UserResponse(
            User.Identity,
            User.Identities,
            User.Claims
        );

        return Ok(b);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaFeeder.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> Get()
        {
            var b = new
            {
                Identity = User.Identity,
                OtherIdentities = User.Identities,
                Claims = User.Claims,
            };

            return Ok(b);
        }
    }
}

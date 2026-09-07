using Microsoft.AspNetCore.Mvc;

namespace StudioFlow.API.Controllers;

// Structure only. Actions, model binding, status codes and [Authorize] rules are
// added in a later step (StudioFlow spec §13, §21).
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioFlow.API.Authentication;
using StudioFlow.Core.DTOs.Registrations;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.API.Controllers;

/// <summary>
/// Member registration actions (spec §21): register / unregister for a class and
/// list the caller's own registrations. The member id always comes from the JWT,
/// never from the request.
/// </summary>
[ApiController]
[Route("api")]
[Authorize(Roles = "Member")]
public sealed class RegistrationsController : ControllerBase
{
    private readonly IRegistrationService _registrationService;

    public RegistrationsController(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    /// <summary>Registers the caller for a class (spec §16). 409 if full / cancelled / started / duplicate.</summary>
    [HttpPost("classes/{classId:int}/register")]
    [ProducesResponseType(typeof(RegistrationResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegistrationResponseDto>> Register(
        [FromRoute] int classId, CancellationToken cancellationToken)
    {
        var registration = await _registrationService.RegisterAsync(classId, User.GetUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, registration);
    }

    /// <summary>Cancels the caller's registration; may promote the first waiting member (spec §18).</summary>
    [HttpDelete("classes/{classId:int}/register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel([FromRoute] int classId, CancellationToken cancellationToken)
    {
        await _registrationService.CancelAsync(classId, User.GetUserId(), cancellationToken);
        return NoContent();
    }

    /// <summary>The caller's own registrations, newest first (spec §21).</summary>
    [HttpGet("me/registrations")]
    [ProducesResponseType(typeof(IReadOnlyList<RegistrationResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RegistrationResponseDto>>> GetMyRegistrations(
        CancellationToken cancellationToken)
    {
        var registrations = await _registrationService.GetMyRegistrationsAsync(User.GetUserId(), cancellationToken);
        return Ok(registrations);
    }
}

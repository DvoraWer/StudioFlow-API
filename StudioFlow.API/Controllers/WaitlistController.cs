using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioFlow.API.Authentication;
using StudioFlow.Core.DTOs.Waitlist;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.API.Controllers;

/// <summary>
/// Waiting-list actions for a full class (spec §18, §21). Separate from
/// registration — joining the waiting list is always an explicit member action.
/// </summary>
[ApiController]
[Route("api/classes/{classId:int}/waitlist")]
[Authorize(Roles = "Member")]
public sealed class WaitlistController : ControllerBase
{
    private readonly IWaitlistService _waitlistService;

    public WaitlistController(IWaitlistService waitlistService)
    {
        _waitlistService = waitlistService;
    }

    /// <summary>Adds the caller to the class's waiting list (spec §18). 409 if the class is not full.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(WaitlistResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WaitlistResponseDto>> Join(
        [FromRoute] int classId, CancellationToken cancellationToken)
    {
        var entry = await _waitlistService.JoinAsync(classId, User.GetUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, entry);
    }

    /// <summary>Removes the caller from the class's waiting list (spec §18).</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave([FromRoute] int classId, CancellationToken cancellationToken)
    {
        await _waitlistService.LeaveAsync(classId, User.GetUserId(), cancellationToken);
        return NoContent();
    }
}

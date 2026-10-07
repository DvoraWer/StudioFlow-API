using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioFlow.API.Authentication;
using StudioFlow.Core.DTOs.Instructors;
using StudioFlow.Core.DTOs.Users;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.API.Controllers;

/// <summary>
/// The caller's own account and instructor profile. Every action resolves the
/// subject from the JWT user id — no user or instructor id is accepted from the
/// route or body, so a caller can only ever read or change their own data.
/// Admin management of instructors stays in <see cref="InstructorsController"/>.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IInstructorService _instructorService;

    public MeController(IUserService userService, IInstructorService instructorService)
    {
        _userService = userService;
        _instructorService = instructorService;
    }

    /// <summary>The caller's account (name, email, role). Any authenticated role.</summary>
    [HttpGet("account")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponseDto>> GetAccount(CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetByIdAsync(User.GetUserId(), cancellationToken));
    }

    /// <summary>The caller's instructor profile. 404 if no profile is linked to the account.</summary>
    [HttpGet("instructor-profile")]
    [Authorize(Roles = "Instructor")]
    [ProducesResponseType(typeof(InstructorResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstructorResponseDto>> GetInstructorProfile(CancellationToken cancellationToken)
    {
        return Ok(await _instructorService.GetMyProfileAsync(User.GetUserId(), cancellationToken));
    }

    /// <summary>Updates Specialization/Bio of the caller's own instructor profile only.</summary>
    [HttpPut("instructor-profile")]
    [Authorize(Roles = "Instructor")]
    [ProducesResponseType(typeof(InstructorResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstructorResponseDto>> UpdateInstructorProfile(
        [FromBody] InstructorUpdateDto request, CancellationToken cancellationToken)
    {
        return Ok(await _instructorService.UpdateMyProfileAsync(User.GetUserId(), request, cancellationToken));
    }
}

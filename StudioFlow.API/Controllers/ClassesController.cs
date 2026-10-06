using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioFlow.API.Authentication;
using StudioFlow.Core.DTOs.Classes;
using StudioFlow.Core.DTOs.Common;
using StudioFlow.Core.DTOs.Participants;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.API.Controllers;

/// <summary>Class catalogue, scheduling and participants (spec §21, §22).</summary>
[ApiController]
[Route("api/classes")]
public sealed class ClassesController : ControllerBase
{
    private readonly IClassService _classService;

    public ClassesController(IClassService classService)
    {
        _classService = classService;
    }

    /// <summary>Server-side paged + filtered list (spec §22). Public.</summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ClassListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ClassListItemDto>>> GetPaged(
        [FromQuery] ClassQueryParameters query, CancellationToken cancellationToken)
    {
        return Ok(await _classService.GetPagedAsync(query, cancellationToken));
    }

    /// <summary>Full class detail, including tags (spec §21). Public.</summary>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClassResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassResponseDto>> GetById(
        [FromRoute] int id, CancellationToken cancellationToken)
    {
        return Ok(await _classService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>
    /// Creates a class after all §14/§15 rules pass (spec §21). Admin creates for any
    /// instructor; an Instructor creates only for themselves (ownership is resolved
    /// from the token in the service; 403 otherwise).
    /// </summary>
    [Authorize(Roles = "Admin,Instructor")]
    [HttpPost]
    [ProducesResponseType(typeof(ClassResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClassResponseDto>> Create(
        [FromBody] ClassCreateDto request, CancellationToken cancellationToken)
    {
        var created = await _classService.CreateAsync(
            request, User.GetUserId(), User.GetRole(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates a class and re-validates all affected rules (spec §21). Admin only.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ClassResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClassResponseDto>> Update(
        [FromRoute] int id, [FromBody] ClassUpdateDto request, CancellationToken cancellationToken)
    {
        return Ok(await _classService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Marks a class Cancelled (spec §21). Admin only.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel([FromRoute] int id, CancellationToken cancellationToken)
    {
        await _classService.CancelAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Active participants of a class (spec §21). Allowed for Admin, or the
    /// instructor who owns the class (enforced in the service; 403 otherwise).
    /// </summary>
    [Authorize(Roles = "Admin,Instructor")]
    [HttpGet("{id:int}/participants")]
    [ProducesResponseType(typeof(IReadOnlyList<ParticipantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ParticipantDto>>> GetParticipants(
        [FromRoute] int id, CancellationToken cancellationToken)
    {
        var participants = await _classService.GetParticipantsAsync(
            id, User.GetUserId(), User.GetRole(), cancellationToken);
        return Ok(participants);
    }
}

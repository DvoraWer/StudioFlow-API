using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioFlow.Core.DTOs.Instructors;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.API.Controllers;

/// <summary>Instructor administration — Admin CRUD (spec §6, §21).</summary>
[ApiController]
[Route("api/instructors")]
[Authorize(Roles = "Admin")]
public sealed class InstructorsController : ControllerBase
{
    private readonly IInstructorService _instructorService;

    public InstructorsController(IInstructorService instructorService)
    {
        _instructorService = instructorService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InstructorResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InstructorResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _instructorService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InstructorResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstructorResponseDto>> GetById([FromRoute] int id, CancellationToken cancellationToken)
    {
        return Ok(await _instructorService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Provisions a new User (Role = Instructor) + linked Instructor in one operation (spec §6).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(InstructorResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InstructorResponseDto>> Create(
        [FromBody] InstructorCreateDto request, CancellationToken cancellationToken)
    {
        var created = await _instructorService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates Specialization/Bio only (spec §6).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(InstructorResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstructorResponseDto>> Update(
        [FromRoute] int id, [FromBody] InstructorUpdateDto request, CancellationToken cancellationToken)
    {
        return Ok(await _instructorService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Deletes the Instructor (not the linked User). 409 if classes reference it (spec §25).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        await _instructorService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

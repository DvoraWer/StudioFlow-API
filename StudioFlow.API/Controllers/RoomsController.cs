using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioFlow.Core.DTOs.Rooms;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.API.Controllers;

/// <summary>
/// Room administration — Admin CRUD (spec §7, §21). Authorization is per action:
/// the room list is also readable by Instructors so they can pick a room when
/// creating their own class; every other action stays Admin-only.
/// </summary>
[ApiController]
[Route("api/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    [Authorize(Roles = "Admin,Instructor")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoomResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoomResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _roomService.GetAllAsync(cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomResponseDto>> GetById([FromRoute] int id, CancellationToken cancellationToken)
    {
        return Ok(await _roomService.GetByIdAsync(id, cancellationToken));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoomResponseDto>> Create(
        [FromBody] RoomCreateDto request, CancellationToken cancellationToken)
    {
        var created = await _roomService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(RoomResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomResponseDto>> Update(
        [FromRoute] int id, [FromBody] RoomUpdateDto request, CancellationToken cancellationToken)
    {
        return Ok(await _roomService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Deletes a room. 409 if any class references it (spec §7, §25).</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        await _roomService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

using AutoMapper;
using StudioFlow.Core.DTOs.Rooms;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.Service.Services;

/// <inheritdoc cref="IRoomService" />
public sealed class RoomService : IRoomService
{
    private readonly IRoomRepository _rooms;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public RoomService(IRoomRepository rooms, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _rooms = rooms;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<RoomResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rooms = await _rooms.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<RoomResponseDto>>(rooms);
    }

    public async Task<RoomResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var room = await _rooms.GetByIdAsync(id, cancellationToken)
            ?? throw NotFoundException.For("Room", id);

        return _mapper.Map<RoomResponseDto>(room);
    }

    public async Task<RoomResponseDto> CreateAsync(RoomCreateDto request, CancellationToken cancellationToken = default)
    {
        var room = _mapper.Map<Room>(request);

        _rooms.Add(room);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RoomResponseDto>(room);
    }

    public async Task<RoomResponseDto> UpdateAsync(int id, RoomUpdateDto request, CancellationToken cancellationToken = default)
    {
        var room = await _rooms.GetForUpdateAsync(id, cancellationToken)
            ?? throw NotFoundException.For("Room", id);

        _mapper.Map(request, room);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RoomResponseDto>(room);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var room = await _rooms.GetForUpdateAsync(id, cancellationToken)
            ?? throw NotFoundException.For("Room", id);

        if (await _rooms.HasClassesAsync(id, cancellationToken))
        {
            throw new ConflictException(
                "This room is referenced by one or more classes and cannot be deleted. Deactivate it instead.");
        }

        _rooms.Remove(room);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

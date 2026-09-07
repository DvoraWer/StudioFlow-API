using AutoMapper;
using StudioFlow.Core.DTOs.Classes;
using StudioFlow.Core.DTOs.Common;
using StudioFlow.Core.DTOs.Participants;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.Service.Services;

/// <inheritdoc cref="IClassService" />
public sealed class ClassService : IClassService
{
    private readonly IClassRepository _classes;
    private readonly IInstructorRepository _instructors;
    private readonly IRoomRepository _rooms;
    private readonly IRegistrationRepository _registrations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ClassService(
        IClassRepository classes,
        IInstructorRepository instructors,
        IRoomRepository rooms,
        IRegistrationRepository registrations,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _classes = classes;
        _instructors = instructors;
        _rooms = rooms;
        _registrations = registrations;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<PagedResult<ClassListItemDto>> GetPagedAsync(
        ClassQueryParameters query, CancellationToken cancellationToken = default)
    {
        var page = await _classes.GetPagedAsync(query, cancellationToken);

        return new PagedResult<ClassListItemDto>
        {
            Items = _mapper.Map<IReadOnlyList<ClassListItemDto>>(page.Items),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }

    public async Task<ClassResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var @class = await _classes.GetByIdAsync(id, cancellationToken)
            ?? throw NotFoundException.For("Class", id);

        return _mapper.Map<ClassResponseDto>(@class);
    }

    public async Task<ClassResponseDto> CreateAsync(ClassCreateDto request, CancellationToken cancellationToken = default)
    {
        await ValidateScheduleAndCapacityAsync(
            request.InstructorId, request.RoomId, request.StartTime, request.EndTime,
            request.Capacity, registeredCount: 0, excludeClassId: null, cancellationToken);

        var @class = _mapper.Map<Class>(request);
        @class.Status = ClassStatus.Active;
        @class.RegisteredCount = 0;

        _classes.Add(@class);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with Instructor(+User), Room and Tags for the response contract.
        var created = await _classes.GetByIdAsync(@class.Id, cancellationToken);
        return _mapper.Map<ClassResponseDto>(created!);
    }

    public async Task<ClassResponseDto> UpdateAsync(int id, ClassUpdateDto request, CancellationToken cancellationToken = default)
    {
        var @class = await _classes.GetForUpdateAsync(id, cancellationToken)
            ?? throw NotFoundException.For("Class", id);

        await ValidateScheduleAndCapacityAsync(
            request.InstructorId, request.RoomId, request.StartTime, request.EndTime,
            request.Capacity, @class.RegisteredCount, excludeClassId: id, cancellationToken);

        _mapper.Map(request, @class); // Name/Description/InstructorId/RoomId/StartTime/EndTime/Capacity only
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _classes.GetByIdAsync(id, cancellationToken);
        return _mapper.Map<ClassResponseDto>(updated!);
    }

    public async Task CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var @class = await _classes.GetForUpdateAsync(id, cancellationToken)
            ?? throw NotFoundException.For("Class", id);

        if (@class.Status == ClassStatus.Cancelled)
        {
            return; // already cancelled — nothing to do
        }

        @class.Status = ClassStatus.Cancelled;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(
        int classId, int callerUserId, UserRole callerRole, CancellationToken cancellationToken = default)
    {
        var @class = await _classes.GetByIdAsync(classId, cancellationToken)
            ?? throw NotFoundException.For("Class", classId);

        if (callerRole != UserRole.Admin)
        {
            var instructor = await _instructors.GetByUserIdAsync(callerUserId, cancellationToken);
            if (instructor is null || instructor.Id != @class.InstructorId)
            {
                throw new ForbiddenActionException("You can only view the participants of your own classes.");
            }
        }

        var registrations = await _registrations.GetActiveByClassAsync(classId, cancellationToken);
        return _mapper.Map<IReadOnlyList<ParticipantDto>>(registrations);
    }

    /// <summary>
    /// Enforces the §14/§15 rules shared by create and update. <paramref name="registeredCount"/>
    /// is 0 on create and the class's current count on update (so capacity can't drop
    /// below people already registered). <paramref name="excludeClassId"/> keeps a class
    /// from clashing with itself during an update.
    /// </summary>
    private async Task ValidateScheduleAndCapacityAsync(
        int instructorId, int roomId, DateTime startTime, DateTime endTime,
        int capacity, int registeredCount, int? excludeClassId, CancellationToken cancellationToken)
    {
        if (endTime <= startTime)
        {
            throw new ValidationException("EndTime must be later than StartTime.");
        }

        if (capacity <= 0)
        {
            throw new ValidationException("Capacity must be greater than zero.");
        }

        _ = await _instructors.GetByIdAsync(instructorId, cancellationToken)
            ?? throw new ValidationException($"Instructor '{instructorId}' does not exist.");

        var room = await _rooms.GetByIdAsync(roomId, cancellationToken)
            ?? throw new ValidationException($"Room '{roomId}' does not exist.");

        if (!room.IsActive)
        {
            throw new ValidationException($"Room '{room.Name}' is not active.");
        }

        if (capacity > room.MaximumCapacity)
        {
            throw new ValidationException(
                $"Capacity {capacity} exceeds room '{room.Name}' maximum capacity of {room.MaximumCapacity}.");
        }

        if (capacity < registeredCount)
        {
            throw new ConflictException(
                $"Capacity {capacity} is below the current {registeredCount} active registrations.");
        }

        if (await _classes.InstructorHasOverlapAsync(instructorId, startTime, endTime, excludeClassId, cancellationToken))
        {
            throw new ConflictException("The instructor already has an active class during this time window.");
        }

        if (await _classes.RoomHasOverlapAsync(roomId, startTime, endTime, excludeClassId, cancellationToken))
        {
            throw new ConflictException("The room is already booked by an active class during this time window.");
        }
    }
}

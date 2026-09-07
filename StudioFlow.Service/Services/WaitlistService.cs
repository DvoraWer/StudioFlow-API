using AutoMapper;
using StudioFlow.Core.DTOs.Waitlist;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.Service.Services;

/// <inheritdoc cref="IWaitlistService" />
public sealed class WaitlistService : IWaitlistService
{
    private readonly IClassRepository _classes;
    private readonly IWaitlistRepository _waitlist;
    private readonly IRegistrationRepository _registrations;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public WaitlistService(
        IClassRepository classes,
        IWaitlistRepository waitlist,
        IRegistrationRepository registrations,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _classes = classes;
        _waitlist = waitlist;
        _registrations = registrations;
        _users = users;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<WaitlistResponseDto> JoinAsync(int classId, int memberId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(memberId, cancellationToken)
            ?? throw NotFoundException.For("User", memberId);

        if (user.Role != UserRole.Member || !user.IsActive)
        {
            throw new ForbiddenActionException("Only active members can join a waiting list.");
        }

        var @class = await _classes.GetByIdAsync(classId, cancellationToken)
            ?? throw NotFoundException.For("Class", classId);

        if (@class.Status == ClassStatus.Cancelled)
        {
            throw new ConflictException("This class has been cancelled.");
        }

        if (@class.StartTime <= DateTime.UtcNow)
        {
            throw new ConflictException("This class has already started.");
        }

        if (@class.RegisteredCount < @class.Capacity)
        {
            throw new ConflictException(
                "This class still has available seats — register instead of joining the waiting list.");
        }

        var registration = await _registrations.GetForUpdateAsync(memberId, classId, cancellationToken);
        if (registration is { Status: RegistrationStatus.Active })
        {
            throw new ConflictException("You are already registered for this class.");
        }

        var existing = await _waitlist.GetForUpdateAsync(memberId, classId, cancellationToken);
        if (existing is { Status: WaitlistStatus.Waiting })
        {
            throw new ConflictException("You are already on the waiting list for this class.");
        }

        var position = await _waitlist.CountWaitingByClassAsync(classId, cancellationToken) + 1;

        WaitlistEntry entry;
        if (existing is null)
        {
            entry = new WaitlistEntry
            {
                MemberId = memberId,
                ClassId = classId,
                JoinedAt = DateTime.UtcNow,
                Position = position,
                Status = WaitlistStatus.Waiting
            };
            _waitlist.Add(entry);
        }
        else
        {
            // Re-activate a previously cancelled entry rather than inserting a duplicate.
            existing.Status = WaitlistStatus.Waiting;
            existing.JoinedAt = DateTime.UtcNow;
            existing.Position = position;
            entry = existing;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = _mapper.Map<WaitlistResponseDto>(entry);
        response.ClassName = @class.Name; // Class was loaded read-only; fill the flattened name here
        return response;
    }

    public async Task LeaveAsync(int classId, int memberId, CancellationToken cancellationToken = default)
    {
        var entry = await _waitlist.GetForUpdateAsync(memberId, classId, cancellationToken);
        if (entry is null || entry.Status != WaitlistStatus.Waiting)
        {
            throw new NotFoundException($"No active waiting-list entry found for class {classId}.");
        }

        entry.Status = WaitlistStatus.Cancelled;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

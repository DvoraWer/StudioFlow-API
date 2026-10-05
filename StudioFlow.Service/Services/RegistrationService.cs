using AutoMapper;
using Microsoft.Extensions.Logging;
using StudioFlow.Core.DTOs.Registrations;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.Service.Services;

/// <inheritdoc cref="IRegistrationService" />
public sealed class RegistrationService : IRegistrationService
{
    private readonly IClassRepository _classes;
    private readonly IRegistrationRepository _registrations;
    private readonly IWaitlistRepository _waitlist;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(
        IClassRepository classes,
        IRegistrationRepository registrations,
        IWaitlistRepository waitlist,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<RegistrationService> logger)
    {
        _classes = classes;
        _registrations = registrations;
        _waitlist = waitlist;
        _users = users;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<RegistrationResponseDto> RegisterAsync(int classId, int memberId, CancellationToken cancellationToken = default)
    {
        await EnsureActiveMemberAsync(memberId, cancellationToken);

        // Tracked load so the xmin concurrency token travels with the entity (spec §16, §17).
        var @class = await _classes.GetForUpdateAsync(classId, cancellationToken)
            ?? throw NotFoundException.For("Class", classId);

        if (@class.Status == ClassStatus.Cancelled)
        {
            throw new ConflictException("This class has been cancelled.");
        }

        if (@class.StartTime <= DateTime.UtcNow)
        {
            throw new ConflictException("This class has already started.");
        }

        var existing = await _registrations.GetForUpdateAsync(memberId, classId, cancellationToken);
        if (existing is { Status: RegistrationStatus.Active })
        {
            throw new ConflictException("You are already registered for this class.");
        }

        if (@class.RegisteredCount >= @class.Capacity)
        {
            throw new ConflictException("This class is full.");
        }

        if (existing is null)
        {
            _registrations.Add(new Registration
            {
                MemberId = memberId,
                ClassId = classId,
                RegisteredAt = DateTime.UtcNow,
                Status = RegistrationStatus.Active
            });
        }
        else
        {
            // Re-activate a previously cancelled registration — the unique index
            // Registration(MemberId, ClassId) is status-agnostic, so a second row
            // would violate it.
            existing.Status = RegistrationStatus.Active;
            existing.RegisteredAt = DateTime.UtcNow;
        }

        @class.RegisteredCount++;

        // One commit: the Class.RegisteredCount bump and the Registration insert/update
        // are written in a single transaction; a stale xmin surfaces as
        // ConcurrencyConflictException (translated in the Data layer's UnitOfWork).
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Registration confirmed. MemberId={MemberId} ClassId={ClassId} RegisteredCount={RegisteredCount}/{Capacity}",
            memberId, classId, @class.RegisteredCount, @class.Capacity);

        return await BuildResponseAsync(classId, memberId, cancellationToken);
    }

    public async Task CancelAsync(int classId, int memberId, CancellationToken cancellationToken = default)
    {
        var registration = await _registrations.GetForUpdateAsync(memberId, classId, cancellationToken);
        if (registration is null || registration.Status != RegistrationStatus.Active)
        {
            throw new NotFoundException($"No active registration found for class {classId}.");
        }

        var @class = await _classes.GetForUpdateAsync(classId, cancellationToken)
            ?? throw NotFoundException.For("Class", classId);

        registration.Status = RegistrationStatus.Cancelled;
        if (@class.RegisteredCount > 0)
        {
            @class.RegisteredCount--;
        }

        // Promote the first waiting member if a seat is now free on an active class (spec §18).
        if (@class.Status == ClassStatus.Active && @class.RegisteredCount < @class.Capacity)
        {
            var next = await _waitlist.GetNextWaitingAsync(classId, cancellationToken);
            if (next is not null)
            {
                next.Status = WaitlistStatus.Promoted;

                var promotedRegistration = await _registrations.GetForUpdateAsync(next.MemberId, classId, cancellationToken);
                if (promotedRegistration is null)
                {
                    _registrations.Add(new Registration
                    {
                        MemberId = next.MemberId,
                        ClassId = classId,
                        RegisteredAt = DateTime.UtcNow,
                        Status = RegistrationStatus.Active
                    });
                }
                else
                {
                    promotedRegistration.Status = RegistrationStatus.Active;
                    promotedRegistration.RegisteredAt = DateTime.UtcNow;
                }

                @class.RegisteredCount++;

                await WaitlistPositions.ReindexAsync(_waitlist, classId, cancellationToken);

                _logger.LogInformation(
                    "Waitlist promotion. Promoted MemberId={MemberId} into ClassId={ClassId} after a cancellation.",
                    next.MemberId, classId);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registration cancelled. MemberId={MemberId} ClassId={ClassId}", memberId, classId);
    }

    public async Task<IReadOnlyList<RegistrationResponseDto>> GetMyRegistrationsAsync(int memberId, CancellationToken cancellationToken = default)
    {
        var registrations = await _registrations.GetByMemberAsync(memberId, cancellationToken);
        return _mapper.Map<IReadOnlyList<RegistrationResponseDto>>(registrations);
    }

    private async Task EnsureActiveMemberAsync(int memberId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(memberId, cancellationToken)
            ?? throw NotFoundException.For("User", memberId);

        if (user.Role != UserRole.Member)
        {
            throw new ForbiddenActionException("Only members can register for classes.");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenActionException("This account is not active.");
        }
    }

    private async Task<RegistrationResponseDto> BuildResponseAsync(int classId, int memberId, CancellationToken cancellationToken)
    {
        // GetByMemberAsync eager-loads Class (+ Room, Instructor -> User) — the graph
        // RegistrationResponseDto needs. Exactly one row exists per (member, class).
        var registrations = await _registrations.GetByMemberAsync(memberId, cancellationToken);
        var registration = registrations.FirstOrDefault(r => r.ClassId == classId)
            ?? throw new NotFoundException($"No registration found for class {classId}.");

        return _mapper.Map<RegistrationResponseDto>(registration);
    }
}

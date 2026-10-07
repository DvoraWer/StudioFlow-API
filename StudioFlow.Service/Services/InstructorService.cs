using AutoMapper;
using StudioFlow.Core.DTOs.Instructors;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Security;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.Service.Services;

/// <inheritdoc cref="IInstructorService" />
public sealed class InstructorService : IInstructorService
{
    private readonly IInstructorRepository _instructors;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public InstructorService(
        IInstructorRepository instructors,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _instructors = instructors;
        _users = users;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<InstructorResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var instructors = await _instructors.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<InstructorResponseDto>>(instructors);
    }

    public async Task<InstructorResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var instructor = await _instructors.GetByIdAsync(id, cancellationToken)
            ?? throw NotFoundException.For("Instructor", id);

        return _mapper.Map<InstructorResponseDto>(instructor);
    }

    public async Task<InstructorResponseDto> CreateAsync(InstructorCreateDto request, CancellationToken cancellationToken = default)
    {
        if (await _users.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new ConflictException($"Email '{request.Email}' is already in use.");
        }

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Instructor,
            IsActive = true
        };

        // Map carries Specialization/Bio only; the linked user is set here.
        var instructor = _mapper.Map<Instructor>(request);
        instructor.User = user;

        _users.Add(user);
        _instructors.Add(instructor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // instructor.User is populated in memory; Id / UserId are now assigned.
        return _mapper.Map<InstructorResponseDto>(instructor);
    }

    public async Task<InstructorResponseDto> UpdateAsync(int id, InstructorUpdateDto request, CancellationToken cancellationToken = default)
    {
        var instructor = await _instructors.GetForUpdateAsync(id, cancellationToken)
            ?? throw NotFoundException.For("Instructor", id);

        _mapper.Map(request, instructor); // Specialization + Bio
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read with the linked User so Name/Email are present in the response.
        var refreshed = await _instructors.GetByIdAsync(id, cancellationToken)
            ?? throw NotFoundException.For("Instructor", id);
        return _mapper.Map<InstructorResponseDto>(refreshed);
    }

    public async Task<InstructorResponseDto> GetMyProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var instructor = await _instructors.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("No instructor profile is linked to your account.");

        return _mapper.Map<InstructorResponseDto>(instructor);
    }

    public async Task<InstructorResponseDto> UpdateMyProfileAsync(int userId, InstructorUpdateDto request, CancellationToken cancellationToken = default)
    {
        // Ownership comes from the caller's user id only; the instructor id is never client-supplied.
        var own = await _instructors.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("No instructor profile is linked to your account.");

        return await UpdateAsync(own.Id, request, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var instructor = await _instructors.GetForUpdateAsync(id, cancellationToken)
            ?? throw NotFoundException.For("Instructor", id);

        if (await _instructors.HasClassesAsync(id, cancellationToken))
        {
            throw new ConflictException(
                "This instructor is referenced by one or more classes and cannot be deleted.");
        }

        _instructors.Remove(instructor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

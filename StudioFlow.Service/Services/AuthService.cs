using AutoMapper;
using Microsoft.Extensions.Logging;
using StudioFlow.Core.DTOs.Auth;
using StudioFlow.Core.DTOs.Users;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Security;
using StudioFlow.Core.Interfaces.Services;

namespace StudioFlow.Service.Services;

/// <inheritdoc cref="IAuthService" />
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<AuthService> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<UserResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        if (await _users.EmailExistsAsync(request.Email, cancellationToken))
        {
            throw new ConflictException($"Email '{request.Email}' is already in use.");
        }

        var user = _mapper.Map<User>(request); // Name + Email only
        user.PasswordHash = _passwordHasher.Hash(request.Password);
        user.Role = UserRole.Member; // spec §5, §19 — public registration is always a Member
        user.IsActive = true;

        _users.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("New member account created. UserId={UserId}", user.Id);
        return _mapper.Map<UserResponseDto>(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(request.Email, cancellationToken);

        if (user is null
            || !_passwordHasher.Verify(request.Password, user.PasswordHash)
            || !user.IsActive)
        {
            // No email, password or hash in the log — just that a login was rejected.
            _logger.LogWarning("Login rejected.");
            throw new AuthenticationException();
        }

        var response = _mapper.Map<AuthResponseDto>(user);
        response.Token = _jwtTokenGenerator.GenerateToken(user);

        _logger.LogInformation("Login succeeded. UserId={UserId} Role={Role}", user.Id, user.Role);
        return response;
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetForUpdateAsync(userId, cancellationToken);

        // The token outlived its account (deleted or deactivated) — the session is no longer valid.
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Password change rejected: account missing or inactive. UserId={UserId}", userId);
            throw new AuthenticationException("Your session is no longer valid. Please sign in again.");
        }

        // 400, not 401: the session is fine, the input is wrong.
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Password change rejected: current password did not match. UserId={UserId}", userId);
            throw new ValidationException("The current password is incorrect.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed. UserId={UserId}", userId);
    }
}

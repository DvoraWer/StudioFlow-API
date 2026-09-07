using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StudioFlow.Core.DTOs.Auth;
using StudioFlow.Core.DTOs.Users;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Security;
using StudioFlow.Service.Services;

namespace StudioFlow.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwt = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private AuthService Sut() => new(
        _users.Object, _hasher.Object, _jwt.Object, _uow.Object, TestKit.Mapper,
        NullLogger<AuthService>.Instance);

    public AuthServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("HASHED");
    }

    [Fact]
    public async Task RegisterAsync_when_the_email_is_taken_throws_ConflictException()
    {
        _users.Setup(u => u.EmailExistsAsync("x@studioflow.local", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await Assert.ThrowsAsync<ConflictException>(
            () => Sut().RegisterAsync(new RegisterRequestDto { Name = "X", Email = "x@studioflow.local", Password = "Password123!" }));
    }

    [Fact]
    public async Task RegisterAsync_creates_an_active_Member_with_a_hashed_password_and_returns_no_token()
    {
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var dto = await Sut().RegisterAsync(new RegisterRequestDto
        {
            Name = "Dana", Email = "dana@studioflow.local", Password = "Password123!"
        });

        Assert.IsType<UserResponseDto>(dto);
        Assert.Equal("Member", dto.Role);
        Assert.Equal("dana@studioflow.local", dto.Email);
        Assert.True(dto.IsActive);
        _hasher.Verify(h => h.Hash("Password123!"), Times.Once);
        _users.Verify(u => u.Add(It.Is<User>(x =>
            x.Role == UserRole.Member && x.IsActive && x.PasswordHash == "HASHED")), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_when_the_user_is_unknown_throws_AuthenticationException()
    {
        _users.Setup(u => u.GetByEmailAsync("none@studioflow.local", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<AuthenticationException>(
            () => Sut().LoginAsync(new LoginRequestDto { Email = "none@studioflow.local", Password = "Password123!" }));
    }

    [Fact]
    public async Task LoginAsync_when_the_password_is_wrong_throws_AuthenticationException()
    {
        _users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Member(1));
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        await Assert.ThrowsAsync<AuthenticationException>(
            () => Sut().LoginAsync(new LoginRequestDto { Email = "member1@studioflow.local", Password = "wrong" }));
    }

    [Fact]
    public async Task LoginAsync_when_the_account_is_inactive_throws_AuthenticationException()
    {
        _users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(TestKit.Member(1, active: false));
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        await Assert.ThrowsAsync<AuthenticationException>(
            () => Sut().LoginAsync(new LoginRequestDto { Email = "member1@studioflow.local", Password = "Password123!" }));
    }

    [Fact]
    public async Task LoginAsync_valid_credentials_return_the_signed_token_and_identity()
    {
        var user = TestKit.Member(7);
        _users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _jwt.Setup(j => j.GenerateToken(user)).Returns("SIGNED.JWT.TOKEN");

        var dto = await Sut().LoginAsync(new LoginRequestDto { Email = user.Email, Password = "Password123!" });

        Assert.Equal("SIGNED.JWT.TOKEN", dto.Token);
        Assert.Equal(7, dto.UserId);
        Assert.Equal("Member", dto.Role);
        _jwt.Verify(j => j.GenerateToken(user), Times.Once);
    }
}

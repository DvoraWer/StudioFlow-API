using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StudioFlow.API.Controllers;
using StudioFlow.Core.DTOs.Auth;
using StudioFlow.Core.DTOs.Instructors;
using StudioFlow.Core.DTOs.Users;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Security;
using StudioFlow.Core.Interfaces.Services;
using StudioFlow.Service.Security;
using StudioFlow.Service.Services;
using ValidationException = StudioFlow.Core.Exceptions.ValidationException;

namespace StudioFlow.Tests;

public class ChangePasswordTests
{
    private const string Current = "Current-Pass-1";
    private const string Next = "Brand-New-Pass-2";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IJwtTokenGenerator> _jwt = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly CapturingLogger<AuthService> _log = new();

    // The real PBKDF2 hasher — proves the stored hash verifies with the production algorithm.
    private readonly Pbkdf2PasswordHasher _hasher = new();

    private AuthService Sut() => new(_users.Object, _hasher, _jwt.Object, _uow.Object, TestKit.Mapper, _log);

    public ChangePasswordTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private User Existing(int id = 7, bool active = true)
    {
        var user = TestKit.Member(id, active);
        user.PasswordHash = _hasher.Hash(Current);
        _users.Setup(u => u.GetForUpdateAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task Wrong_current_password_throws_ValidationException_not_AuthenticationException_and_does_not_save()
    {
        var user = Existing();
        var before = user.PasswordHash;

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Sut().ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = "wrong-password", NewPassword = Next }));

        Assert.IsNotType<AuthenticationException>(ex); // 400, so the client keeps the session
        Assert.Equal(before, user.PasswordHash);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Correct_current_password_stores_a_new_hash_that_verifies_and_saves_once()
    {
        var user = Existing();

        await Sut().ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = Current, NewPassword = Next });

        Assert.True(_hasher.Verify(Next, user.PasswordHash));
        Assert.False(_hasher.Verify(Current, user.PasswordHash));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Only_the_given_user_is_loaded()
    {
        Existing(7);

        await Sut().ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = Current, NewPassword = Next });

        _users.Verify(u => u.GetForUpdateAsync(7, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.GetForUpdateAsync(It.Is<int>(id => id != 7), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_user_is_rejected_without_saving()
    {
        _users.Setup(u => u.GetForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            Sut().ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = Current, NewPassword = Next }));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Inactive_user_is_rejected_without_saving()
    {
        var user = Existing(active: false);
        var before = user.PasswordHash;

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            Sut().ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = Current, NewPassword = Next }));
        Assert.Equal(before, user.PasswordHash);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Passwords_and_hashes_never_reach_the_log()
    {
        var user = Existing();
        var oldHash = user.PasswordHash;

        await Assert.ThrowsAsync<ValidationException>(() =>
            Sut().ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = "wrong-password", NewPassword = Next }));
        await Sut().ChangePasswordAsync(7, new ChangePasswordRequestDto { CurrentPassword = Current, NewPassword = Next });

        Assert.NotEmpty(_log.Entries);
        foreach (var entry in _log.Entries)
        {
            Assert.DoesNotContain(Current, entry);
            Assert.DoesNotContain(Next, entry);
            Assert.DoesNotContain("wrong-password", entry);
            Assert.DoesNotContain(oldHash, entry);
            Assert.DoesNotContain(user.PasswordHash, entry);
            Assert.DoesNotContain("pbkdf2", entry);
        }
    }

    [Theory]
    [InlineData("short", false)]                         // < 8
    [InlineData("exactly8", true)]
    public void NewPassword_length_is_validated(string newPassword, bool valid)
    {
        var dto = new ChangePasswordRequestDto { CurrentPassword = Current, NewPassword = newPassword };
        Assert.Equal(valid, Validator.TryValidateObject(dto, new ValidationContext(dto), null, validateAllProperties: true));
    }

    [Fact]
    public void NewPassword_over_100_and_CurrentPassword_over_100_are_rejected()
    {
        var tooLong = new string('x', 101);
        var a = new ChangePasswordRequestDto { CurrentPassword = Current, NewPassword = tooLong };
        var b = new ChangePasswordRequestDto { CurrentPassword = tooLong, NewPassword = Next };
        Assert.False(Validator.TryValidateObject(a, new ValidationContext(a), null, true));
        Assert.False(Validator.TryValidateObject(b, new ValidationContext(b), null, true));
    }

    [Fact]
    public void Dto_carries_only_current_and_new_password()
    {
        var names = typeof(ChangePasswordRequestDto).GetProperties().Select(p => p.Name).OrderBy(n => n);
        Assert.Equal(new[] { "CurrentPassword", "NewPassword" }, names);
    }
}

public class MyAccountTests
{
    private readonly Mock<IUserRepository> _users = new();

    private UserService Sut() => new(_users.Object, TestKit.Mapper);

    public static TheoryData<UserRole> Roles => new() { UserRole.Admin, UserRole.Instructor, UserRole.Member };

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task Every_role_gets_its_own_account(UserRole role)
    {
        var user = TestKit.Member(12);
        user.Role = role;
        _users.Setup(u => u.GetByIdAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var dto = await Sut().GetByIdAsync(12);

        Assert.Equal(12, dto.Id);
        Assert.Equal(user.Name, dto.Name);
        Assert.Equal(user.Email, dto.Email);
        Assert.Equal(role.ToString(), dto.Role);
    }

    [Fact]
    public async Task Missing_user_throws_NotFoundException()
    {
        _users.Setup(u => u.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().GetByIdAsync(12));
    }

    [Fact]
    public void Account_response_has_no_credential_members()
    {
        var names = typeof(UserResponseDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(names, n => n.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }
}

public class MyInstructorProfileTests
{
    private readonly Mock<IInstructorRepository> _instructors = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private InstructorService Sut() => new(
        _instructors.Object, _users.Object, _hasher.Object, _uow.Object, TestKit.Mapper);

    public MyInstructorProfileTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task GetMyProfile_returns_the_profile_linked_to_the_caller()
    {
        _instructors.Setup(i => i.GetByUserIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Instructor(3, userId: 5));

        var dto = await Sut().GetMyProfileAsync(5);

        Assert.Equal(3, dto.Id);
        Assert.Equal(5, dto.UserId);
        Assert.Equal("Ilana Cohen", dto.Name);
        Assert.Equal("Yoga", dto.Specialization);
    }

    [Fact]
    public async Task GetMyProfile_without_a_linked_profile_throws_NotFoundException()
    {
        _instructors.Setup(i => i.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((Instructor?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().GetMyProfileAsync(5));
    }

    [Fact]
    public async Task UpdateMyProfile_changes_only_Specialization_and_Bio_of_the_callers_own_instructor()
    {
        _instructors.Setup(i => i.GetByUserIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Instructor(3, userId: 5));
        var tracked = TestKit.Instructor(3, userId: 5);
        var linkedUser = tracked.User;
        _instructors.Setup(i => i.GetForUpdateAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(tracked);
        _instructors.Setup(i => i.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(() => tracked);

        var dto = await Sut().UpdateMyProfileAsync(5, new InstructorUpdateDto { Specialization = "Pilates", Bio = "New bio" });

        Assert.Equal("Pilates", tracked.Specialization);
        Assert.Equal("New bio", tracked.Bio);
        Assert.Equal(3, tracked.Id);
        Assert.Equal(5, tracked.UserId);
        Assert.Same(linkedUser, tracked.User);
        Assert.Equal("Ilana Cohen", tracked.User.Name);
        Assert.Equal("ilana@studioflow.local", tracked.User.Email);
        Assert.Equal(UserRole.Instructor, tracked.User.Role);
        Assert.Equal("Pilates", dto.Specialization);
        _instructors.Verify(i => i.GetForUpdateAsync(It.Is<int>(id => id != 3), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMyProfile_without_a_linked_profile_throws_NotFoundException_and_creates_nothing()
    {
        _instructors.Setup(i => i.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((Instructor?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Sut().UpdateMyProfileAsync(5, new InstructorUpdateDto { Specialization = "x" }));
        _instructors.Verify(i => i.Add(It.IsAny<Instructor>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Update_dto_carries_only_Specialization_and_Bio()
    {
        var names = typeof(InstructorUpdateDto).GetProperties().Select(p => p.Name).OrderBy(n => n);
        Assert.Equal(new[] { "Bio", "Specialization" }, names);
    }
}

/// <summary>Proves the controller passes the JWT user id — and only that — to the services.</summary>
public class MeControllerTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<IInstructorService> _instructorService = new();

    private MeController Sut(int userId, string role) => new(_userService.Object, _instructorService.Object)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, role)
                }, "Test"))
            }
        }
    };

    [Theory]
    [InlineData(1, "Admin")]
    [InlineData(5, "Instructor")]
    [InlineData(9, "Member")]
    public async Task GetAccount_looks_up_the_authenticated_user(int userId, string role)
    {
        _userService.Setup(s => s.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new UserResponseDto { Id = userId, Role = role });

        var result = await Sut(userId, role).GetAccount(CancellationToken.None);

        var dto = Assert.IsType<UserResponseDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(userId, dto.Id);
        _userService.Verify(s => s.GetByIdAsync(It.Is<int>(id => id != userId), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Instructor_profile_actions_use_the_authenticated_user_id()
    {
        var update = new InstructorUpdateDto { Specialization = "Spin" };
        _instructorService.Setup(s => s.GetMyProfileAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new InstructorResponseDto());
        _instructorService.Setup(s => s.UpdateMyProfileAsync(5, update, It.IsAny<CancellationToken>())).ReturnsAsync(new InstructorResponseDto());

        await Sut(5, "Instructor").GetInstructorProfile(CancellationToken.None);
        await Sut(5, "Instructor").UpdateInstructorProfile(update, CancellationToken.None);

        _instructorService.Verify(s => s.GetMyProfileAsync(5, It.IsAny<CancellationToken>()), Times.Once);
        _instructorService.Verify(s => s.UpdateMyProfileAsync(5, update, It.IsAny<CancellationToken>()), Times.Once);
        _instructorService.Verify(s => s.UpdateAsync(It.IsAny<int>(), It.IsAny<InstructorUpdateDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>Records every formatted log message plus its structured values.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullLogger.Instance.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? string.Join(" ", pairs.Select(p => $"{p.Key}={p.Value}"))
            : string.Empty;
        Entries.Add($"{formatter(state, exception)} | {values} | {exception}");
    }
}

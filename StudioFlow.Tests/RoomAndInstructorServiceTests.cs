using Moq;
using StudioFlow.Core.DTOs.Instructors;
using StudioFlow.Core.DTOs.Rooms;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Security;
using StudioFlow.Service.Services;

namespace StudioFlow.Tests;

public class RoomServiceTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private RoomService Sut() => new(_rooms.Object, _uow.Object, TestKit.Mapper);

    public RoomServiceTests() =>
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

    [Fact]
    public async Task DeleteAsync_is_blocked_with_ConflictException_when_classes_reference_the_room()
    {
        _rooms.Setup(r => r.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Room(1));
        _rooms.Setup(r => r.HasClassesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => Sut().DeleteAsync(1));
        _rooms.Verify(r => r.Remove(It.IsAny<Room>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_room_and_saves_when_nothing_references_it()
    {
        _rooms.Setup(r => r.GetForUpdateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Room(1));
        _rooms.Setup(r => r.HasClassesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Sut().DeleteAsync(1);

        _rooms.Verify(r => r.Remove(It.IsAny<Room>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_when_missing_throws_NotFoundException()
    {
        _rooms.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().GetByIdAsync(1));
    }

    [Fact]
    public async Task CreateAsync_adds_and_saves_and_returns_the_dto()
    {
        var dto = new RoomCreateDto { Name = "Room X", MaximumCapacity = 12, IsActive = true };
        var result = await Sut().CreateAsync(dto);

        Assert.Equal("Room X", result.Name);
        _rooms.Verify(r => r.Add(It.Is<Room>(x => x.Name == "Room X" && x.MaximumCapacity == 12)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class InstructorServiceTests
{
    private readonly Mock<IInstructorRepository> _instructors = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private InstructorService Sut() => new(
        _instructors.Object, _users.Object, _hasher.Object, _uow.Object, TestKit.Mapper);

    public InstructorServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("HASHED");
    }

    [Fact]
    public async Task CreateAsync_when_the_email_is_taken_throws_ConflictException()
    {
        _users.Setup(u => u.EmailExistsAsync("taken@studioflow.local", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var dto = new InstructorCreateDto { Name = "New", Email = "taken@studioflow.local", Password = "Password123!" };
        await Assert.ThrowsAsync<ConflictException>(() => Sut().CreateAsync(dto));
        _users.Verify(u => u.Add(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_provisions_a_hashed_active_Instructor_user_and_saves_once()
    {
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var dto = new InstructorCreateDto
        {
            Name = "New Coach", Email = "coach@studioflow.local", Password = "Password123!",
            Specialization = "Spin", Bio = "bio"
        };
        await Sut().CreateAsync(dto);

        _hasher.Verify(h => h.Hash("Password123!"), Times.Once);
        _users.Verify(u => u.Add(It.Is<User>(x =>
            x.Email == "coach@studioflow.local" &&
            x.Role == UserRole.Instructor &&
            x.IsActive &&
            x.PasswordHash == "HASHED")), Times.Once);
        _instructors.Verify(i => i.Add(It.IsAny<Instructor>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_is_blocked_with_ConflictException_when_the_instructor_has_classes()
    {
        _instructors.Setup(i => i.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Instructor { Id = 1, UserId = 5 });
        _instructors.Setup(i => i.HasClassesAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => Sut().DeleteAsync(1));
        _instructors.Verify(i => i.Remove(It.IsAny<Instructor>()), Times.Never);
    }
}

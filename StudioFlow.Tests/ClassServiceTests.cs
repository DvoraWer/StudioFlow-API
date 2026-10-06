using Moq;
using StudioFlow.Core.DTOs.Classes;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Service.Services;

namespace StudioFlow.Tests;

public class ClassServiceTests
{
    private readonly Mock<IClassRepository> _classes = new();
    private readonly Mock<IInstructorRepository> _instructors = new();
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IRegistrationRepository> _regs = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private ClassService Sut() => new(
        _classes.Object, _instructors.Object, _rooms.Object, _regs.Object, _uow.Object, TestKit.Mapper);

    public ClassServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _instructors.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Instructor(1));
        _rooms.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Room(1, max: 20));
        _classes.Setup(r => r.InstructorHasOverlapAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _classes.Setup(r => r.RoomHasOverlapAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
    }

    private const int AdminUserId = 90;
    private const int InstructorUserId = 5; // owns instructor profile #1 (TestKit.Instructor default)

    private Task<ClassResponseDto> CreateAsAdmin(ClassCreateDto dto) => Sut().CreateAsync(dto, AdminUserId, UserRole.Admin);

    private Task<ClassResponseDto> CreateAsInstructor(ClassCreateDto dto) => Sut().CreateAsync(dto, InstructorUserId, UserRole.Instructor);

    private void StubReloadAfterCreate() =>
        _classes.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 42, capacity: 10));

    private static ClassCreateDto CreateDto(
        int capacity = 10, DateTime? start = null, DateTime? end = null, int? instructorId = 1) => new()
    {
        Name = "Morning Yoga",
        Description = "Flow",
        InstructorId = instructorId,
        RoomId = 1,
        StartTime = start ?? DateTime.UtcNow.AddDays(3),
        EndTime = end ?? DateTime.UtcNow.AddDays(3).AddHours(1),
        Capacity = capacity
    };

    // ---------- Create ----------------------------------------------------

    [Fact]
    public async Task CreateAsync_valid_request_adds_the_class_saves_once_and_returns_the_dto()
    {
        _classes.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 42, capacity: 10));

        var dto = await CreateAsAdmin(CreateDto());

        Assert.IsType<ClassResponseDto>(dto);
        _classes.Verify(r => r.Add(It.Is<Class>(c => c.Status == ClassStatus.Active && c.RegisteredCount == 0)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_when_EndTime_is_not_after_StartTime_throws_ValidationException()
    {
        var t = DateTime.UtcNow.AddDays(3);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateAsAdmin(CreateDto(start: t, end: t)));
        Assert.Equal("EndTime must be later than StartTime.", ex.Message);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_when_capacity_is_zero_or_less_throws_ValidationException()
        => await Assert.ThrowsAsync<ValidationException>(() => CreateAsAdmin(CreateDto(capacity: 0)));

    [Fact]
    public async Task CreateAsync_when_instructor_does_not_exist_throws_ValidationException()
    {
        _instructors.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Instructor?)null);
        await Assert.ThrowsAsync<ValidationException>(() => CreateAsAdmin(CreateDto()));
    }

    [Fact]
    public async Task CreateAsync_when_room_does_not_exist_throws_ValidationException()
    {
        _rooms.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);
        await Assert.ThrowsAsync<ValidationException>(() => CreateAsAdmin(CreateDto()));
    }

    [Fact]
    public async Task CreateAsync_when_room_is_inactive_throws_ValidationException()
    {
        _rooms.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Room(1, max: 20, active: false));
        await Assert.ThrowsAsync<ValidationException>(() => CreateAsAdmin(CreateDto()));
    }

    [Fact]
    public async Task CreateAsync_when_capacity_exceeds_room_maximum_throws_ValidationException()
    {
        _rooms.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Room(1, max: 5));
        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateAsAdmin(CreateDto(capacity: 6)));
        Assert.Contains("exceeds room", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_when_the_instructor_has_an_overlapping_class_throws_ConflictException()
    {
        _classes.Setup(r => r.InstructorHasOverlapAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        await Assert.ThrowsAsync<ConflictException>(() => CreateAsAdmin(CreateDto()));
    }

    [Fact]
    public async Task CreateAsync_when_the_room_has_an_overlapping_class_throws_ConflictException()
    {
        _classes.Setup(r => r.RoomHasOverlapAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        await Assert.ThrowsAsync<ConflictException>(() => CreateAsAdmin(CreateDto()));
    }

    // ---------- Create: who the class belongs to ------------------------

    [Fact]
    public async Task CreateAsync_admin_can_create_a_class_for_any_valid_instructor()
    {
        StubReloadAfterCreate();
        _instructors.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Instructor(3, userId: 33));

        await CreateAsAdmin(CreateDto(instructorId: 3));

        _classes.Verify(r => r.Add(It.Is<Class>(c => c.InstructorId == 3)), Times.Once);
        _instructors.Verify(r => r.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_admin_without_an_InstructorId_throws_ValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateAsAdmin(CreateDto(instructorId: null)));
        _classes.Verify(r => r.Add(It.IsAny<Class>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_instructor_omitting_InstructorId_gets_the_class_assigned_to_their_own_profile()
    {
        StubReloadAfterCreate();
        _instructors.Setup(r => r.GetByUserIdAsync(InstructorUserId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TestKit.Instructor(1, userId: InstructorUserId));

        var dto = await CreateAsInstructor(CreateDto(instructorId: null));

        Assert.IsType<ClassResponseDto>(dto);
        _classes.Verify(r => r.Add(It.Is<Class>(c => c.InstructorId == 1 && c.Status == ClassStatus.Active)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_instructor_sending_their_own_InstructorId_succeeds()
    {
        StubReloadAfterCreate();
        _instructors.Setup(r => r.GetByUserIdAsync(InstructorUserId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TestKit.Instructor(1, userId: InstructorUserId));

        await CreateAsInstructor(CreateDto(instructorId: 1));

        _classes.Verify(r => r.Add(It.Is<Class>(c => c.InstructorId == 1)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_instructor_cannot_create_a_class_for_another_instructor()
    {
        _instructors.Setup(r => r.GetByUserIdAsync(InstructorUserId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TestKit.Instructor(1, userId: InstructorUserId));
        _instructors.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Instructor(2, userId: 22));

        await Assert.ThrowsAsync<ForbiddenActionException>(() => CreateAsInstructor(CreateDto(instructorId: 2)));

        _classes.Verify(r => r.Add(It.IsAny<Class>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_instructor_role_without_an_instructor_profile_throws_ForbiddenActionException()
    {
        _instructors.Setup(r => r.GetByUserIdAsync(InstructorUserId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Instructor?)null);

        await Assert.ThrowsAsync<ForbiddenActionException>(() => CreateAsInstructor(CreateDto(instructorId: null)));
        _classes.Verify(r => r.Add(It.IsAny<Class>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_member_cannot_create_a_class()
    {
        await Assert.ThrowsAsync<ForbiddenActionException>(
            () => Sut().CreateAsync(CreateDto(), callerUserId: 3, callerRole: UserRole.Member));
        _classes.Verify(r => r.Add(It.IsAny<Class>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_instructor_created_class_still_enforces_room_capacity()
    {
        _instructors.Setup(r => r.GetByUserIdAsync(InstructorUserId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TestKit.Instructor(1, userId: InstructorUserId));
        _rooms.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Room(1, max: 5));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateAsInstructor(CreateDto(capacity: 6, instructorId: null)));
        Assert.Contains("exceeds room", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_instructor_created_class_still_enforces_EndTime_after_StartTime()
    {
        _instructors.Setup(r => r.GetByUserIdAsync(InstructorUserId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TestKit.Instructor(1, userId: InstructorUserId));
        var t = DateTime.UtcNow.AddDays(3);

        await Assert.ThrowsAsync<ValidationException>(() => CreateAsInstructor(CreateDto(start: t, end: t, instructorId: null)));
    }

    [Fact]
    public async Task CreateAsync_instructor_created_class_checks_overlap_against_their_own_schedule()
    {
        _instructors.Setup(r => r.GetByUserIdAsync(InstructorUserId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TestKit.Instructor(1, userId: InstructorUserId));
        _classes.Setup(r => r.InstructorHasOverlapAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => CreateAsInstructor(CreateDto(instructorId: null)));
    }

    [Fact]
    public async Task CreateAsync_instructor_created_class_still_checks_room_overlap()
    {
        _instructors.Setup(r => r.GetByUserIdAsync(InstructorUserId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TestKit.Instructor(1, userId: InstructorUserId));
        _classes.Setup(r => r.RoomHasOverlapAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => CreateAsInstructor(CreateDto(instructorId: null)));
    }

    // ---------- Update / get / cancel -----------------------------------

    [Fact]
    public async Task UpdateAsync_cannot_reduce_capacity_below_the_current_registered_count()
    {
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 5, capacity: 10, registered: 8));

        var dto = new ClassUpdateDto
        {
            Name = "x", Description = "y", InstructorId = 1, RoomId = 1,
            StartTime = DateTime.UtcNow.AddDays(3), EndTime = DateTime.UtcNow.AddDays(3).AddHours(1), Capacity = 5
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Sut().UpdateAsync(5, dto));
        Assert.Contains("below the current", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_when_the_class_does_not_exist_throws_NotFoundException()
    {
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((Class?)null);
        var dto = new ClassUpdateDto
        {
            Name = "x", Description = "y", InstructorId = 1, RoomId = 1,
            StartTime = DateTime.UtcNow.AddDays(3), EndTime = DateTime.UtcNow.AddDays(3).AddHours(1), Capacity = 5
        };
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().UpdateAsync(5, dto));
    }

    [Fact]
    public async Task GetByIdAsync_when_missing_throws_NotFoundException()
    {
        _classes.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((Class?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().GetByIdAsync(5));
    }

    [Fact]
    public async Task CancelAsync_sets_status_to_Cancelled_and_saves_once()
    {
        var cls = TestKit.ActiveClass(id: 5);
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(cls);

        await Sut().CancelAsync(5);

        Assert.Equal(ClassStatus.Cancelled, cls.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_is_idempotent_when_the_class_is_already_cancelled()
    {
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 5, status: ClassStatus.Cancelled));

        await Sut().CancelAsync(5);

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Participants: resource-ownership authorization ----------

    [Fact]
    public async Task GetParticipantsAsync_when_an_instructor_asks_for_a_class_that_is_not_theirs_throws_ForbiddenActionException()
    {
        _classes.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 5, instructorId: 1));
        // caller's instructor profile is #2 — not the owner (#1)
        _instructors.Setup(r => r.GetByUserIdAsync(77, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Instructor { Id = 2, UserId = 77 });

        await Assert.ThrowsAsync<ForbiddenActionException>(
            () => Sut().GetParticipantsAsync(5, callerUserId: 77, callerRole: UserRole.Instructor));
    }

    [Fact]
    public async Task GetParticipantsAsync_for_an_admin_returns_the_active_roster()
    {
        _classes.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.ActiveClass(id: 5));
        _regs.Setup(r => r.GetActiveByClassAsync(5, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { TestKit.ActiveRegistration(3, 5) });

        var roster = await Sut().GetParticipantsAsync(5, callerUserId: 90, callerRole: UserRole.Admin);

        Assert.Single(roster);
        _instructors.Verify(r => r.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

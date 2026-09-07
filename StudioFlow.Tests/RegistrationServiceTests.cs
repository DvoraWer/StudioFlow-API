using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StudioFlow.Core.DTOs.Registrations;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Service.Services;

namespace StudioFlow.Tests;

public class RegistrationServiceTests
{
    private readonly Mock<IClassRepository> _classes = new();
    private readonly Mock<IRegistrationRepository> _regs = new();
    private readonly Mock<IWaitlistRepository> _waitlist = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private RegistrationService Sut() => new(
        _classes.Object, _regs.Object, _waitlist.Object, _users.Object, _uow.Object,
        TestKit.Mapper, NullLogger<RegistrationService>.Instance);

    public RegistrationServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    // ---------- Register ----------------------------------------------------

    [Fact]
    public async Task RegisterAsync_when_a_seat_is_free_creates_registration_bumps_count_and_saves_once()
    {
        var cls = TestKit.ActiveClass(id: 5, capacity: 5, registered: 2);
        _users.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Member(1));
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        _regs.Setup(r => r.GetForUpdateAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync((Registration?)null);
        _regs.Setup(r => r.GetByMemberAsync(1, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { TestKit.ActiveRegistration(1, 5, cls) });

        var dto = await Sut().RegisterAsync(5, 1);

        Assert.IsType<RegistrationResponseDto>(dto);
        Assert.Equal(5, dto.ClassId);
        Assert.Equal(3, cls.RegisteredCount);
        _regs.Verify(r => r.Add(It.Is<Registration>(x =>
            x.MemberId == 1 && x.ClassId == 5 && x.Status == RegistrationStatus.Active)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_when_class_is_full_throws_ConflictException_and_does_not_write()
    {
        _users.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Member(1));
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 5, capacity: 2, registered: 2));
        _regs.Setup(r => r.GetForUpdateAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync((Registration?)null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Sut().RegisterAsync(5, 1));
        Assert.Equal("This class is full.", ex.Message);
        _regs.Verify(r => r.Add(It.IsAny<Registration>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_when_class_is_cancelled_throws_ConflictException()
    {
        _users.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Member(1));
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 5, status: ClassStatus.Cancelled));

        await Assert.ThrowsAsync<ConflictException>(() => Sut().RegisterAsync(5, 1));
    }

    [Fact]
    public async Task RegisterAsync_when_class_has_already_started_throws_ConflictException()
    {
        _users.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Member(1));
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 5, start: DateTime.UtcNow.AddHours(-1)));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Sut().RegisterAsync(5, 1));
        Assert.Equal("This class has already started.", ex.Message);
    }

    [Fact]
    public async Task RegisterAsync_when_member_already_actively_registered_throws_ConflictException()
    {
        var cls = TestKit.ActiveClass(id: 5, capacity: 5, registered: 1);
        _users.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Member(1));
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        _regs.Setup(r => r.GetForUpdateAsync(1, 5, It.IsAny<CancellationToken>()))
             .ReturnsAsync(TestKit.ActiveRegistration(1, 5, cls));

        await Assert.ThrowsAsync<ConflictException>(() => Sut().RegisterAsync(5, 1));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_reactivates_a_previously_cancelled_registration_instead_of_inserting()
    {
        var cls = TestKit.ActiveClass(id: 5, capacity: 5, registered: 0);
        var cancelled = new Registration
        {
            Id = 7, MemberId = 1, ClassId = 5, Status = RegistrationStatus.Cancelled,
            RegisteredAt = DateTime.UtcNow.AddDays(-2)
        };
        _users.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Member(1));
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        _regs.Setup(r => r.GetForUpdateAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(cancelled);
        _regs.Setup(r => r.GetByMemberAsync(1, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { TestKit.ActiveRegistration(1, 5, cls) });

        await Sut().RegisterAsync(5, 1);

        _regs.Verify(r => r.Add(It.IsAny<Registration>()), Times.Never);
        Assert.Equal(RegistrationStatus.Active, cancelled.Status);
        Assert.Equal(1, cls.RegisteredCount);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_when_member_does_not_exist_throws_NotFoundException()
    {
        _users.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().RegisterAsync(5, 1));
    }

    [Fact]
    public async Task RegisterAsync_when_caller_is_not_a_Member_throws_ForbiddenActionException()
    {
        _users.Setup(r => r.GetByIdAsync(90, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Admin(90));
        await Assert.ThrowsAsync<ForbiddenActionException>(() => Sut().RegisterAsync(5, 90));
    }

    // ---------- Cancel + waitlist promotion (spec §18) --------------------

    [Fact]
    public async Task CancelAsync_with_no_waiting_member_cancels_and_decrements_once()
    {
        var cls = TestKit.ActiveClass(id: 5, capacity: 5, registered: 3);
        var reg = TestKit.ActiveRegistration(1, 5, cls);
        _regs.Setup(r => r.GetForUpdateAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(reg);
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        _waitlist.Setup(w => w.GetNextWaitingAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((WaitlistEntry?)null);

        await Sut().CancelAsync(5, 1);

        Assert.Equal(RegistrationStatus.Cancelled, reg.Status);
        Assert.Equal(2, cls.RegisteredCount);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_promotes_the_first_waiting_member_atomically()
    {
        var cls = TestKit.ActiveClass(id: 5, capacity: 2, registered: 2);
        var reg = TestKit.ActiveRegistration(1, 5, cls);
        var waiting = new WaitlistEntry
        {
            Id = 11, MemberId = 99, ClassId = 5, Position = 1,
            JoinedAt = DateTime.UtcNow, Status = WaitlistStatus.Waiting
        };
        _regs.Setup(r => r.GetForUpdateAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(reg);
        _classes.Setup(r => r.GetForUpdateAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        _waitlist.Setup(w => w.GetNextWaitingAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(waiting);
        _regs.Setup(r => r.GetForUpdateAsync(99, 5, It.IsAny<CancellationToken>())).ReturnsAsync((Registration?)null);

        await Sut().CancelAsync(5, 1);

        Assert.Equal(RegistrationStatus.Cancelled, reg.Status);
        Assert.Equal(WaitlistStatus.Promoted, waiting.Status);
        _regs.Verify(r => r.Add(It.Is<Registration>(x =>
            x.MemberId == 99 && x.ClassId == 5 && x.Status == RegistrationStatus.Active)), Times.Once);
        Assert.Equal(2, cls.RegisteredCount); // 2 -> 1 (cancel) -> 2 (promote)
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once); // one transaction
    }

    [Fact]
    public async Task CancelAsync_when_there_is_no_active_registration_throws_NotFoundException()
    {
        _regs.Setup(r => r.GetForUpdateAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync((Registration?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().CancelAsync(5, 1));
    }
}

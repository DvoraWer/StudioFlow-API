using Moq;
using StudioFlow.Core.DTOs.Waitlist;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Service.Services;

namespace StudioFlow.Tests;

public class WaitlistServiceTests
{
    private readonly Mock<IClassRepository> _classes = new();
    private readonly Mock<IWaitlistRepository> _waitlist = new();
    private readonly Mock<IRegistrationRepository> _regs = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private WaitlistService Sut() => new(
        _classes.Object, _waitlist.Object, _regs.Object, _users.Object, _uow.Object, TestKit.Mapper);

    public WaitlistServiceTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _users.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(TestKit.Member(1));
        _regs.Setup(r => r.GetForUpdateAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Registration?)null);
        _waitlist.Setup(w => w.GetForUpdateAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((WaitlistEntry?)null);
    }

    [Fact]
    public async Task JoinAsync_on_a_full_class_adds_the_member_at_the_next_position_and_saves_once()
    {
        var cls = TestKit.ActiveClass(id: 6, capacity: 2, registered: 2);
        _classes.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        _waitlist.Setup(w => w.CountWaitingByClassAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var dto = await Sut().JoinAsync(6, 1);

        Assert.IsType<WaitlistResponseDto>(dto);
        Assert.Equal(1, dto.Position);
        Assert.Equal(cls.Name, dto.ClassName);
        _waitlist.Verify(w => w.Add(It.Is<WaitlistEntry>(x =>
            x.MemberId == 1 && x.ClassId == 6 && x.Position == 1 && x.Status == WaitlistStatus.Waiting)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_assigns_position_after_the_existing_waiters()
    {
        _classes.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 6, capacity: 2, registered: 2));
        _waitlist.Setup(w => w.CountWaitingByClassAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var dto = await Sut().JoinAsync(6, 1);

        Assert.Equal(3, dto.Position);
        _waitlist.Verify(w => w.Add(It.Is<WaitlistEntry>(x => x.Position == 3)), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_when_the_class_still_has_seats_throws_ConflictException()
    {
        _classes.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 6, capacity: 5, registered: 1));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Sut().JoinAsync(6, 1));
        Assert.Contains("register instead", ex.Message);
        _waitlist.Verify(w => w.Add(It.IsAny<WaitlistEntry>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsync_when_the_class_is_cancelled_throws_ConflictException()
    {
        _classes.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 6, capacity: 2, registered: 2, status: ClassStatus.Cancelled));

        await Assert.ThrowsAsync<ConflictException>(() => Sut().JoinAsync(6, 1));
    }

    [Fact]
    public async Task JoinAsync_when_member_is_already_registered_throws_ConflictException()
    {
        var cls = TestKit.ActiveClass(id: 6, capacity: 2, registered: 2);
        _classes.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        _regs.Setup(r => r.GetForUpdateAsync(1, 6, It.IsAny<CancellationToken>()))
             .ReturnsAsync(TestKit.ActiveRegistration(1, 6, cls));

        await Assert.ThrowsAsync<ConflictException>(() => Sut().JoinAsync(6, 1));
    }

    [Fact]
    public async Task JoinAsync_when_member_is_already_waiting_throws_ConflictException()
    {
        _classes.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 6, capacity: 2, registered: 2));
        _waitlist.Setup(w => w.GetForUpdateAsync(1, 6, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new WaitlistEntry { Id = 1, MemberId = 1, ClassId = 6, Status = WaitlistStatus.Waiting, Position = 1 });

        await Assert.ThrowsAsync<ConflictException>(() => Sut().JoinAsync(6, 1));
    }

    [Fact]
    public async Task JoinAsync_reactivates_a_previously_cancelled_waitlist_entry()
    {
        var cls = TestKit.ActiveClass(id: 6, capacity: 2, registered: 2);
        var old = new WaitlistEntry { Id = 3, MemberId = 1, ClassId = 6, Status = WaitlistStatus.Cancelled, Position = 9 };
        _classes.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        _waitlist.Setup(w => w.GetForUpdateAsync(1, 6, It.IsAny<CancellationToken>())).ReturnsAsync(old);
        _waitlist.Setup(w => w.CountWaitingByClassAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = await Sut().JoinAsync(6, 1);

        _waitlist.Verify(w => w.Add(It.IsAny<WaitlistEntry>()), Times.Never);
        Assert.Equal(WaitlistStatus.Waiting, old.Status);
        Assert.Equal(2, old.Position);
        Assert.Equal(2, dto.Position);
    }

    [Fact]
    public async Task LeaveAsync_marks_a_waiting_entry_cancelled_and_saves()
    {
        var entry = new WaitlistEntry { Id = 1, MemberId = 1, ClassId = 6, Status = WaitlistStatus.Waiting, Position = 1 };
        _waitlist.Setup(w => w.GetForUpdateAsync(1, 6, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

        await Sut().LeaveAsync(6, 1);

        Assert.Equal(WaitlistStatus.Cancelled, entry.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveAsync_when_there_is_no_waiting_entry_throws_NotFoundException()
    {
        _waitlist.Setup(w => w.GetForUpdateAsync(1, 6, It.IsAny<CancellationToken>())).ReturnsAsync((WaitlistEntry?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().LeaveAsync(6, 1));
    }
}

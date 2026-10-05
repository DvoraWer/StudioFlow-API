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
        _waitlist.Setup(w => w.GetWaitingByClassForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<WaitlistEntry>());
        // Join and Leave both load the class tracked; tests needing a different class override this.
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 6, capacity: 2, registered: 2));
    }

    [Fact]
    public async Task JoinAsync_on_a_full_class_adds_the_member_at_the_next_position_and_saves_once()
    {
        var cls = TestKit.ActiveClass(id: 6, capacity: 2, registered: 2);
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
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
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 6, capacity: 2, registered: 2));
        _waitlist.Setup(w => w.CountWaitingByClassAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var dto = await Sut().JoinAsync(6, 1);

        Assert.Equal(3, dto.Position);
        _waitlist.Verify(w => w.Add(It.Is<WaitlistEntry>(x => x.Position == 3)), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_when_the_class_still_has_seats_throws_ConflictException()
    {
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 6, capacity: 5, registered: 1));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Sut().JoinAsync(6, 1));
        Assert.Contains("register instead", ex.Message);
        _waitlist.Verify(w => w.Add(It.IsAny<WaitlistEntry>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsync_when_the_class_is_cancelled_throws_ConflictException()
    {
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 6, capacity: 2, registered: 2, status: ClassStatus.Cancelled));

        await Assert.ThrowsAsync<ConflictException>(() => Sut().JoinAsync(6, 1));
    }

    [Fact]
    public async Task JoinAsync_when_member_is_already_registered_throws_ConflictException()
    {
        var cls = TestKit.ActiveClass(id: 6, capacity: 2, registered: 2);
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        _regs.Setup(r => r.GetForUpdateAsync(1, 6, It.IsAny<CancellationToken>()))
             .ReturnsAsync(TestKit.ActiveRegistration(1, 6, cls));

        await Assert.ThrowsAsync<ConflictException>(() => Sut().JoinAsync(6, 1));
    }

    [Fact]
    public async Task JoinAsync_when_member_is_already_waiting_throws_ConflictException()
    {
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>()))
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
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
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

    // ---- Class xmin as the per-class concurrency boundary for waitlist changes ----

    [Fact]
    public async Task JoinAsync_loads_the_class_tracked_and_marks_it_for_the_concurrency_check_before_saving()
    {
        var cls = TestKit.ActiveClass(id: 6, capacity: 2, registered: 2);
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        var markedBeforeSave = false;
        _classes.Setup(r => r.MarkForConcurrencyCheck(cls));
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => markedBeforeSave = _classes.Invocations.Any(i => i.Method.Name == nameof(IClassRepository.MarkForConcurrencyCheck)))
            .ReturnsAsync(1);

        await Sut().JoinAsync(6, 1);

        _classes.Verify(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>()), Times.Once);
        _classes.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _classes.Verify(r => r.MarkForConcurrencyCheck(cls), Times.Once);
        Assert.True(markedBeforeSave);
        Assert.Equal(2, cls.RegisteredCount); // the concurrency touch does not change the value
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_when_validation_fails_does_not_mark_the_class()
    {
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: 6, capacity: 5, registered: 1));

        await Assert.ThrowsAsync<ConflictException>(() => Sut().JoinAsync(6, 1));

        _classes.Verify(r => r.MarkForConcurrencyCheck(It.IsAny<Class>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LeaveAsync_loads_the_class_tracked_and_marks_it_for_the_concurrency_check_before_saving()
    {
        var cls = TestKit.ActiveClass(id: 6, capacity: 2, registered: 2);
        _classes.Setup(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(cls);
        _waitlist.Setup(w => w.GetForUpdateAsync(1, 6, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new WaitlistEntry { Id = 1, MemberId = 1, ClassId = 6, Status = WaitlistStatus.Waiting, Position = 1 });
        var markedBeforeSave = false;
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => markedBeforeSave = _classes.Invocations.Any(i => i.Method.Name == nameof(IClassRepository.MarkForConcurrencyCheck)))
            .ReturnsAsync(1);

        await Sut().LeaveAsync(6, 1);

        _classes.Verify(r => r.GetForUpdateAsync(6, It.IsAny<CancellationToken>()), Times.Once);
        _classes.Verify(r => r.MarkForConcurrencyCheck(cls), Times.Once);
        Assert.True(markedBeforeSave);
        Assert.Equal(2, cls.RegisteredCount);
    }

    [Fact]
    public async Task LeaveAsync_when_there_is_no_waiting_entry_does_not_mark_the_class()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Sut().LeaveAsync(6, 1));

        _classes.Verify(r => r.MarkForConcurrencyCheck(It.IsAny<Class>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsync_translates_a_concurrency_conflict_into_the_waitlist_message()
    {
        var original = new ConcurrencyConflictException();
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(original);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Sut().JoinAsync(6, 1));

        Assert.Equal(ConcurrencyConflictException.WaitlistMessage, ex.Message);
        Assert.Same(original, ex.InnerException);
    }

    [Fact]
    public async Task LeaveAsync_translates_a_concurrency_conflict_into_the_waitlist_message()
    {
        _waitlist.Setup(w => w.GetForUpdateAsync(1, 6, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new WaitlistEntry { Id = 1, MemberId = 1, ClassId = 6, Status = WaitlistStatus.Waiting, Position = 1 });
        var original = new ConcurrencyConflictException();
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(original);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Sut().LeaveAsync(6, 1));

        Assert.Equal(ConcurrencyConflictException.WaitlistMessage, ex.Message);
        Assert.Same(original, ex.InnerException);
    }

    // ---- Position = current place in the Waiting queue (1..n, no gaps) ----

    /// <summary>
    /// Backs the waitlist mock with an in-memory list that behaves like EF: entries are
    /// tracked objects, and the "Waiting" queries reflect the last saved state, so an entry
    /// changed (but not yet saved) in the current unit of work is still returned.
    /// </summary>
    private readonly List<WaitlistEntry> _store = new();
    private HashSet<int> _savedWaitingIds = new();

    private void UseStore(int classId, params WaitlistEntry[] seed)
    {
        _store.AddRange(seed);
        Persist();

        _classes.Setup(r => r.GetForUpdateAsync(classId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestKit.ActiveClass(id: classId, capacity: 2, registered: 2));
        _users.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((int id, CancellationToken _) => TestKit.Member(id));
        _regs.Setup(r => r.GetForUpdateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Registration?)null);

        _waitlist.Setup(w => w.GetForUpdateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((int m, int c, CancellationToken _) => _store.FirstOrDefault(e => e.MemberId == m && e.ClassId == c));
        _waitlist.Setup(w => w.CountWaitingByClassAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((int c, CancellationToken _) => _store.Count(e => e.ClassId == c && _savedWaitingIds.Contains(e.Id)));
        _waitlist.Setup(w => w.GetWaitingByClassForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((int c, CancellationToken _) => (IReadOnlyList<WaitlistEntry>)_store
                     .Where(e => e.ClassId == c && _savedWaitingIds.Contains(e.Id))
                     .OrderBy(e => e.JoinedAt).ThenBy(e => e.Id)
                     .ToList());
        _waitlist.Setup(w => w.Add(It.IsAny<WaitlistEntry>()))
                 .Callback((WaitlistEntry e) => { e.Id = _store.Count == 0 ? 1 : _store.Max(x => x.Id) + 1; _store.Add(e); });

        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(Persist)
            .ReturnsAsync(1);
    }

    private void Persist() =>
        _savedWaitingIds = _store.Where(e => e.Status == WaitlistStatus.Waiting).Select(e => e.Id).ToHashSet();

    private static readonly DateTime T0 = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private static WaitlistEntry Waiting(int id, int memberId, int position, int minutesAfterT0) => new()
    {
        Id = id, MemberId = memberId, ClassId = 6, Position = position,
        JoinedAt = T0.AddMinutes(minutesAfterT0), Status = WaitlistStatus.Waiting
    };

    private int PositionOf(int memberId) => _store.Single(e => e.MemberId == memberId).Position;

    [Fact]
    public async Task JoinAsync_three_members_in_turn_get_positions_1_2_and_3()
    {
        UseStore(6);

        var a = await Sut().JoinAsync(6, 10);
        var b = await Sut().JoinAsync(6, 11);
        var c = await Sut().JoinAsync(6, 12);

        Assert.Equal(1, a.Position);
        Assert.Equal(2, b.Position);
        Assert.Equal(3, c.Position);
        Assert.Equal(new[] { 1, 2, 3 }, new[] { PositionOf(10), PositionOf(11), PositionOf(12) });
    }

    [Fact]
    public async Task LeaveAsync_when_the_first_member_leaves_the_rest_become_1_and_2()
    {
        var a = Waiting(1, 10, 1, 0);
        var b = Waiting(2, 11, 2, 1);
        var c = Waiting(3, 12, 3, 2);
        UseStore(6, a, b, c);
        var joinedB = b.JoinedAt;
        var joinedC = c.JoinedAt;

        await Sut().LeaveAsync(6, 10);

        Assert.Equal(WaitlistStatus.Cancelled, a.Status);
        Assert.Equal(1, b.Position);
        Assert.Equal(2, c.Position);
        Assert.Equal(joinedB, b.JoinedAt); // FIFO data untouched
        Assert.Equal(joinedC, c.JoinedAt);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once); // one transaction
    }

    [Fact]
    public async Task LeaveAsync_when_a_middle_member_leaves_the_rest_are_reindexed_without_gaps()
    {
        var a = Waiting(1, 10, 1, 0);
        var b = Waiting(2, 11, 2, 1);
        var c = Waiting(3, 12, 3, 2);
        var d = Waiting(4, 13, 4, 3);
        UseStore(6, a, b, c, d);

        await Sut().LeaveAsync(6, 11);

        Assert.Equal(WaitlistStatus.Cancelled, b.Status);
        Assert.Equal(1, a.Position);
        Assert.Equal(2, c.Position);
        Assert.Equal(3, d.Position);
    }

    [Fact]
    public async Task JoinAsync_after_someone_left_gives_the_new_member_the_next_sequential_position()
    {
        UseStore(6, Waiting(1, 10, 1, 0), Waiting(2, 11, 2, 1), Waiting(3, 12, 3, 2));

        await Sut().LeaveAsync(6, 10);
        var d = await Sut().JoinAsync(6, 13);

        Assert.Equal(3, d.Position);
        Assert.Equal(new[] { 1, 2, 3 }, new[] { PositionOf(11), PositionOf(12), PositionOf(13) });
    }

    [Fact]
    public async Task LeaveAsync_reindexes_in_JoinedAt_order_not_by_the_stored_Position()
    {
        // Stored positions deliberately disagree with join order (e.g. legacy gappy data).
        var early = Waiting(1, 10, 7, 0);
        var middle = Waiting(2, 11, 2, 5);
        var late = Waiting(3, 12, 1, 9);
        var leaving = Waiting(4, 13, 4, 7);
        UseStore(6, early, middle, late, leaving);

        await Sut().LeaveAsync(6, 13);

        Assert.Equal(1, early.Position);
        Assert.Equal(2, middle.Position);
        Assert.Equal(3, late.Position);
    }
}

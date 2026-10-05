using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Data.Context;
using StudioFlow.Data.Repositories;
using StudioFlow.Service.Services;

namespace StudioFlow.Tests;

/// <summary>
/// Waitlist concurrency proof against the real PostgreSQL database — same deterministic
/// two-DbContext pattern as <see cref="LastSeatConcurrencyProofTests"/>, no timing races.
///
/// Waitlist join/leave use the Class xmin as the per-class concurrency boundary: they
/// force a no-op UPDATE of the Class row (IClassRepository.MarkForConcurrencyCheck), so a
/// request that read the class before another waitlist change committed loses with
/// <see cref="ConcurrencyConflictException"/> (HTTP 409) and writes nothing.
///
/// To drive the real <see cref="WaitlistService"/> deterministically, the losing context
/// loads the Class before the winner commits. EF's identity resolution then makes the
/// service's own GetForUpdateAsync return that already-tracked instance with the old xmin —
/// exactly the "read before the other request committed" interleaving.
///
/// Seed: a full class (capacity 1, 1 registered) with two members already waiting
/// (positions 1 and 2) and two more members who will try to join.
///
/// Requires the studioflow-pg container running (override with STUDIOFLOW_TEST_CONNECTION).
/// </summary>
[Trait("Category", "Integration")]
public sealed class WaitlistConcurrencyProofTests : IAsyncLifetime
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("STUDIOFLOW_TEST_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=studioflow;Username=postgres;Password=postgres";

    private readonly string _tag = "zz-wlconc-" + Guid.NewGuid().ToString("N");

    private bool _dbAvailable;
    private int _roomId, _instructorId, _instructorUserId, _classId;
    private int _waiting1Id, _waiting2Id, _joinerCId, _joinerDId;

    private static DbContextOptions<StudioFlowDbContext> Options() =>
        new DbContextOptionsBuilder<StudioFlowDbContext>().UseNpgsql(ConnectionString).Options;

    private static WaitlistService Service(StudioFlowDbContext db) => new(
        new ClassRepository(db), new WaitlistRepository(db), new RegistrationRepository(db),
        new UserRepository(db), new UnitOfWork(db), TestKit.Mapper);

    public async Task InitializeAsync()
    {
        try
        {
            await using var probe = new StudioFlowDbContext(Options());
            _dbAvailable = await probe.Database.CanConnectAsync();
        }
        catch
        {
            _dbAvailable = false;
        }

        if (!_dbAvailable)
        {
            return;
        }

        await using var db = new StudioFlowDbContext(Options());

        var room = new Room { Name = $"{_tag}-room", MaximumCapacity = 1, IsActive = true };
        var instructorUser = new User
        {
            Name = $"{_tag}-instr", Email = $"{_tag}-instr@test.local",
            PasswordHash = "not-a-hash", Role = UserRole.Instructor, IsActive = true
        };
        var instructor = new Instructor { User = instructorUser, Specialization = "test" };
        User NewMember(string name) => new()
        {
            Name = $"{_tag}-{name}", Email = $"{_tag}-{name}@test.local",
            PasswordHash = "not-a-hash", Role = UserRole.Member, IsActive = true
        };
        var w1 = NewMember("w1");
        var w2 = NewMember("w2");
        var c = NewMember("c");
        var d = NewMember("d");
        var start = DateTime.UtcNow.AddDays(7);
        var cls = new Class
        {
            Name = $"{_tag}-class", Description = "waitlist concurrency proof", Instructor = instructor, Room = room,
            StartTime = start, EndTime = start.AddHours(1),
            Capacity = 1, RegisteredCount = 1, Status = ClassStatus.Active // full → joins go to the waitlist
        };

        db.AddRange(room, instructor, cls, w1, w2, c, d);
        await db.SaveChangesAsync();

        var t0 = DateTime.UtcNow.AddHours(-1);
        db.WaitlistEntries.AddRange(
            new WaitlistEntry { MemberId = w1.Id, ClassId = cls.Id, JoinedAt = t0, Position = 1, Status = WaitlistStatus.Waiting },
            new WaitlistEntry { MemberId = w2.Id, ClassId = cls.Id, JoinedAt = t0.AddMinutes(1), Position = 2, Status = WaitlistStatus.Waiting });
        await db.SaveChangesAsync();

        _roomId = room.Id;
        _instructorId = instructor.Id;
        _instructorUserId = instructorUser.Id;
        _classId = cls.Id;
        _waiting1Id = w1.Id;
        _waiting2Id = w2.Id;
        _joinerCId = c.Id;
        _joinerDId = d.Id;
    }

    private void RequireDb() =>
        Assert.True(_dbAvailable,
            "PostgreSQL not reachable. This proof needs the studioflow-pg container running " +
            "(or STUDIOFLOW_TEST_CONNECTION pointing at a migrated StudioFlow database).");

    /// <summary>Waiting entries of the test class in FIFO order (JoinedAt, then Id), from a fresh context.</summary>
    private async Task<List<WaitlistEntry>> WaitingInFifoOrderAsync()
    {
        await using var db = new StudioFlowDbContext(Options());
        return await db.WaitlistEntries.AsNoTracking()
            .Where(w => w.ClassId == _classId && w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.JoinedAt).ThenBy(w => w.Id)
            .ToListAsync();
    }

    private async Task<Class> FreshClassAsync()
    {
        await using var db = new StudioFlowDbContext(Options());
        return await db.Classes.AsNoTracking().FirstAsync(c => c.Id == _classId);
    }

    private static void AssertSequentialQueue(List<WaitlistEntry> waiting, params int[] expectedMemberIdsInFifoOrder)
    {
        Assert.Equal(expectedMemberIdsInFifoOrder, waiting.Select(w => w.MemberId).ToArray());
        Assert.Equal(Enumerable.Range(1, waiting.Count).ToArray(), waiting.Select(w => w.Position).ToArray());
    }

    // ---- Scenario A: two joins race for the same class ---------------------------------

    [Fact]
    public async Task Two_concurrent_joins_reading_the_same_xmin_and_count_yield_one_success_one_conflict_and_no_duplicate_position()
    {
        RequireDb();

        await using var ctxA = new StudioFlowDbContext(Options());
        await using var ctxB = new StudioFlowDbContext(Options());
        var classesA = new ClassRepository(ctxA);
        var classesB = new ClassRepository(ctxB);
        var waitlistA = new WaitlistRepository(ctxA);
        var waitlistB = new WaitlistRepository(ctxB);

        // Both requests read the same class state (same xmin) and the same queue length —
        // the exact situation that used to hand out the same Position twice.
        var classA = await classesA.GetForUpdateAsync(_classId);
        var classB = await classesB.GetForUpdateAsync(_classId);
        Assert.NotNull(classA);
        Assert.NotNull(classB);
        Assert.Equal(classA.Version, classB.Version);
        var xminBefore = classA.Version;

        var countA = await waitlistA.CountWaitingByClassAsync(_classId);
        var countB = await waitlistB.CountWaitingByClassAsync(_classId);
        Assert.Equal(2, countA);
        Assert.Equal(2, countB);

        // Request A (member C) joins: insert + no-op Class UPDATE in one SaveChanges — wins.
        waitlistA.Add(new WaitlistEntry
        {
            MemberId = _joinerCId, ClassId = _classId, JoinedAt = DateTime.UtcNow,
            Position = countA + 1, Status = WaitlistStatus.Waiting
        });
        classesA.MarkForConcurrencyCheck(classA);
        await new UnitOfWork(ctxA).SaveChangesAsync();

        // EF really issued the UPDATE: PostgreSQL advanced xmin, the business value is unchanged.
        var afterA = await FreshClassAsync();
        Assert.NotEqual(xminBefore, afterA.Version);
        Assert.Equal(1, afterA.RegisteredCount);

        // Request B (member D) tries to take the same Position 3 against the stale xmin — must lose.
        waitlistB.Add(new WaitlistEntry
        {
            MemberId = _joinerDId, ClassId = _classId, JoinedAt = DateTime.UtcNow,
            Position = countB + 1, Status = WaitlistStatus.Waiting
        });
        classesB.MarkForConcurrencyCheck(classB);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new UnitOfWork(ctxB).SaveChangesAsync());

        // The loser's insert was rolled back with its failed Class UPDATE.
        await using (var verify = new StudioFlowDbContext(Options()))
        {
            Assert.False(await verify.WaitlistEntries.AnyAsync(w => w.ClassId == _classId && w.MemberId == _joinerDId));
            var next = await new WaitlistRepository(verify).GetNextWaitingAsync(_classId);
            Assert.Equal(_waiting1Id, next!.MemberId); // FIFO head unchanged (JoinedAt, Id)
        }

        AssertSequentialQueue(await WaitingInFifoOrderAsync(), _waiting1Id, _waiting2Id, _joinerCId);
    }

    [Fact]
    public async Task Concurrent_joins_through_WaitlistService_the_stale_request_gets_the_waitlist_409_and_writes_nothing()
    {
        RequireDb();

        await using var ctxA = new StudioFlowDbContext(Options());
        await using var ctxB = new StudioFlowDbContext(Options());

        // Request B has read the class before A commits.
        var staleClass = await new ClassRepository(ctxB).GetForUpdateAsync(_classId);
        var staleXmin = staleClass!.Version;

        var joinedC = await Service(ctxA).JoinAsync(_classId, _joinerCId);
        Assert.Equal(3, joinedC.Position);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => Service(ctxB).JoinAsync(_classId, _joinerDId));
        Assert.Equal(ConcurrencyConflictException.WaitlistMessage, ex.Message);
        Assert.Equal(staleXmin, staleClass.Version); // B really ran against the old xmin

        await using (var verify = new StudioFlowDbContext(Options()))
        {
            Assert.False(await verify.WaitlistEntries.AnyAsync(w => w.ClassId == _classId && w.MemberId == _joinerDId));
        }
        AssertSequentialQueue(await WaitingInFifoOrderAsync(), _waiting1Id, _waiting2Id, _joinerCId);

        // A retry with fresh state succeeds and takes the next sequential Position.
        await using var retry = new StudioFlowDbContext(Options());
        var joinedD = await Service(retry).JoinAsync(_classId, _joinerDId);
        Assert.Equal(4, joinedD.Position);
        AssertSequentialQueue(await WaitingInFifoOrderAsync(), _waiting1Id, _waiting2Id, _joinerCId, _joinerDId);
    }

    // ---- Scenario B: a join races a leave ----------------------------------------------

    [Fact]
    public async Task Join_wins_over_a_concurrent_leave_the_leave_gets_409_and_the_queue_is_not_partially_reindexed()
    {
        RequireDb();

        await using var ctxJoin = new StudioFlowDbContext(Options());
        await using var ctxLeave = new StudioFlowDbContext(Options());

        // The leave request has read the class before the join commits.
        _ = await new ClassRepository(ctxLeave).GetForUpdateAsync(_classId);

        var joinedC = await Service(ctxJoin).JoinAsync(_classId, _joinerCId);
        Assert.Equal(3, joinedC.Position);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => Service(ctxLeave).LeaveAsync(_classId, _waiting1Id));
        Assert.Equal(ConcurrencyConflictException.WaitlistMessage, ex.Message);

        // Nothing from the losing leave was written: W1 still waiting, no positions shifted.
        AssertSequentialQueue(await WaitingInFifoOrderAsync(), _waiting1Id, _waiting2Id, _joinerCId);

        // Retrying the leave with fresh state reindexes the whole queue without gaps.
        await using var retry = new StudioFlowDbContext(Options());
        await Service(retry).LeaveAsync(_classId, _waiting1Id);
        AssertSequentialQueue(await WaitingInFifoOrderAsync(), _waiting2Id, _joinerCId);
    }

    [Fact]
    public async Task Leave_wins_over_a_concurrent_join_the_join_gets_409_and_leaves_no_entry()
    {
        RequireDb();

        await using var ctxJoin = new StudioFlowDbContext(Options());
        await using var ctxLeave = new StudioFlowDbContext(Options());

        // The join request has read the class before the leave commits.
        _ = await new ClassRepository(ctxJoin).GetForUpdateAsync(_classId);

        await Service(ctxLeave).LeaveAsync(_classId, _waiting1Id);
        AssertSequentialQueue(await WaitingInFifoOrderAsync(), _waiting2Id);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => Service(ctxJoin).JoinAsync(_classId, _joinerCId));
        Assert.Equal(ConcurrencyConflictException.WaitlistMessage, ex.Message);

        await using (var verify = new StudioFlowDbContext(Options()))
        {
            Assert.False(await verify.WaitlistEntries.AnyAsync(w => w.ClassId == _classId && w.MemberId == _joinerCId));
        }
        AssertSequentialQueue(await WaitingInFifoOrderAsync(), _waiting2Id);

        await using var retry = new StudioFlowDbContext(Options());
        var joinedC = await Service(retry).JoinAsync(_classId, _joinerCId);
        Assert.Equal(2, joinedC.Position);
        AssertSequentialQueue(await WaitingInFifoOrderAsync(), _waiting2Id, _joinerCId);
    }

    public async Task DisposeAsync()
    {
        if (!_dbAvailable)
        {
            return;
        }

        await using var db = new StudioFlowDbContext(Options());
        var memberIds = new[] { _waiting1Id, _waiting2Id, _joinerCId, _joinerDId };
        await db.WaitlistEntries.Where(w => w.ClassId == _classId).ExecuteDeleteAsync();
        await db.Classes.Where(c => c.Id == _classId).ExecuteDeleteAsync();
        await db.Instructors.Where(i => i.Id == _instructorId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == _instructorUserId || memberIds.Contains(u.Id)).ExecuteDeleteAsync();
        await db.Rooms.Where(r => r.Id == _roomId).ExecuteDeleteAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Data.Context;
using StudioFlow.Data.Repositories;

namespace StudioFlow.Tests;

/// <summary>
/// Proves against the real PostgreSQL database that FIFO promotion order is JoinedAt
/// (then Id), never Position. Positions are seeded to contradict join order on purpose.
///
/// Requires the studioflow-pg container running (same setup as
/// <see cref="LastSeatConcurrencyProofTests"/>; override with STUDIOFLOW_TEST_CONNECTION).
/// </summary>
[Trait("Category", "Integration")]
public sealed class WaitlistFifoOrderTests : IAsyncLifetime
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("STUDIOFLOW_TEST_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=studioflow;Username=postgres;Password=postgres";

    private readonly string _tag = "zz-fifo-" + Guid.NewGuid().ToString("N");

    private bool _dbAvailable;
    private int _roomId, _instructorId, _instructorUserId, _classId;
    private readonly List<int> _memberIds = new();
    private int _earliestEntryId, _tieLowIdEntryId;

    private static DbContextOptions<StudioFlowDbContext> Options() =>
        new DbContextOptionsBuilder<StudioFlowDbContext>().UseNpgsql(ConnectionString).Options;

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
        var members = Enumerable.Range(0, 3).Select(i => new User
        {
            Name = $"{_tag}-m{i}", Email = $"{_tag}-m{i}@test.local",
            PasswordHash = "not-a-hash", Role = UserRole.Member, IsActive = true
        }).ToList();
        var start = DateTime.UtcNow.AddDays(7);
        var cls = new Class
        {
            Name = $"{_tag}-class", Description = "fifo proof", Instructor = instructor, Room = room,
            StartTime = start, EndTime = start.AddHours(1),
            Capacity = 1, RegisteredCount = 1, Status = ClassStatus.Active
        };

        db.AddRange(room, instructor, cls);
        db.AddRange(members);
        await db.SaveChangesAsync();

        var t0 = DateTime.UtcNow.AddHours(-1);
        // Earliest joiner has the WORST Position; the two later joiners share a JoinedAt.
        var earliest = new WaitlistEntry { MemberId = members[0].Id, ClassId = cls.Id, JoinedAt = t0, Position = 3, Status = WaitlistStatus.Waiting };
        db.WaitlistEntries.Add(earliest);
        await db.SaveChangesAsync();
        var tieLowId = new WaitlistEntry { MemberId = members[1].Id, ClassId = cls.Id, JoinedAt = t0.AddMinutes(5), Position = 2, Status = WaitlistStatus.Waiting };
        db.WaitlistEntries.Add(tieLowId);
        await db.SaveChangesAsync();
        var tieHighId = new WaitlistEntry { MemberId = members[2].Id, ClassId = cls.Id, JoinedAt = t0.AddMinutes(5), Position = 1, Status = WaitlistStatus.Waiting };
        db.WaitlistEntries.Add(tieHighId);
        await db.SaveChangesAsync();

        _roomId = room.Id;
        _instructorId = instructor.Id;
        _instructorUserId = instructorUser.Id;
        _classId = cls.Id;
        _memberIds.AddRange(members.Select(m => m.Id));
        _earliestEntryId = earliest.Id;
        _tieLowIdEntryId = tieLowId.Id;
    }

    [Fact]
    public async Task GetNextWaitingAsync_picks_the_earliest_JoinedAt_then_lowest_Id_regardless_of_Position()
    {
        Assert.True(_dbAvailable,
            "PostgreSQL not reachable. This test needs the studioflow-pg container running " +
            "(or STUDIOFLOW_TEST_CONNECTION pointing at a migrated StudioFlow database).");

        await using var db = new StudioFlowDbContext(Options());
        var repo = new WaitlistRepository(db);

        var next = await repo.GetNextWaitingAsync(_classId);
        Assert.NotNull(next);
        Assert.Equal(_earliestEntryId, next.Id); // Position 3, but joined first

        // Once the earliest is gone, the JoinedAt tie is broken by Id, not by Position.
        next.Status = WaitlistStatus.Promoted;
        await db.SaveChangesAsync();

        var after = await repo.GetNextWaitingAsync(_classId);
        Assert.NotNull(after);
        Assert.Equal(_tieLowIdEntryId, after.Id); // Position 2, but lower Id than the Position-1 entry
    }

    public async Task DisposeAsync()
    {
        if (!_dbAvailable)
        {
            return;
        }

        await using var db = new StudioFlowDbContext(Options());
        await db.WaitlistEntries.Where(w => w.ClassId == _classId).ExecuteDeleteAsync();
        await db.Classes.Where(c => c.Id == _classId).ExecuteDeleteAsync();
        await db.Instructors.Where(i => i.Id == _instructorId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == _instructorUserId || _memberIds.Contains(u.Id)).ExecuteDeleteAsync();
        await db.Rooms.Where(r => r.Id == _roomId).ExecuteDeleteAsync();
    }
}

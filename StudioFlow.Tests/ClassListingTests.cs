using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.DTOs.Classes;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Data.Context;
using StudioFlow.Data.Repositories;

namespace StudioFlow.Tests;

/// <summary>
/// Proves against the real PostgreSQL database that the paged class list
/// (ClassRepository.GetPagedAsync → GET /api/classes) excludes classes whose StartTime
/// has passed, and that this filter runs before Count and Skip/Take.
///
/// The started classes have the EARLIEST start times, so with the list's StartTime
/// ordering they would fill page 1 if the filter were applied after paging.
/// Every seeded class name carries a unique tag, and the queries search by it, so
/// other data in the database cannot affect the results.
///
/// Requires the studioflow-pg container running (same setup as
/// <see cref="LastSeatConcurrencyProofTests"/>; override with STUDIOFLOW_TEST_CONNECTION).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ClassListingTests : IAsyncLifetime
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("STUDIOFLOW_TEST_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=studioflow;Username=postgres;Password=postgres";

    private readonly string _tag = "zz-list-" + Guid.NewGuid().ToString("N");

    private bool _dbAvailable;
    private int _roomId, _instructorId, _instructorUserId;
    private int _future1Id, _future2Id, _future3Id;

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

        var room = new Room { Name = $"{_tag}-room", MaximumCapacity = 10, IsActive = true };
        var instructorUser = new User
        {
            Name = $"{_tag}-instr", Email = $"{_tag}-instr@test.local",
            PasswordHash = "not-a-hash", Role = UserRole.Instructor, IsActive = true
        };
        var instructor = new Instructor { User = instructorUser, Specialization = "test" };

        var now = DateTime.UtcNow;
        Class NewClass(string name, DateTime start) => new()
        {
            Name = $"{_tag}-{name}", Description = "class listing test", Instructor = instructor, Room = room,
            StartTime = start, EndTime = start.AddHours(1),
            Capacity = 5, RegisteredCount = 0, Status = ClassStatus.Active
        };

        var longStarted = NewClass("started-yesterday", now.AddDays(-1));
        var justStarted = NewClass("started-just-now", now.AddSeconds(-1)); // still running (EndTime in the future)
        var future1 = NewClass("future-1", now.AddDays(1));
        var future2 = NewClass("future-2", now.AddDays(2));
        var future3 = NewClass("future-3", now.AddDays(3));

        db.AddRange(room, instructor, longStarted, justStarted, future1, future2, future3);
        await db.SaveChangesAsync();

        _roomId = room.Id;
        _instructorId = instructor.Id;
        _instructorUserId = instructorUser.Id;
        _future1Id = future1.Id;
        _future2Id = future2.Id;
        _future3Id = future3.Id;
    }

    private void RequireDb() =>
        Assert.True(_dbAvailable,
            "PostgreSQL not reachable. This test needs the studioflow-pg container running " +
            "(or STUDIOFLOW_TEST_CONNECTION pointing at a migrated StudioFlow database).");

    [Fact]
    public async Task GetPagedAsync_excludes_classes_that_have_already_started_and_returns_future_ones()
    {
        RequireDb();

        await using var db = new StudioFlowDbContext(Options());
        var page = await new ClassRepository(db).GetPagedAsync(
            new ClassQueryParameters { Search = _tag, Page = 1, PageSize = 100 });

        Assert.Equal(new[] { _future1Id, _future2Id, _future3Id }, page.Items.Select(c => c.Id).ToArray());
        Assert.All(page.Items, c => Assert.True(c.StartTime > DateTime.UtcNow));
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_filters_started_classes_before_count_and_skip_take()
    {
        RequireDb();

        await using var db = new StudioFlowDbContext(Options());
        var repo = new ClassRepository(db);

        // Ordered by StartTime, the two started classes come first — if they were removed
        // only after paging, page 1 would be short or empty and TotalCount would be 5.
        var page1 = await repo.GetPagedAsync(new ClassQueryParameters { Search = _tag, Page = 1, PageSize = 2 });
        var page2 = await repo.GetPagedAsync(new ClassQueryParameters { Search = _tag, Page = 2, PageSize = 2 });

        Assert.Equal(new[] { _future1Id, _future2Id }, page1.Items.Select(c => c.Id).ToArray());
        Assert.Equal(new[] { _future3Id }, page2.Items.Select(c => c.Id).ToArray());
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(3, page2.TotalCount);
    }

    public async Task DisposeAsync()
    {
        if (!_dbAvailable)
        {
            return;
        }

        await using var db = new StudioFlowDbContext(Options());
        await db.Classes.Where(c => c.RoomId == _roomId).ExecuteDeleteAsync();
        await db.Instructors.Where(i => i.Id == _instructorId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == _instructorUserId).ExecuteDeleteAsync();
        await db.Rooms.Where(r => r.Id == _roomId).ExecuteDeleteAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Data.Context;
using StudioFlow.Data.Repositories;

namespace StudioFlow.Tests;

/// <summary>
/// The dedicated last-seat concurrency proof (spec §37, §38).
///
/// This is NOT a mock. It opens two independent <see cref="StudioFlowDbContext"/>
/// instances against the real PostgreSQL database, both reading the same class
/// (capacity 1, RegisteredCount 0). One commits; the other's commit fails on the
/// stale <c>xmin</c> concurrency token and is translated by the Data layer's
/// <see cref="UnitOfWork"/> into <see cref="ConcurrencyConflictException"/>.
/// The database ends with exactly one registration.
///
/// Requires the studioflow-pg container running. Override the connection with the
/// STUDIOFLOW_TEST_CONNECTION environment variable; otherwise the test is skipped.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LastSeatConcurrencyProofTests : IAsyncLifetime
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("STUDIOFLOW_TEST_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=studioflow;Username=postgres;Password=postgres";

    private readonly string _tag = "zz-concurrency-" + Guid.NewGuid().ToString("N");

    private bool _dbAvailable;
    private int _roomId, _instructorId, _instructorUserId, _classId, _memberAId, _memberBId;

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
        var memberA = new User
        {
            Name = $"{_tag}-A", Email = $"{_tag}-a@test.local",
            PasswordHash = "not-a-hash", Role = UserRole.Member, IsActive = true
        };
        var memberB = new User
        {
            Name = $"{_tag}-B", Email = $"{_tag}-b@test.local",
            PasswordHash = "not-a-hash", Role = UserRole.Member, IsActive = true
        };
        var start = DateTime.UtcNow.AddDays(7);
        var cls = new Class
        {
            Name = $"{_tag}-class", Description = "concurrency proof", Instructor = instructor, Room = room,
            StartTime = start, EndTime = start.AddHours(1),
            Capacity = 1, RegisteredCount = 0, Status = ClassStatus.Active
        };

        db.AddRange(room, instructor, memberA, memberB, cls);
        await db.SaveChangesAsync();

        _roomId = room.Id;
        _instructorId = instructor.Id;
        _instructorUserId = instructorUser.Id;
        _memberAId = memberA.Id;
        _memberBId = memberB.Id;
        _classId = cls.Id;
    }

    [Fact]
    public async Task Two_concurrent_registrations_for_the_final_seat_yield_one_success_one_conflict_and_one_row()
    {
        Assert.True(_dbAvailable,
            "PostgreSQL not reachable. This proof needs the studioflow-pg container running " +
            "(or STUDIOFLOW_TEST_CONNECTION pointing at a migrated StudioFlow database).");

        await using var ctxA = new StudioFlowDbContext(Options());
        await using var ctxB = new StudioFlowDbContext(Options());

        // Both contexts read the same class state — one free seat, identical xmin.
        var classA = await ctxA.Classes.FirstAsync(c => c.Id == _classId);
        var classB = await ctxB.Classes.FirstAsync(c => c.Id == _classId);
        Assert.Equal(0, classA.RegisteredCount);
        Assert.Equal(0, classB.RegisteredCount);

        // Member A registers — this commit wins.
        classA.RegisteredCount++;
        ctxA.Registrations.Add(new Registration
        {
            MemberId = _memberAId, ClassId = _classId, RegisteredAt = DateTime.UtcNow, Status = RegistrationStatus.Active
        });
        var written = await new UnitOfWork(ctxA).SaveChangesAsync();
        Assert.True(written >= 2); // UPDATE Classes + INSERT Registration

        // Member B registers against the now-stale class row — must lose on xmin.
        classB.RegisteredCount++;
        ctxB.Registrations.Add(new Registration
        {
            MemberId = _memberBId, ClassId = _classId, RegisteredAt = DateTime.UtcNow, Status = RegistrationStatus.Active
        });

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => new UnitOfWork(ctxB).SaveChangesAsync());

        // The database is the source of truth: exactly one active registration, count == 1.
        await using var verify = new StudioFlowDbContext(Options());
        var registrations = await verify.Registrations
            .Where(r => r.ClassId == _classId)
            .ToListAsync();

        Assert.Single(registrations);
        Assert.Equal(_memberAId, registrations[0].MemberId);
        Assert.Equal(RegistrationStatus.Active, registrations[0].Status);

        var finalClass = await verify.Classes.FirstAsync(c => c.Id == _classId);
        Assert.Equal(1, finalClass.RegisteredCount);
    }

    public async Task DisposeAsync()
    {
        if (!_dbAvailable)
        {
            return;
        }

        await using var db = new StudioFlowDbContext(Options());
        await db.Registrations.Where(r => r.ClassId == _classId).ExecuteDeleteAsync();
        await db.Classes.Where(c => c.Id == _classId).ExecuteDeleteAsync();
        await db.Instructors.Where(i => i.Id == _instructorId).ExecuteDeleteAsync();
        await db.Users.Where(u => u.Id == _instructorUserId || u.Id == _memberAId || u.Id == _memberBId).ExecuteDeleteAsync();
        await db.Rooms.Where(r => r.Id == _roomId).ExecuteDeleteAsync();
    }
}

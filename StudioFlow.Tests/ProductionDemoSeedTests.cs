using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using StudioFlow.Core.DTOs.Classes;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Exceptions;
using StudioFlow.Data.Context;
using StudioFlow.Data.Repositories;
using StudioFlow.Data.Seed;
using StudioFlow.Service.Security;
using StudioFlow.Service.Services;

namespace StudioFlow.Tests;

/// <summary>
/// The production demo seed against a real, freshly created and migrated PostgreSQL
/// database (one throwaway database per test, dropped afterwards). Requires the
/// studioflow-pg container, or STUDIOFLOW_TEST_CONNECTION pointing at a server the
/// user may create databases on.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProductionDemoSeedTests : IAsyncLifetime
{
    private const string AdminEmail = "demo.admin@studioflow.example";
    private const string AdminPassword = "Adm1n-Test-Only!";
    private const string InstructorPassword = "Instr-Test-Only!";

    private static string ServerConnectionString =>
        Environment.GetEnvironmentVariable("STUDIOFLOW_TEST_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=studioflow;Username=postgres;Password=postgres";

    private readonly string _database = "studioflow_seedtest_" + Guid.NewGuid().ToString("N");
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly CapturingLogger _logger = new();
    private bool _dbAvailable;

    private string ConnectionString =>
        new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = _database }.ConnectionString;

    private StudioFlowDbContext NewContext() =>
        new(new DbContextOptionsBuilder<StudioFlowDbContext>().UseNpgsql(ConnectionString).Options);

    private static DemoSeedOptions Options() => new()
    {
        Enabled = true,
        AdminName = "Demo Admin",
        AdminEmail = AdminEmail,
        AdminPassword = AdminPassword,
        DemoUserPassword = InstructorPassword
    };

    private async Task SeedAsync(DemoSeedOptions? options = null)
    {
        await using var db = NewContext();
        await ProductionDemoSeed.SeedAsync(db, _hasher, options ?? Options(), _logger);
    }

    public async Task InitializeAsync()
    {
        try
        {
            var admin = new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = "postgres" };
            await using var conn = new NpgsqlConnection(admin.ConnectionString);
            await conn.OpenAsync();
            await using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            _dbAvailable = true;
        }
        catch
        {
            _dbAvailable = false;
            return;
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (!_dbAvailable)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        var admin = new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = "postgres" };
        await using var conn = new NpgsqlConnection(admin.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private void RequireDb() => Assert.True(_dbAvailable,
        "PostgreSQL not reachable. These tests need the studioflow-pg container running " +
        "(or STUDIOFLOW_TEST_CONNECTION pointing at a server where databases can be created).");

    // ---------- Admin -----------------------------------------------------

    [Fact]
    public async Task Creates_the_Demo_Admin_when_absent()
    {
        RequireDb();
        await SeedAsync();

        await using var db = NewContext();
        var admin = await db.Users.SingleAsync(u => u.Email == AdminEmail);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.True(admin.IsActive);
        Assert.Equal("Demo Admin", admin.Name);
        Assert.True(_hasher.Verify(AdminPassword, admin.PasswordHash));
        Assert.DoesNotContain(AdminPassword, admin.PasswordHash);
    }

    [Fact]
    public async Task Leaves_an_existing_account_with_the_admin_email_completely_unchanged()
    {
        RequireDb();
        await using (var db = NewContext())
        {
            db.Users.Add(new User
            {
                Name = "Someone Else", Email = AdminEmail, PasswordHash = "pre-existing-hash",
                Role = UserRole.Member, IsActive = true
            });
            await db.SaveChangesAsync();
        }

        await SeedAsync();

        await using var check = NewContext();
        var user = await check.Users.SingleAsync(u => u.Email == AdminEmail);
        Assert.Equal(UserRole.Member, user.Role);           // not promoted
        Assert.Equal("pre-existing-hash", user.PasswordHash); // password not changed
        Assert.Equal("Someone Else", user.Name);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Demo Admin already exists"));
    }

    // ---------- Instructor / no members -----------------------------------

    [Fact]
    public async Task Creates_exactly_one_Demo_Instructor_with_a_linked_profile_and_a_working_password()
    {
        RequireDb();
        await SeedAsync();

        await using var db = NewContext();
        var instructors = await db.Instructors.Include(i => i.User).ToListAsync();
        var instructor = Assert.Single(instructors);
        Assert.Equal(ProductionDemoSeed.DemoInstructorEmail, instructor.User.Email);
        Assert.Equal(UserRole.Instructor, instructor.User.Role);
        Assert.True(instructor.User.IsActive);
        Assert.True(_hasher.Verify(InstructorPassword, instructor.User.PasswordHash));
        Assert.Equal(1, await db.Users.CountAsync(u => u.Role == UserRole.Instructor));
    }

    [Fact]
    public async Task Creates_no_members_registrations_or_waitlist_entries()
    {
        RequireDb();
        await SeedAsync();

        await using var db = NewContext();
        Assert.Equal(2, await db.Users.CountAsync()); // Demo Admin + Demo Instructor
        Assert.Equal(0, await db.Users.CountAsync(u => u.Role == UserRole.Member));
        Assert.Equal(0, await db.Registrations.CountAsync());
        Assert.Equal(0, await db.WaitlistEntries.CountAsync());
    }

    // ---------- Rooms / tags ---------------------------------------------

    [Fact]
    public async Task Creates_the_expected_rooms_and_tags()
    {
        RequireDb();
        await SeedAsync();

        await using var db = NewContext();
        var rooms = await db.Rooms.OrderBy(r => r.Name).Select(r => new { r.Name, r.MaximumCapacity, r.IsActive }).ToListAsync();
        Assert.Equal(
            ProductionDemoSeed.Rooms.OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => (r.Name, r.MaximumCapacity)),
            rooms.OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => (r.Name, r.MaximumCapacity)));
        Assert.All(rooms, r => Assert.True(r.IsActive));

        var tags = await db.Tags.Select(t => t.Name).ToListAsync();
        Assert.Equal(ProductionDemoSeed.Tags.OrderBy(t => t), tags.OrderBy(t => t));
    }

    // ---------- Classes ---------------------------------------------------

    [Fact]
    public async Task Creates_the_expected_future_classes_owned_by_the_Demo_Instructor_with_valid_rules()
    {
        RequireDb();
        var before = DateTime.UtcNow;
        await SeedAsync();

        await using var db = NewContext();
        var demo = await db.Instructors.SingleAsync(i => i.User.Email == ProductionDemoSeed.DemoInstructorEmail);
        var classes = await db.Classes.Include(c => c.Room).Include(c => c.Tags).ToListAsync();

        Assert.Equal(ProductionDemoSeed.Classes.Select(c => c.Name).OrderBy(n => n), classes.Select(c => c.Name).OrderBy(n => n));
        Assert.All(classes, c =>
        {
            Assert.Equal(demo.Id, c.InstructorId);
            Assert.Equal(ClassStatus.Active, c.Status);
            Assert.Equal(0, c.RegisteredCount);
            Assert.True(c.StartTime > before.AddDays(1), $"{c.Name} should start at least a day after seeding");
            Assert.True(c.EndTime > c.StartTime);
            Assert.InRange(c.Capacity, 1, c.Room.MaximumCapacity);
            Assert.Equal(
                ProductionDemoSeed.Classes.Single(s => s.Name == c.Name).Tags.OrderBy(t => t),
                c.Tags.Select(t => t.Name).OrderBy(t => t));
        });

        static bool Overlap(Class a, Class b) => a.StartTime < b.EndTime && b.StartTime < a.EndTime;
        foreach (var a in classes)
        {
            foreach (var b in classes.Where(b => b.Id != a.Id))
            {
                Assert.False(a.InstructorId == b.InstructorId && Overlap(a, b), $"instructor overlap: {a.Name} / {b.Name}");
                Assert.False(a.RoomId == b.RoomId && Overlap(a, b), $"room overlap: {a.Name} / {b.Name}");
            }
        }
    }

    // ---------- Idempotency ------------------------------------------------

    [Fact]
    public async Task Running_twice_creates_no_duplicates_and_changes_nothing()
    {
        RequireDb();
        await SeedAsync();

        string Snapshot(StudioFlowDbContext db) => string.Join("|",
            db.Users.Count(), db.Instructors.Count(), db.Rooms.Count(), db.Tags.Count(), db.Classes.Count(),
            db.Classes.SelectMany(c => c.Tags).Count(), db.Registrations.Count(), db.WaitlistEntries.Count());

        string first, firstHashes, firstTimes;
        await using (var db = NewContext())
        {
            first = Snapshot(db);
            firstHashes = string.Join(",", db.Users.OrderBy(u => u.Id).Select(u => u.PasswordHash));
            firstTimes = string.Join(",", db.Classes.OrderBy(c => c.Id).Select(c => c.StartTime));
        }

        // Second startup — passwords may already be removed from configuration.
        await SeedAsync(new DemoSeedOptions { Enabled = true, AdminEmail = AdminEmail });

        await using var check = NewContext();
        Assert.Equal("2|1|4|8|10|26|0|0", first);
        Assert.Equal(first, Snapshot(check));
        Assert.Equal(firstHashes, string.Join(",", check.Users.OrderBy(u => u.Id).Select(u => u.PasswordHash)));
        Assert.Equal(firstTimes, string.Join(",", check.Classes.OrderBy(c => c.Id).Select(c => c.StartTime)));
    }

    // ---------- Failure safety / logging ---------------------------------

    [Fact]
    public async Task Missing_or_invalid_settings_fail_loudly_name_only_the_settings_and_write_nothing()
    {
        RequireDb();

        // (configuration, setting the error must name, value that must never appear in it)
        var cases = new (Action<DemoSeedOptions> Break, string Setting, string? Secret)[]
        {
            (o => o.DemoUserPassword = null, "DemoSeed:DemoUserPassword", AdminPassword),
            (o => o.AdminEmail = null, "DemoSeed:AdminEmail", null),
            (o => o.AdminEmail = "not-an-email", "DemoSeed:AdminEmail", "not-an-email"),
            (o => o.AdminName = "   ", "DemoSeed:AdminName", null),
            (o => o.AdminPassword = "Short1!", "DemoSeed:AdminPassword", "Short1!"),
            (o => o.DemoUserPassword = "Tiny9?", "DemoSeed:DemoUserPassword", "Tiny9?"),
            (o => o.AdminPassword = new string('x', 101), "DemoSeed:AdminPassword", new string('x', 101))
        };

        foreach (var (breakIt, setting, secret) in cases)
        {
            var options = Options();
            breakIt(options);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(options));

            Assert.Contains(setting, ex.Message);
            if (secret is not null)
            {
                Assert.DoesNotContain(secret, ex.Message);
            }

            await using var db = NewContext(); // validation runs before any write
            Assert.Equal(0, await db.Users.CountAsync());
            Assert.Equal(0, await db.Rooms.CountAsync());
        }
    }

    [Fact]
    public async Task Logs_contain_no_passwords_hashes_or_emails()
    {
        RequireDb();
        await SeedAsync();
        await SeedAsync(); // second run also logs the "already exists" warning

        await using var db = NewContext();
        var secrets = new List<string> { AdminPassword, InstructorPassword, AdminEmail, ProductionDemoSeed.DemoInstructorEmail };
        secrets.AddRange(await db.Users.Select(u => u.PasswordHash).ToListAsync());

        Assert.NotEmpty(_logger.Entries);
        foreach (var entry in _logger.Entries)
        {
            foreach (var secret in secrets)
            {
                Assert.DoesNotContain(secret, entry.Message);
            }
        }
    }

    // ---------- The Demo Instructor uses the real create-class flow --------

    [Fact]
    public async Task Demo_Instructor_can_create_a_class_for_themselves_but_not_for_another_instructor()
    {
        RequireDb();
        await SeedAsync();

        int demoUserId, demoInstructorId, otherInstructorId, roomId;
        await using (var db = NewContext())
        {
            var demo = await db.Instructors.Include(i => i.User).SingleAsync();
            demoUserId = demo.UserId;
            demoInstructorId = demo.Id;
            roomId = (await db.Rooms.SingleAsync(r => r.Name == "Main Studio")).Id;

            // An instructor the Admin added later through the app.
            var other = new Instructor
            {
                User = new User
                {
                    Name = "Other Coach", Email = "other.coach@studioflow.example", PasswordHash = "x",
                    Role = UserRole.Instructor, IsActive = true
                }
            };
            db.Instructors.Add(other);
            await db.SaveChangesAsync();
            otherInstructorId = other.Id;
        }

        await using var ctx = NewContext();
        var service = new ClassService(
            new ClassRepository(ctx), new InstructorRepository(ctx), new RoomRepository(ctx),
            new RegistrationRepository(ctx), new UnitOfWork(ctx), TestKit.Mapper);

        var start = DateTime.UtcNow.Date.AddDays(20).AddHours(9);
        ClassCreateDto Dto(string name, int? instructorId) => new()
        {
            Name = name, Description = "Created by the Demo Instructor", InstructorId = instructorId,
            RoomId = roomId, StartTime = start, EndTime = start.AddHours(1), Capacity = 10
        };

        var created = await service.CreateAsync(Dto("Instructor Own Class", null), demoUserId, UserRole.Instructor);
        Assert.Equal(demoInstructorId, created.InstructorId);
        Assert.Equal(ProductionDemoSeed.DemoInstructorName, created.InstructorName);

        await Assert.ThrowsAsync<ForbiddenActionException>(() =>
            service.CreateAsync(Dto("Spoofed Class", otherInstructorId), demoUserId, UserRole.Instructor));

        await using var check = NewContext();
        Assert.False(await check.Classes.AnyAsync(c => c.Name == "Spoofed Class"));
        Assert.Equal(0, await check.Classes.CountAsync(c => c.InstructorId == otherInstructorId));
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}

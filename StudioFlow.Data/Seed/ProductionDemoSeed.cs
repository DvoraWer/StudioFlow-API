using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Interfaces.Security;
using StudioFlow.Data.Context;

namespace StudioFlow.Data.Seed;

/// <summary>
/// Opt-in demo data for a fresh PRODUCTION deployment: one Demo Admin, one Demo
/// Instructor (User + Instructor profile), rooms, tags and ~10 future classes owned
/// by the Demo Instructor. No members, registrations or waitlist entries.
///
/// Separate from the Development <see cref="SeedData"/> (never calls it). Safe to run
/// on every startup: each record is matched by its natural key (User.Email,
/// Instructor.UserId, Room.Name, Tag.Name, Class.Name — the same keys SeedData uses)
/// and only inserted when missing; existing rows are never modified. Everything runs
/// in one transaction, so a failure leaves nothing half-seeded and is rethrown.
/// Nothing sensitive (passwords, hashes, emails) is logged — only counts and names
/// of rooms/classes.
/// </summary>
public static class ProductionDemoSeed
{
    public const string DemoInstructorName = "Yael Shapiro";
    public const string DemoInstructorEmail = "yael.shapiro@studioflow.example";
    private const string DemoInstructorSpecialization = "Yoga, Pilates, Strength & Indoor Cycling";
    private const string DemoInstructorBio =
        "Certified multi-discipline coach with 10 years of studio experience, from sunrise yoga to high-intensity spin.";

    public sealed record RoomSeed(string Name, int MaximumCapacity);

    public sealed record ClassSeed(
        string Name, string Description, string RoomName,
        int DaysFromSeed, int StartHourUtc, int StartMinuteUtc, int DurationMinutes,
        int Capacity, string[] Tags);

    public static readonly IReadOnlyList<RoomSeed> Rooms = new[]
    {
        new RoomSeed("Main Studio", 25),
        new RoomSeed("Mind & Body Room", 15),
        new RoomSeed("Spin Studio", 12),
        new RoomSeed("Small Studio", 6)
    };

    public static readonly IReadOnlyList<string> Tags = new[]
    {
        "Beginner", "Intermediate", "Advanced", "Cardio", "Strength", "Flexibility", "Mindfulness", "Low Impact"
    };

    /// <summary>
    /// Days are counted from the UTC date of the first seed run (D+2 … D+13), so every
    /// class starts at least a day after deployment. Times are UTC; Israel is UTC+2/+3,
    /// so these land at normal studio hours (early morning / late afternoon). Every
    /// class has its own time slot, so the single instructor and each room never overlap.
    /// </summary>
    public static readonly IReadOnlyList<ClassSeed> Classes = new[]
    {
        new ClassSeed("Sunrise Vinyasa Yoga", "A flowing vinyasa practice to wake up body and mind. All levels welcome.",
            "Mind & Body Room", 2, 5, 0, 60, 15, new[] { "Beginner", "Flexibility", "Mindfulness" }),
        new ClassSeed("Functional Strength", "Compound movements with dumbbells and bodyweight to build everyday strength.",
            "Main Studio", 2, 16, 0, 60, 20, new[] { "Intermediate", "Strength" }),
        new ClassSeed("Power Spin 45", "45 minutes of rhythm-based indoor cycling with climbs and sprints.",
            "Spin Studio", 3, 16, 0, 45, 12, new[] { "Intermediate", "Cardio" }),
        new ClassSeed("Pilates Core Essentials", "Small-group mat pilates focused on core control, posture and breathing.",
            "Small Studio", 4, 6, 0, 60, 6, new[] { "Beginner", "Strength", "Low Impact" }),
        new ClassSeed("HIIT Express", "Short, intense intervals mixing cardio and strength. Bring water.",
            "Main Studio", 5, 16, 0, 45, 20, new[] { "Advanced", "Cardio", "Strength" }),
        new ClassSeed("Gentle Stretch & Mobility", "Slow, guided stretching and joint mobility to release tension.",
            "Mind & Body Room", 6, 15, 0, 60, 12, new[] { "Beginner", "Flexibility", "Low Impact" }),
        new ClassSeed("Kettlebell Fundamentals", "Learn safe swing, goblet squat and press technique in a small group.",
            "Small Studio", 8, 6, 0, 60, 6, new[] { "Beginner", "Strength" }),
        new ClassSeed("Mat Pilates Flow", "A continuous pilates sequence for strength, length and balance.",
            "Mind & Body Room", 9, 6, 0, 60, 15, new[] { "Intermediate", "Flexibility", "Low Impact" }),
        new ClassSeed("Endurance Ride", "A steady 60-minute ride building aerobic base and leg endurance.",
            "Spin Studio", 11, 16, 0, 60, 12, new[] { "Advanced", "Cardio" }),
        new ClassSeed("Weekend Yoga & Breathwork", "A longer weekend session of yoga and guided breathing.",
            "Main Studio", 13, 7, 0, 90, 25, new[] { "Beginner", "Flexibility", "Mindfulness" })
    };

    public static async Task SeedAsync(
        StudioFlowDbContext db, IPasswordHasher passwordHasher, DemoSeedOptions options,
        ILogger logger, CancellationToken ct = default)
    {
        var adminEmail = options.AdminEmail?.Trim();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // --- Look everything up first, so invalid settings fail before any write ---
        var existingAdmin = string.IsNullOrEmpty(adminEmail)
            ? null
            : await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail, ct);
        var existingInstructorUser = await db.Users
            .Include(u => u.Instructor)
            .FirstOrDefaultAsync(u => u.Email == DemoInstructorEmail, ct);

        ThrowIfSettingsInvalid(options, adminEmail, adminMissing: existingAdmin is null,
            instructorMissing: existingInstructorUser is null);

        var created = new CreatedCounts();

        // --- Demo Admin: create-only, an existing account is never touched ---
        if (existingAdmin is null)
        {
            db.Users.Add(new User
            {
                Name = options.AdminName!.Trim(),
                Email = adminEmail!,
                PasswordHash = passwordHasher.Hash(options.AdminPassword!),
                Role = UserRole.Admin,
                IsActive = true
            });
            created.Users++;
        }
        else
        {
            logger.LogWarning("Demo seed: the configured Demo Admin already exists; the account was left unchanged.");
        }

        // --- Demo Instructor: User (Role=Instructor) + linked profile, create-only ---
        var instructor = EnsureDemoInstructor(db, passwordHasher, options, existingInstructorUser, logger, created);
        await db.SaveChangesAsync(ct);

        // --- Rooms and tags (natural key: Name) ---
        var rooms = new Dictionary<string, Room>();
        foreach (var seed in Rooms)
        {
            var room = await db.Rooms.FirstOrDefaultAsync(r => r.Name == seed.Name, ct);
            if (room is null)
            {
                room = new Room { Name = seed.Name, MaximumCapacity = seed.MaximumCapacity, IsActive = true };
                db.Rooms.Add(room);
                created.Rooms++;
            }

            rooms[seed.Name] = room;
        }

        var tags = new Dictionary<string, Tag>();
        foreach (var name in Tags)
        {
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name, ct);
            if (tag is null)
            {
                tag = new Tag { Name = name };
                db.Tags.Add(tag);
                created.Tags++;
            }

            tags[name] = tag;
        }

        await db.SaveChangesAsync(ct);

        // --- Classes (natural key: Name), all owned by the Demo Instructor ---
        if (instructor is null)
        {
            logger.LogWarning("Demo seed: no usable Demo Instructor profile, so demo classes were not created.");
        }
        else
        {
            var seedDate = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            foreach (var seed in Classes)
            {
                if (await db.Classes.AnyAsync(c => c.Name == seed.Name, ct))
                {
                    continue; // already seeded — times and tags stay as first created
                }

                var start = seedDate.AddDays(seed.DaysFromSeed).AddHours(seed.StartHourUtc).AddMinutes(seed.StartMinuteUtc);
                var end = start.AddMinutes(seed.DurationMinutes);
                var room = rooms[seed.RoomName];

                var problem = await ValidateClassAsync(db, instructor.Id, room, start, end, seed.Capacity, ct);
                if (problem is not null)
                {
                    logger.LogWarning("Demo seed: class {ClassName} was skipped: {Reason}", seed.Name, problem);
                    continue;
                }

                var cls = new Class
                {
                    Name = seed.Name,
                    Description = seed.Description,
                    InstructorId = instructor.Id,
                    RoomId = room.Id,
                    StartTime = start,
                    EndTime = end,
                    Capacity = seed.Capacity,
                    RegisteredCount = 0, // no seeded registrations
                    Status = ClassStatus.Active
                };
                foreach (var tagName in seed.Tags)
                {
                    cls.Tags.Add(tags[tagName]); // new class, so each ClassTags link is new
                    created.TagLinks++;
                }

                db.Classes.Add(cls);
                created.Classes++;

                // Saved one by one so the next class's overlap check sees this one.
                await db.SaveChangesAsync(ct);
            }
        }

        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Demo seed completed. Created {Users} user(s), {Instructors} instructor profile(s), {Rooms} room(s), " +
            "{Tags} tag(s), {Classes} class(es), {TagLinks} class-tag link(s); existing records were left unchanged.",
            created.Users, created.Instructors, created.Rooms, created.Tags, created.Classes, created.TagLinks);
    }

    private static Instructor? EnsureDemoInstructor(
        StudioFlowDbContext db, IPasswordHasher passwordHasher, DemoSeedOptions options,
        User? existingUser, ILogger logger, CreatedCounts created)
    {
        if (existingUser is null)
        {
            // Same shape as InstructorService.CreateAsync: active Instructor user + profile.
            var user = new User
            {
                Name = DemoInstructorName,
                Email = DemoInstructorEmail,
                PasswordHash = passwordHasher.Hash(options.DemoUserPassword!),
                Role = UserRole.Instructor,
                IsActive = true
            };
            var instructor = new Instructor
            {
                User = user,
                Specialization = DemoInstructorSpecialization,
                Bio = DemoInstructorBio
            };
            db.Users.Add(user);
            db.Instructors.Add(instructor);
            created.Users++;
            created.Instructors++;
            return instructor;
        }

        if (existingUser.Role != UserRole.Instructor)
        {
            logger.LogWarning("Demo seed: the Demo Instructor email belongs to a non-instructor account; it was left unchanged.");
            return null;
        }

        if (existingUser.Instructor is not null)
        {
            return existingUser.Instructor;
        }

        // Instructor user without a profile: add only the missing profile (account untouched).
        var profile = new Instructor
        {
            UserId = existingUser.Id,
            Specialization = DemoInstructorSpecialization,
            Bio = DemoInstructorBio
        };
        db.Instructors.Add(profile);
        created.Instructors++;
        return profile;
    }

    /// <summary>
    /// The ClassService create rules, applied because the seed writes through the
    /// DbContext: future start, End after Start, active room, 0 &lt; capacity ≤ room
    /// maximum, and no overlap with an Active class of the same instructor or room.
    /// Returns null when valid, otherwise a short reason (no sensitive data).
    /// </summary>
    private static async Task<string?> ValidateClassAsync(
        StudioFlowDbContext db, int instructorId, Room room, DateTime start, DateTime end, int capacity,
        CancellationToken ct)
    {
        if (start <= DateTime.UtcNow) return "its start time is not in the future";
        if (end <= start) return "EndTime must be later than StartTime";
        if (!room.IsActive) return $"room '{room.Name}' is not active";
        if (capacity <= 0 || capacity > room.MaximumCapacity)
            return $"capacity {capacity} does not fit room '{room.Name}' (max {room.MaximumCapacity})";

        if (await db.Classes.AnyAsync(c => c.InstructorId == instructorId && c.Status == ClassStatus.Active &&
                                           c.StartTime < end && start < c.EndTime, ct))
            return "the instructor already has an active class in that time window";

        if (await db.Classes.AnyAsync(c => c.RoomId == room.Id && c.Status == ClassStatus.Active &&
                                           c.StartTime < end && start < c.EndTime, ct))
            return $"room '{room.Name}' is already booked in that time window";

        return null;
    }

    // Same rules as RegisterRequestDto / LoginRequestDto ([EmailAddress], [StringLength(100, MinimumLength = 8)]),
    // so every seeded account can actually log in through POST /api/auth/login.
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 100;
    private static readonly System.ComponentModel.DataAnnotations.EmailAddressAttribute EmailRule = new();

    /// <summary>
    /// Runs before any write. AdminEmail is always required (it is the lookup key);
    /// AdminName/AdminPassword only while the admin still has to be created, and
    /// DemoUserPassword only while the Demo Instructor does. Messages name the
    /// setting and the rule — never the value.
    /// </summary>
    private static void ThrowIfSettingsInvalid(
        DemoSeedOptions options, string? adminEmail, bool adminMissing, bool instructorMissing)
    {
        var problems = new List<string>();

        if (string.IsNullOrEmpty(adminEmail))
            problems.Add("DemoSeed:AdminEmail is missing");
        else if (!EmailRule.IsValid(adminEmail))
            problems.Add("DemoSeed:AdminEmail must be a valid email address");

        if (adminMissing)
        {
            if (string.IsNullOrWhiteSpace(options.AdminName))
                problems.Add("DemoSeed:AdminName is missing");
            AddPasswordProblem(problems, "DemoSeed:AdminPassword", options.AdminPassword);
        }

        if (instructorMissing)
            AddPasswordProblem(problems, "DemoSeed:DemoUserPassword", options.DemoUserPassword);

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"DemoSeed is enabled but its configuration is invalid: {string.Join("; ", problems)}.");
        }
    }

    private static void AddPasswordProblem(List<string> problems, string setting, string? password)
    {
        if (string.IsNullOrEmpty(password))
            problems.Add($"{setting} is missing");
        else if (password.Length is < MinPasswordLength or > MaxPasswordLength)
            problems.Add($"{setting} must be {MinPasswordLength}-{MaxPasswordLength} characters long");
    }

    private sealed class CreatedCounts
    {
        public int Users, Instructors, Rooms, Tags, Classes, TagLinks;
    }
}

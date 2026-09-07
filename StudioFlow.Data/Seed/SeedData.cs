using Microsoft.EntityFrameworkCore;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Core.Interfaces.Security;
using StudioFlow.Data.Context;

namespace StudioFlow.Data.Seed;

/// <summary>
/// Deterministic, idempotent DEVELOPMENT seed data (StudioFlow spec §27).
/// Every record is matched by a natural key and inserted only when missing, so
/// running this repeatedly never creates duplicates. Development only — the
/// caller must gate it on the Development environment.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Documented DEVELOPMENT-ONLY password for every seeded user (spec §19 uses
    /// this exact value as its example; spec §45 requires demo credentials to be
    /// documented). It is only ever hashed here, never returned by the API.
    /// </summary>
    public const string DevPassword = "Password123!";

    public static async Task SeedAsync(
        StudioFlowDbContext db, IPasswordHasher passwordHasher, CancellationToken ct = default)
    {
        // --- Users (natural key: Email, which has a unique index) ---
        var admin = await GetOrAddUserAsync(db, passwordHasher, "admin@studioflow.local", "Admin User", UserRole.Admin, ct);
        var instructorUser = await GetOrAddUserAsync(db, passwordHasher, "instructor@studioflow.local", "Ilana Cohen", UserRole.Instructor, ct);
        var member1 = await GetOrAddUserAsync(db, passwordHasher, "member1@studioflow.local", "Maya Levi", UserRole.Member, ct);
        var member2 = await GetOrAddUserAsync(db, passwordHasher, "member2@studioflow.local", "Noa Bar", UserRole.Member, ct);
        var member3 = await GetOrAddUserAsync(db, passwordHasher, "member3@studioflow.local", "Dan Katz", UserRole.Member, ct);
        await db.SaveChangesAsync(ct);

        // --- Instructor profile (1:1 with the instructor User) ---
        var instructor = await db.Instructors.FirstOrDefaultAsync(i => i.UserId == instructorUser.Id, ct);
        if (instructor is null)
        {
            instructor = new Instructor
            {
                UserId = instructorUser.Id,
                Specialization = "Yoga & Mobility",
                Bio = "Certified studio instructor with 8 years of teaching experience."
            };
            db.Instructors.Add(instructor);
            await db.SaveChangesAsync(ct);
        }

        // --- Rooms (natural key: Name) ---
        var roomA = await GetOrAddRoomAsync(db, "Room A", 20, ct);
        var roomB = await GetOrAddRoomAsync(db, "Room B", 10, ct);
        var studioC = await GetOrAddRoomAsync(db, "Studio C", 5, ct);
        await db.SaveChangesAsync(ct);

        // --- Classes (natural key: Name). Times are deterministic UTC wall-clock,
        //     comfortably in the future so the demo stays valid for weeks. ---
        var yoga = await GetOrAddClassAsync(db, "Morning Yoga Flow",
            "Gentle full-body flow to start the day.",
            instructor.Id, roomA.Id, AtDay(10, 8), AtDay(10, 9), capacity: 20, ct);

        var hiit = await GetOrAddClassAsync(db, "HIIT Blast",
            "High-intensity interval training for all levels.",
            instructor.Id, roomB.Id, AtDay(12, 18), AtDay(12, 19), capacity: 10, ct);

        var spin = await GetOrAddClassAsync(db, "Evening Spin",
            "Indoor cycling with music, 45 minutes.",
            instructor.Id, roomB.Id, AtDay(14, 19), AtDay(14, 20), capacity: 10, ct);

        var pilates = await GetOrAddClassAsync(db, "Pilates Core",
            "Mat pilates focused on core strength.",
            instructor.Id, studioC.Id, AtDay(16, 17), AtDay(16, 18), capacity: 5, ct);

        // The concurrency-demo class: a single seat (spec §17, §46).
        var lastSeat = await GetOrAddClassAsync(db, "Last Seat Sprint",
            "Single-seat class used to demonstrate the last-seat race.",
            instructor.Id, studioC.Id, AtDay(8, 12), AtDay(8, 13), capacity: 1, ct);

        // A class seeded full, so the waitlist path can be demonstrated (spec §18).
        var fullClass = await GetOrAddClassAsync(db, "Full House Combat",
            "Boxing-style conditioning. Seeded full to demonstrate the waitlist.",
            instructor.Id, roomA.Id, AtDay(11, 18), AtDay(11, 19), capacity: 2, ct);
        await db.SaveChangesAsync(ct);

        // --- Registrations (natural key: MemberId + ClassId, unique index) ---
        await GetOrAddRegistrationAsync(db, member1.Id, yoga.Id, ct);
        await GetOrAddRegistrationAsync(db, member2.Id, yoga.Id, ct);
        await GetOrAddRegistrationAsync(db, member1.Id, fullClass.Id, ct);
        await GetOrAddRegistrationAsync(db, member2.Id, fullClass.Id, ct);
        await db.SaveChangesAsync(ct);

        // Keep RegisteredCount in step with the active registrations just created.
        await SyncRegisteredCountAsync(db, yoga.Id, ct);
        await SyncRegisteredCountAsync(db, fullClass.Id, ct);
        await db.SaveChangesAsync(ct);

        // --- Waitlist: member3 waiting on the full class (natural key: MemberId + ClassId) ---
        await GetOrAddWaitlistEntryAsync(db, member3.Id, fullClass.Id, position: 1, ct);
        await db.SaveChangesAsync(ct);

        // --- Tags (natural key: Name, unique index) ---
        var beginner = await GetOrAddTagAsync(db, "Beginner", ct);
        var advanced = await GetOrAddTagAsync(db, "Advanced", ct);
        var cardio = await GetOrAddTagAsync(db, "Cardio", ct);
        var flexibility = await GetOrAddTagAsync(db, "Flexibility", ct);
        var strength = await GetOrAddTagAsync(db, "Strength", ct);
        await db.SaveChangesAsync(ct);

        // --- Class <-> Tag assignments (idempotent: a join row is added only if absent) ---
        await AssignTagsAsync(db, yoga.Id, new[] { beginner, flexibility }, ct);
        await AssignTagsAsync(db, hiit.Id, new[] { advanced, cardio, strength }, ct);
        await AssignTagsAsync(db, spin.Id, new[] { cardio }, ct);
        await AssignTagsAsync(db, pilates.Id, new[] { beginner, strength, flexibility }, ct);
        await AssignTagsAsync(db, lastSeat.Id, new[] { advanced, cardio }, ct);
        await AssignTagsAsync(db, fullClass.Id, new[] { advanced, strength }, ct);
        await db.SaveChangesAsync(ct);
    }

    private static DateTime AtDay(int daysFromToday, int hourUtc) =>
        DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(daysFromToday).AddHours(hourUtc), DateTimeKind.Utc);

    private static async Task<User> GetOrAddUserAsync(
        StudioFlowDbContext db, IPasswordHasher passwordHasher,
        string email, string name, UserRole role, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            user = new User
            {
                Email = email,
                Name = name,
                Role = role,
                IsActive = true,
                PasswordHash = passwordHasher.Hash(DevPassword)
            };
            db.Users.Add(user);
        }
        else if (!passwordHasher.Verify(DevPassword, user.PasswordHash))
        {
            // Upgrade rows seeded before password hashing existed (idempotent:
            // once the hash verifies, this branch never runs again).
            user.PasswordHash = passwordHasher.Hash(DevPassword);
        }

        return user;
    }

    private static async Task<Room> GetOrAddRoomAsync(
        StudioFlowDbContext db, string name, int maximumCapacity, CancellationToken ct)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Name == name, ct);
        if (room is null)
        {
            room = new Room { Name = name, MaximumCapacity = maximumCapacity, IsActive = true };
            db.Rooms.Add(room);
        }

        return room;
    }

    private static async Task<Class> GetOrAddClassAsync(
        StudioFlowDbContext db, string name, string description, int instructorId, int roomId,
        DateTime startTime, DateTime endTime, int capacity, CancellationToken ct)
    {
        var cls = await db.Classes.FirstOrDefaultAsync(c => c.Name == name, ct);
        if (cls is null)
        {
            cls = new Class
            {
                Name = name,
                Description = description,
                InstructorId = instructorId,
                RoomId = roomId,
                StartTime = startTime,
                EndTime = endTime,
                Capacity = capacity,
                RegisteredCount = 0,
                Status = ClassStatus.Active
            };
            db.Classes.Add(cls);
        }

        return cls;
    }

    private static async Task GetOrAddRegistrationAsync(
        StudioFlowDbContext db, int memberId, int classId, CancellationToken ct)
    {
        var exists = await db.Registrations.AnyAsync(r => r.MemberId == memberId && r.ClassId == classId, ct);
        if (!exists)
        {
            db.Registrations.Add(new Registration
            {
                MemberId = memberId,
                ClassId = classId,
                RegisteredAt = DateTime.UtcNow,
                Status = RegistrationStatus.Active
            });
        }
    }

    private static async Task GetOrAddWaitlistEntryAsync(
        StudioFlowDbContext db, int memberId, int classId, int position, CancellationToken ct)
    {
        var exists = await db.WaitlistEntries.AnyAsync(w => w.MemberId == memberId && w.ClassId == classId, ct);
        if (!exists)
        {
            db.WaitlistEntries.Add(new WaitlistEntry
            {
                MemberId = memberId,
                ClassId = classId,
                Position = position,
                JoinedAt = DateTime.UtcNow,
                Status = WaitlistStatus.Waiting
            });
        }
    }

    private static async Task<Tag> GetOrAddTagAsync(StudioFlowDbContext db, string name, CancellationToken ct)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name, ct);
        if (tag is null)
        {
            tag = new Tag { Name = name };
            db.Tags.Add(tag);
        }

        return tag;
    }

    private static async Task AssignTagsAsync(
        StudioFlowDbContext db, int classId, IReadOnlyCollection<Tag> tags, CancellationToken ct)
    {
        var cls = await db.Classes
            .Include(c => c.Tags)
            .FirstOrDefaultAsync(c => c.Id == classId, ct);

        if (cls is null)
        {
            return;
        }

        foreach (var tag in tags)
        {
            if (cls.Tags.All(t => t.Id != tag.Id))
            {
                cls.Tags.Add(tag);
            }
        }
    }

    private static async Task SyncRegisteredCountAsync(StudioFlowDbContext db, int classId, CancellationToken ct)
    {
        var cls = await db.Classes.FirstOrDefaultAsync(c => c.Id == classId, ct);
        if (cls is null)
        {
            return;
        }

        var activeCount = await db.Registrations
            .CountAsync(r => r.ClassId == classId && r.Status == RegistrationStatus.Active, ct);

        if (cls.RegisteredCount != activeCount)
        {
            cls.RegisteredCount = activeCount;
        }
    }
}

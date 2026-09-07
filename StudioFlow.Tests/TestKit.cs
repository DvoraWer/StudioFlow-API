using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using StudioFlow.Core.Entities;
using StudioFlow.Core.Enums;
using StudioFlow.Service.Mapping;

namespace StudioFlow.Tests;

/// <summary>Shared bits for the service unit tests.</summary>
internal static class TestKit
{
    /// <summary>The real AutoMapper configuration — so mapping is exercised too.</summary>
    public static readonly IMapper Mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

    public static User Member(int id = 1, bool active = true) => new()
    {
        Id = id,
        Name = $"Member {id}",
        Email = $"member{id}@studioflow.local",
        PasswordHash = "hash",
        Role = UserRole.Member,
        IsActive = active
    };

    public static User Admin(int id = 90) => new()
    {
        Id = id, Name = "Admin", Email = "admin@studioflow.local",
        PasswordHash = "hash", Role = UserRole.Admin, IsActive = true
    };

    public static Room Room(int id = 1, int max = 20, bool active = true) => new()
    {
        Id = id, Name = $"Room {id}", MaximumCapacity = max, IsActive = active
    };

    public static Instructor Instructor(int id = 1, int userId = 5) => new()
    {
        Id = id,
        UserId = userId,
        Specialization = "Yoga",
        User = new User { Id = userId, Name = "Ilana Cohen", Email = "ilana@studioflow.local", PasswordHash = "hash", Role = UserRole.Instructor, IsActive = true }
    };

    public static Class ActiveClass(
        int id = 1, int capacity = 10, int registered = 0, int instructorId = 1, int roomId = 1,
        ClassStatus status = ClassStatus.Active, DateTime? start = null)
    {
        var s = start ?? DateTime.UtcNow.AddDays(7);
        return new Class
        {
            Id = id,
            Name = $"Class {id}",
            Description = "desc",
            InstructorId = instructorId,
            RoomId = roomId,
            StartTime = s,
            EndTime = s.AddHours(1),
            Capacity = capacity,
            RegisteredCount = registered,
            Status = status,
            Instructor = Instructor(instructorId),
            Room = Room(roomId)
        };
    }

    public static Registration ActiveRegistration(int memberId, int classId, Class? cls = null) => new()
    {
        Id = memberId * 1000 + classId,
        MemberId = memberId,
        ClassId = classId,
        RegisteredAt = DateTime.UtcNow,
        Status = RegistrationStatus.Active,
        Class = cls ?? ActiveClass(classId),
        Member = Member(memberId)
    };
}

using AutoMapper;
using StudioFlow.Core.DTOs.Auth;
using StudioFlow.Core.DTOs.Classes;
using StudioFlow.Core.DTOs.Instructors;
using StudioFlow.Core.DTOs.Participants;
using StudioFlow.Core.DTOs.Registrations;
using StudioFlow.Core.DTOs.Rooms;
using StudioFlow.Core.DTOs.Users;
using StudioFlow.Core.DTOs.Waitlist;
using StudioFlow.Core.Entities;

namespace StudioFlow.Service.Mapping;

/// <summary>
/// Entity &lt;-&gt; DTO maps (spec §24). Written explicitly rather than with
/// ReverseMap(): every request-&gt;entity map ignores server-controlled members
/// (Id, RegisteredCount, Status, the xmin concurrency token, timestamps, Role and
/// navigation graphs) so client input can never be projected onto them (spec §9,
/// §36, §43). Enum values are surfaced to clients as their names.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ----------------------------------------------------------------
        // Entity -> response DTO
        // ----------------------------------------------------------------

        CreateMap<User, UserResponseDto>()
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()));

        CreateMap<User, AuthResponseDto>()
            .ForMember(d => d.UserId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()))
            .ForMember(d => d.Token, o => o.Ignore()); // signed and set by the auth service

        CreateMap<Room, RoomResponseDto>();

        CreateMap<Instructor, InstructorResponseDto>()
            .ForMember(d => d.Name, o => o.MapFrom(s => s.User.Name))
            .ForMember(d => d.Email, o => o.MapFrom(s => s.User.Email));

        CreateMap<Tag, TagDto>();

        CreateMap<Class, ClassResponseDto>()
            .ForMember(d => d.InstructorName, o => o.MapFrom(s => s.Instructor.User.Name))
            .ForMember(d => d.RoomName, o => o.MapFrom(s => s.Room.Name))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Tags, o => o.MapFrom(s => s.Tags));
        // AvailableSeats / IsFull are computed getters on the DTO (spec §8) — no setter, nothing to map.

        CreateMap<Class, ClassListItemDto>()
            .ForMember(d => d.InstructorName, o => o.MapFrom(s => s.Instructor.User.Name))
            .ForMember(d => d.RoomName, o => o.MapFrom(s => s.Room.Name))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        CreateMap<Registration, RegistrationResponseDto>()
            .ForMember(d => d.ClassName, o => o.MapFrom(s => s.Class.Name))
            .ForMember(d => d.StartTime, o => o.MapFrom(s => s.Class.StartTime))
            .ForMember(d => d.EndTime, o => o.MapFrom(s => s.Class.EndTime))
            .ForMember(d => d.RoomName, o => o.MapFrom(s => s.Class.Room.Name))
            .ForMember(d => d.InstructorName, o => o.MapFrom(s => s.Class.Instructor.User.Name))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        CreateMap<Registration, ParticipantDto>()
            .ForMember(d => d.MemberId, o => o.MapFrom(s => s.MemberId))
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Member.Name))
            .ForMember(d => d.Email, o => o.MapFrom(s => s.Member.Email))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        CreateMap<WaitlistEntry, WaitlistResponseDto>()
            .ForMember(d => d.ClassName, o => o.MapFrom(s => s.Class.Name))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // ----------------------------------------------------------------
        // Request DTO -> entity (server-controlled members explicitly ignored)
        // ----------------------------------------------------------------

        CreateMap<RegisterRequestDto, User>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.PasswordHash, o => o.Ignore())   // service hashes the plaintext password
            .ForMember(d => d.Role, o => o.Ignore())           // always Member — set by the service
            .ForMember(d => d.IsActive, o => o.Ignore())       // set by the service
            .ForMember(d => d.Instructor, o => o.Ignore())
            .ForMember(d => d.Registrations, o => o.Ignore())
            .ForMember(d => d.WaitlistEntries, o => o.Ignore());
        // RegisterRequestDto.Password has no destination member — never mapped onto the entity.

        CreateMap<ClassCreateDto, Class>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.RegisteredCount, o => o.Ignore())
            .ForMember(d => d.Status, o => o.Ignore())
            .ForMember(d => d.Version, o => o.Ignore())        // xmin concurrency token (spec §9)
            .ForMember(d => d.InstructorId, o => o.Ignore())   // resolved by ClassService from the caller's role
            .ForMember(d => d.Instructor, o => o.Ignore())
            .ForMember(d => d.Room, o => o.Ignore())
            .ForMember(d => d.Registrations, o => o.Ignore())
            .ForMember(d => d.WaitlistEntries, o => o.Ignore())
            .ForMember(d => d.Tags, o => o.Ignore());          // tag assignment is not part of the class contract

        CreateMap<ClassUpdateDto, Class>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.RegisteredCount, o => o.Ignore())
            .ForMember(d => d.Status, o => o.Ignore())
            .ForMember(d => d.Version, o => o.Ignore())
            .ForMember(d => d.Instructor, o => o.Ignore())
            .ForMember(d => d.Room, o => o.Ignore())
            .ForMember(d => d.Registrations, o => o.Ignore())
            .ForMember(d => d.WaitlistEntries, o => o.Ignore())
            .ForMember(d => d.Tags, o => o.Ignore());

        CreateMap<RoomCreateDto, Room>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.Classes, o => o.Ignore());

        CreateMap<RoomUpdateDto, Room>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.Classes, o => o.Ignore());

        // InstructorCreateDto carries Name/Email/Password for the new User account
        // (handled by the service); only Specialization/Bio project onto Instructor here.
        CreateMap<InstructorCreateDto, Instructor>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.UserId, o => o.Ignore())         // set by the service once the User exists
            .ForMember(d => d.User, o => o.Ignore())
            .ForMember(d => d.Classes, o => o.Ignore());

        CreateMap<InstructorUpdateDto, Instructor>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.UserId, o => o.Ignore())         // linked account is not reassigned here
            .ForMember(d => d.User, o => o.Ignore())
            .ForMember(d => d.Classes, o => o.Ignore());
    }
}

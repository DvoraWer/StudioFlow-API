# StudioFlow — Project Specification

> This is the authoritative implementation specification for the StudioFlow backend
> (and its minimal React client). Keep it in sync if the course requirements change.

---

## 1. Project Overview

StudioFlow is a sports studio management system built with:

- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- React
- JWT Authentication
- AutoMapper
- xUnit + Moq
- NLog

The backend is the primary graded component. The React client exists to demonstrate and consume the API.

The most important technical feature is optimistic concurrency around limited class capacity.

### Core scenario

Each class has a limited number of seats.

Example:

```
Class capacity = 1
RegisteredCount = 0
```

Two different members attempt to register at almost exactly the same time.

The system must guarantee:

- Only one registration succeeds.
- The other request receives HTTP 409 Conflict.
- The losing request must not create a registration.
- The conflict must be caused by EF Core optimistic concurrency using a concurrency token (see §9).
- The React client must display a clear message to the user.

Example message:

```
The last available seat was taken by another user.
```

This scenario must be demonstrable and tested.

---

## 2. General Implementation Rules

The implementation must follow these principles:

1. Keep the architecture clean and layered.
2. Do not put business logic inside controllers.
3. Do not inject DbContext directly into controllers.
4. Do not expose EF Core entities directly through the API.
5. Use DTOs for requests and responses.
6. Use Dependency Injection.
7. Use async methods throughout the application.
8. Pass CancellationToken from Controller → Service → Repository → EF Core.
9. Use EF Core Code First.
10. Use server-side pagination.
11. Use AsNoTracking() for read-only queries.
12. Use Include / ThenInclude when relationships are required.
13. Avoid N+1 queries.
14. Use JWT authentication.
15. Enforce authorization on the server.
16. Use global exception handling middleware.
17. Use CorrelationId middleware.
18. Use NLog.
19. Write unit tests for the Service layer using Moq.
20. Implement and demonstrate optimistic concurrency.

Do not over-engineer the system.

Do not introduce unnecessary technologies such as:

- Docker
- Cloud services
- SignalR
- Redis
- S3
- Google login
- External authentication
- Microservices

unless explicitly requested later.

---

## 3. Solution Structure

Create one Visual Studio solution:

```
StudioFlow.sln
```

with exactly four projects:

```
StudioFlow.Core
StudioFlow.Data
StudioFlow.Service
StudioFlow.API
```

### Dependency structure

```
StudioFlow.Core
       ↑
       |
StudioFlow.Data

StudioFlow.Core
       ↑
       |
StudioFlow.Service

StudioFlow.Core
       ↑
       |
StudioFlow.API
       |
       ↓
StudioFlow.Service
```

The API may reference Data only for dependency registration in Program.cs.

### Core

Contains:

- Entities
- Enums
- DTOs
- Interfaces

Core should not depend on the other application projects.

### Data

Contains:

- StudioFlowDbContext
- EF Core configurations
- Repositories
- Migrations
- Seed data

Depends on Core.

### Service

Contains:

- Business logic
- Services
- Business validation
- Mapping configuration

Depends on Core.

### API

Contains:

- Controllers
- Middleware
- Authentication
- Authorization
- Dependency injection configuration
- Program.cs

---

## 4. Domain Model

The system contains six main entities.

```
User
Instructor
Room
Class
Registration
WaitlistEntry
```

There is intentionally no separate Member entity.
A member is simply a User whose role is Member.

---

## 5. User Entity

**User**

Fields:

```
Id
Name
Email
PasswordHash
Role
IsActive
```

### Rules

- Email must be unique.
- Password must never be stored as plaintext.
- Role must be one of: `Admin`, `Instructor`, `Member`.
- Public registration always creates a Member.
- Users must not be allowed to register themselves as Admin or Instructor.

---

## 6. Instructor Entity

**Instructor**

Fields:

```
Id
UserId
Specialization
Bio
```

Specialization and Bio may be optional.

Relationship:

```
User 1 ---- 1 Instructor
```

An instructor is associated with one User account.

---

## 7. Room Entity

**Room**

Fields:

```
Id
Name
MaximumCapacity
IsActive
```

Example:

```
Room A
MaximumCapacity = 20
```

A room may contain many classes at different times.
However, two active classes must never occupy the same room at overlapping times.

---

## 8. Class Entity

The main entity for the limited-resource scenario.

**Class**

Fields:

```
Id
Name
Description
InstructorId
RoomId
StartTime
EndTime
Capacity
RegisteredCount
Status
RowVersion
```

Status:

```
Active
Cancelled
```

### Important

Do NOT store `IsFull`, `IsEmpty`, `AvailableSeats` as database fields. They should be calculated.

For example:

```
AvailableSeats = Capacity - RegisteredCount
IsFull = RegisteredCount >= Capacity
```

---

## 9. Concurrency token

> **Deviation from the original course text (2026-09-06):** the project uses
> **PostgreSQL**, not SQL Server. SQL Server's `rowversion` / `[Timestamp] byte[]`
> does not exist in PostgreSQL, so the concurrency token below replaces it.
> Everywhere this document later says "RowVersion", read it as "the Class
> concurrency token".

Class must contain a concurrency token.

For PostgreSQL (Npgsql), map a `uint` property read-only to the system `xmin`
column and mark it as the concurrency token:

```csharp
// Class entity
public uint Version { get; set; }

// Fluent API (ClassConfiguration)
builder.Property(c => c.Version)
    .HasColumnName("xmin")
    .HasColumnType("xid")
    .ValueGeneratedOnAddOrUpdate()
    .IsConcurrencyToken();
```

PostgreSQL bumps `xmin` on every `UPDATE` of the row, so a stale write raises
`DbUpdateConcurrencyException`. No dedicated column is added to the table.

This token is critical. It is what prevents two simultaneous requests from successfully taking the same last seat.

---

## 10. Registration Entity

**Registration**

Fields:

```
Id
MemberId
ClassId
RegisteredAt
Status
```

Status:

```
Active
Cancelled
```

Relationships:

```
User 1 ---- N Registration
Class 1 ---- N Registration
```

### Database constraint

Create a unique index/constraint on `MemberId + ClassId`.
This prevents duplicate registrations.

---

## 11. WaitlistEntry Entity

**WaitlistEntry**

Fields:

```
Id
MemberId
ClassId
JoinedAt
Position
Status
```

Status:

```
Waiting
Promoted
Cancelled
```

Relationships:

```
User 1 ---- N WaitlistEntry
Class 1 ---- N WaitlistEntry
```

---

## 12. Relationship Summary

```
User
 ├── 1:1 Instructor
 ├── 1:N Registration
 └── 1:N WaitlistEntry

Instructor
 └── 1:N Class

Room
 └── 1:N Class

Class
 ├── 1:N Registration
 └── 1:N WaitlistEntry
```

Conceptually: `User N:M Class` through `Registration` and `WaitlistEntry`.

---

## 13. Roles

There are three roles.

### Admin

Admin has full system management permissions. Admin can:

- Manage users.
- Manage instructors.
- Manage rooms.
- Create classes.
- Edit classes.
- Cancel classes.
- Change instructor.
- Change room.
- Change date/time.
- Change capacity.
- View participants.

### Instructor

Instructor can:

- View their own classes.
- View participants in their classes.
- View registration counts.
- Edit limited content fields such as class name, description.

Instructor cannot change:

- Date
- Time
- Room
- Capacity
- Instructor assignment

These operational changes belong to Admin.

### Member

Member can:

- Register.
- Login.
- Browse classes.
- Search classes.
- Filter classes.
- View class details.
- Register for a class.
- Cancel their own registration.
- View their own registrations.
- Join a waiting list.
- Leave a waiting list.

---

## 14. Business Rules

The following rules must be implemented on the server.

### Class rules

1. Capacity must be greater than zero.
2. Capacity cannot exceed room maximum capacity.
3. RegisteredCount cannot exceed Capacity.
4. EndTime must be later than StartTime.
5. Cancelled classes cannot receive registrations.
6. Cancelled classes cannot receive waitlist entries.
7. A class cannot be deleted if doing so would destroy required history; cancellation should normally be used instead.
8. Capacity cannot be reduced below the current number of active registrations.

### Registration rules

A member cannot register when:

- The class does not exist.
- The class is cancelled.
- The class has already started.
- The member is already registered.
- The class has no available seats.

If there is a seat, registration should succeed.
If another user takes the last seat during the operation, optimistic concurrency must cause the second request to fail with 409 Conflict.

---

## 15. Instructor and Room Scheduling Rules

An instructor cannot teach two active classes at overlapping times.
A room cannot contain two active classes at overlapping times.

Example:

```
10:00 - 11:00
and
10:30 - 11:30
```

overlap and are invalid for the same instructor/room.

But:

```
10:00 - 11:00
and
11:00 - 12:00
```

do not overlap.

Whenever an Admin changes instructor, room, start time, or end time, the service must revalidate the scheduling rules.

---

## 16. Registration Algorithm

Registration must be implemented in the Service layer.

Conceptually:

```
Register(memberId, classId)
        |
        v
Load Class + RowVersion
        |
        v
Does class exist?
        |
        v
Is class active?
        |
        v
Has class started?
        |
        v
Is member already registered?
        |
        v
Is there an available seat?
        |
        v
Increase RegisteredCount
        |
        v
Create Registration
        |
        v
SaveChangesAsync()
```

The update and registration must be saved as one logical operation.

---

## 17. Optimistic Concurrency Scenario

This is the most important part of the project.

Suppose:

```
Capacity = 1
RegisteredCount = 0
```

Two users, Member A and Member B, attempt to register.

Both initially read:

```
RegisteredCount = 0
RowVersion = X
```

Both think the seat is available.

Member A saves first. Database becomes:

```
RegisteredCount = 1
RowVersion = Y
```

Member B still has `RowVersion = X`.

When Member B attempts to save, EF Core must detect that the row has changed. It throws `DbUpdateConcurrencyException`.

The application must catch this and convert it to HTTP 409 Conflict.

Example response:

```json
{
  "code": "CONCURRENCY_CONFLICT",
  "message": "The last available seat was taken by another user.",
  "correlationId": "..."
}
```

- Do NOT silently retry the operation.
- Do NOT give the second user the seat.

---

## 18. Waitlist Logic

A member may join the waitlist only if the class is full.

Example:

```
Capacity = 2
RegisteredCount = 2
```

Member C tries to register. Registration is rejected because there are no seats.
Member C may instead join the waitlist.

Example:

```
Position 1 → Member C
Position 2 → Member D
Position 3 → Member E
```

When a registered member cancels:

1. A seat becomes available.
2. Find the first Waiting entry.
3. Promote that member.
4. Create their Registration.
5. Mark the WaitlistEntry as Promoted.
6. Update RegisteredCount.

The promotion should be handled atomically where practical.

---

## 19. Authentication

### Register

Endpoint: `POST /api/auth/register`

Request:

```json
{
  "name": "John Smith",
  "email": "john@example.com",
  "password": "Password123!"
}
```

The new user always receives `Role = Member`.

### Login

Endpoint: `POST /api/auth/login`

The service:

1. Finds the user.
2. Validates password.
3. Checks IsActive.
4. Creates JWT.
5. Adds claims.

At minimum: `UserId`, `Role`.

JWT must have appropriate expiration, signing key, and issuer/audience validation where configured.

---

## 20. Authorization

Use `[Authorize]` and role-based authorization.

Examples:

```csharp
[Authorize(Roles = "Admin")]
[Authorize(Roles = "Admin,Instructor")]
```

Authorization must be enforced by the API.
The React application must never be considered the security boundary.

---

## 21. API Endpoints

### Authentication

```
POST /api/auth/register
POST /api/auth/login
```

### Classes

```
GET  /api/classes
GET  /api/classes/{id}
POST /api/classes
PUT  /api/classes/{id}
POST /api/classes/{id}/cancel
```

Permissions:

```
GET    → Public/Authenticated
POST   → Admin
PUT    → Admin
cancel → Admin
```

### Registrations

```
POST   /api/classes/{id}/register
DELETE /api/classes/{id}/register
GET    /api/me/registrations
```

Member only.

### Waitlist

```
POST   /api/classes/{id}/waitlist
DELETE /api/classes/{id}/waitlist
```

Member only.

### Participants

```
GET /api/classes/{id}/participants
```

Allowed: Admin, Instructor of that class.

An instructor must not be able to see arbitrary classes belonging to another instructor unless explicitly authorized.

### Rooms

Admin CRUD:

```
GET    /api/rooms
GET    /api/rooms/{id}
POST   /api/rooms
PUT    /api/rooms/{id}
DELETE /api/rooms/{id}
```

### Instructors

Admin CRUD:

```
GET    /api/instructors
GET    /api/instructors/{id}
POST   /api/instructors
PUT    /api/instructors/{id}
DELETE /api/instructors/{id}
```

---

## 22. Classes Pagination

The classes endpoint must support server-side pagination.

Example:

```
GET /api/classes?page=1&pageSize=10
```

Possible filters: `search`, `instructorId`, `roomId`, `status`, `date`.

Example:

```
GET /api/classes?page=1&pageSize=10&search=yoga&status=Active
```

The query must be executed approximately as:

```
Where → OrderBy → Skip → Take → ToListAsync
```

Do NOT do:

```csharp
var allClasses = await query.ToListAsync();
var page = allClasses.Skip(...).Take(...);
```

Pagination must happen in the database.

---

## 23. DTOs

Suggested request DTOs:

```
RegisterRequestDto
LoginRequestDto
ClassCreateDto
ClassUpdateDto
RoomCreateDto
RoomUpdateDto
InstructorCreateDto
InstructorUpdateDto
```

Suggested response DTOs:

```
AuthResponseDto
ClassResponseDto
ClassListItemDto
RegistrationResponseDto
WaitlistResponseDto
ParticipantDto
RoomResponseDto
InstructorResponseDto
UserResponseDto
```

Never return `PasswordHash` or internal EF implementation details.

---

## 24. AutoMapper

Create AutoMapper Profiles.

Example structure:

```
Service/
    Mapping/
        MappingProfile.cs
```

Mappings should include Entity → Response DTO and Request DTO → Entity.
Avoid repeating mapping logic throughout controllers.

---

## 25. EF Core Configuration

Use Fluent API. Configure:

- Primary keys
- Foreign keys
- Required properties
- Maximum lengths
- Relationships
- Unique indexes
- RowVersion
- Delete behavior
- Seed data

Important unique constraint: `Registration(MemberId, ClassId)`.

---

## 26. Migrations

Use EF Core Code First migrations. At least two migrations should exist.

Example commands:

```
dotnet ef migrations add InitialCreate -p StudioFlow.Data -s StudioFlow.API
dotnet ef database update -p StudioFlow.Data -s StudioFlow.API
```

The actual commands may be adjusted to the final project structure.

---

## 27. Seed Data

Seed enough data to demonstrate the application.

- Users: 1 Admin, 1 Instructor, 2+ Members
- Rooms: at least Room A – capacity 20, Room B – capacity 10
- Classes: several future classes; at least one with `Capacity = 1` to make the concurrency demonstration easy.

---

## 28. Repository Interfaces

Create:

```
IUserRepository
IInstructorRepository
IRoomRepository
IClassRepository
IRegistrationRepository
IWaitlistRepository
```

Repositories should handle data access. Business decisions belong in Services.

---

## 29. Services

Create:

```
AuthService
ClassService
RegistrationService
WaitlistService
RoomService
InstructorService
UserService
```

Each service receives its dependencies through constructor injection. Example:

```csharp
public RegistrationService(
    IClassRepository classRepository,
    IRegistrationRepository registrationRepository,
    IWaitlistRepository waitlistRepository)
{
}
```

Do not instantiate repositories manually.

---

## 30. Async Requirements

All database operations must be asynchronous. Use:

```csharp
await repository.GetAsync(..., cancellationToken);
await dbContext.SaveChangesAsync(cancellationToken);
```

Do not use `.Result` or `.Wait()`.

Pass CancellationToken through: Controller → Service → Repository → EF Core.

---

## 31. Middleware

Implement global exception handling. Unexpected exceptions should produce a consistent JSON response.

Do not expose stack traces, database internals, or sensitive information in production responses.

---

## 32. CorrelationId

Every request should have a CorrelationId. If the request contains one, reuse it. Otherwise generate one.
Return it in the response headers and/or error response.

Example: `X-Correlation-Id: abc123`

This ID should also be useful in NLog entries.

---

## 33. Middleware Order

The pipeline should conceptually ensure:

```
CorrelationId
      ↓
Exception Handling
      ↓
Routing
      ↓
Authentication
      ↓
Authorization
      ↓
Controllers
```

Use the appropriate ASP.NET Core ordering for the framework version.

---

## 34. NLog

Install `NLog.Web.AspNetCore`. Create `nlog.config`.

Logging levels:

- **Information** — Normal meaningful operations.
- **Warning** — Resource conflicts and concurrency conflicts.
- **Error** — Unexpected exceptions.
- **Debug** — Detailed development diagnostics.

Never log: Passwords, JWT tokens, Sensitive request bodies.

Application code should use `ILogger<T>` rather than `Console.WriteLine()`.

---

## 35. Error Handling

Use consistent HTTP status codes.

- **400 Bad Request** — Invalid request/model validation.
- **401 Unauthorized** — Missing or invalid JWT.
- **403 Forbidden** — Authenticated but insufficient permissions.
- **404 Not Found** — Requested entity does not exist.
- **409 Conflict** — Business conflict or concurrency conflict (class is full, last seat was taken, duplicate registration, schedule conflict, capacity cannot be reduced).
- **500 Internal Server Error** — Unexpected server error.

---

## 36. Validation

Use model validation for basic input validation: Required, Email format, Password requirements, Capacity > 0, StartTime < EndTime.

Use Service layer validation for business rules: Capacity <= Room.MaximumCapacity, No instructor overlap, No room overlap, No duplicate registration, Class is active, Class has not started.

---

## 37. Unit Testing

Use xUnit and Moq. Focus unit tests on the Service layer. Repositories should be mocked.

Tests should include at least:

**Registration**

- Registration succeeds when a seat exists.
- Registration fails when class is full.
- Duplicate registration is rejected.
- Cancelled class registration is rejected.
- Registration after class start is rejected.

**Capacity**

- Capacity cannot be reduced below active registrations.

**Authorization/business rules**

- Instructor cannot modify restricted operational fields.

**Waitlist**

- Waitlist works when class is full.
- Waitlist fails when class is not full.
- Duplicate waitlist entry is rejected.
- First waiting member is promoted after cancellation.

---

## 38. Mandatory Concurrency Test

Create a separate concurrency proof. Use two different DbContext instances.

Conceptually:

```
Context A → reads Class
Context B → reads same Class
```

Both have `RowVersion = X`.

Then:

```
Context A → modifies → SaveChanges → SUCCESS
Context B → modifies → SaveChanges → must throw DbUpdateConcurrencyException
```

Verify that the second operation does not succeed.

This test is important because it proves that the system is actually using optimistic concurrency rather than merely checking availability before saving.

---

## 39. React Application

Create a simple React client.

### Screen 1 — Login/Register

- Login
- Registration
- Store JWT
- Display authentication errors

### Screen 2 — Classes

- List classes
- Search
- Filters
- Pagination (from the server)
- Show: name, instructor, room, time, available seats, status

### Screen 3 — Class Details

Show: Class name, Description, Instructor, Room, Start time, End time, Capacity, Registered count, Available seats.

Buttons (depending on the user's current state): Register, Join Waitlist, Cancel Registration.

### Screen 4 — My Registrations

Show the current member's registrations. Allow cancellation.

---

## 40. React Authentication

After login, JWT should be stored appropriately for this educational/local project.

Authenticated requests must contain: `Authorization: Bearer <token>`

The frontend may hide controls based on role, but the backend must always enforce permissions.

---

## 41. React 409 Handling

This is mandatory.

When `POST /api/classes/{id}/register` returns 409 Conflict, React should show something like:

```
The last available seat was taken by another user. Please refresh the class.
```

Do not display only "Something went wrong."

---

## 42. Recommended API Client Structure

```
src/
    api/
        authApi
        classesApi
        registrationApi
        waitlistApi
    components/
    pages/
    context/
    hooks/
```

A central HTTP helper/interceptor may be used for JWT, common errors, 401 handling.
Keep the frontend simple.

---

## 43. Security Requirements

Never:

- Store plaintext passwords.
- Allow public users to select Admin.
- Return PasswordHash.
- Log passwords.
- Log JWT tokens.
- Trust the frontend for authorization.
- Put real secrets in Git.

Use configuration/User Secrets for: Connection String, JWT Secret.

---

## 44. Configuration

Recommended:

```
appsettings.json
appsettings.Development.json
User Secrets
```

Development secrets should not be committed. For example:

```
ConnectionStrings:DefaultConnection
Jwt:Secret
Jwt:Issuer
Jwt:Audience
```

---

## 45. README

The repository README must explain:

- **System** — What StudioFlow does.
- **Architecture** — Core, Data, Service, API.
- **Requirements** — What must be installed.
- **Database** — How to configure PostgreSQL.
- **Secrets** — How to configure User Secrets.
- **Migrations** — How to run migrations.
- **API** — How to run the API.
- **React** — How to run the frontend.
- **Demo Users** — Explain available roles.
- **Concurrency** — Capacity = 1, two users, one successful registration, one 409 Conflict.
- **Tests** — How to run `dotnet test`.

---

## 46. Demo Scenario for Grading

The final system should make this demonstration easy.

1. Start PostgreSQL.
2. Run migrations.
3. Start API.
4. Start React.
5. Login as two different Members.
6. Open a class with `Capacity = 1`, `RegisteredCount = 0`.
7. Both users attempt to register nearly simultaneously.
   Expected: Member A → 200/201 success; Member B → 409 Conflict.
8. Show that only one Registration exists.
9. Show the concurrency warning in logs.
10. Cancel the registration.
11. If another member is waiting, demonstrate waitlist promotion.

---

## 47. Definition of Done

The implementation is complete only when all of the following are true:

- Solution builds successfully.
- Four required projects exist.
- Dependency direction is correct.
- EF Core Code First works.
- Database migrations work.
- At least two migrations exist.
- Seed data works.
- Relationships are configured.
- Unique registration constraint exists.
- DTOs are used.
- AutoMapper is used.
- Pagination is performed server-side.
- AsNoTracking() is used for appropriate reads.
- Include / ThenInclude are used where appropriate.
- Async is used end-to-end.
- CancellationToken is passed through.
- JWT authentication works.
- Roles work.
- Authorization is enforced server-side.
- Global exception middleware works.
- CorrelationId works.
- NLog works.
- Passwords/tokens are never logged.
- Service unit tests exist.
- Moq is used.
- Concurrency proof exists.
- DbUpdateConcurrencyException is handled.
- Last-seat race produces one success and one 409.
- React login works.
- React class pagination works.
- React registration works.
- React waitlist works.
- React handles 409 clearly.
- README contains setup and demo instructions.

---

## 48. Final Instructions

When implementing this project:

1. First read the official course project requirements document.
2. Treat the course requirements as authoritative.
3. Use this document as the detailed implementation specification.
4. Build the project incrementally.
5. Keep the solution compiling after each major stage.
6. Do not implement everything in one huge unstructured step.
7. Create the architecture before implementing controllers.
8. Implement entities and EF Core configuration first.
9. Then repositories.
10. Then services.
11. Then authentication/authorization.
12. Then middleware/logging.
13. Then controllers.
14. Then tests.
15. Then React.
16. Run tests and build the entire solution before considering it complete.

Most importantly: **Do not simplify away the concurrency requirement.**

The system must genuinely use: the Class concurrency token (PostgreSQL `xmin`, see §9) + EF Core optimistic concurrency + DbUpdateConcurrencyException + HTTP 409 Conflict.

The final application must demonstrate that when two users compete for the last available seat, exactly one wins and the other receives a conflict.

The implementation should remain clean, understandable, maintainable, and appropriate for a software-engineering course project rather than an over-engineered production system.

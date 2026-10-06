# StudioFlow — Server

StudioFlow is a sports‑studio management API. Each class has a limited number of
seats; members compete for the last seat, classes fill up, waiting lists form,
and cancellations promote the next member in line. The most important technical
feature is **optimistic‑concurrency handling of the last available seat**.

This repository is the **backend** (ASP.NET Core Web API + PostgreSQL). The React
client lives in `studioflow-client/` (see its own README) and in a separate repo
for submission.

---

## Architecture

Four projects in `StudioFlow.sln`, plus a test project:

| Project | Responsibility |
|---|---|
| `StudioFlow.Core` | Entities, Enums, DTOs, interfaces (repositories, services, security), domain exceptions. **No dependencies, no EF Core.** |
| `StudioFlow.Data` | `StudioFlowDbContext`, Fluent `IEntityTypeConfiguration<T>`, repositories, `UnitOfWork`, migrations, dev seed. Depends on Core + Npgsql. |
| `StudioFlow.Service` | Business logic and orchestration; AutoMapper profile. Depends on Core **only** — never on Data or EF Core. |
| `StudioFlow.API` | Controllers, middleware (correlation id + global exception handling), JWT auth, DI wiring, NLog, Swagger, `Program.cs`. References Core + Service + Data (Data for DI registration only). |
| `StudioFlow.Tests` | xUnit + Moq service unit tests and the two‑`DbContext` concurrency proof. |

Dependency direction: `Core → nothing`, `Data → Core`, `Service → Core`,
`API → Core + Service + Data`.

**Domain model:** `User` (a "member" is a User with `Role = Member` — there is no
separate Member entity), `Instructor` (1:1 with a User), `Room`, `Class`,
`Registration`, `WaitlistEntry`, and `Tag` (`Class` ↔ `Tag` many‑to‑many via the
`ClassTags` join table). Unique constraint on `Registration(MemberId, ClassId)`.

---

## Prerequisites

* .NET SDK 8 (net8.0)
* Docker Desktop (for the PostgreSQL container)
* `dotnet-ef` global tool (only if you want to run migrations by hand):
  `dotnet tool install --global dotnet-ef --version 8.0.10`
* Node.js 20+ (only to run the React client)

---

## 1 — Start PostgreSQL (Docker)

```bash
docker run --name studioflow-pg \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=studioflow \
  -p 5432:5432 -d postgres:16

# afterwards, just:
docker start studioflow-pg
```

This gives a database `studioflow` on `localhost:5432`, user `postgres` /
password `postgres`. It is a disposable local container — that password is **not**
a production secret.

---

## 2 — Configure secrets (User Secrets)

The connection string and the JWT signing key are **not** stored in
source control (spec §43, §44). Set them once as .NET User Secrets on the API
project:

```bash
cd StudioFlow.API

dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=studioflow;Username=postgres;Password=postgres"

# any string of at least 32 characters; generate your own
dotnet user-secrets set "Jwt:Key" "<your-local-dev-signing-key-min-32-chars>"
```

`dotnet user-secrets list` shows what is set. In **Development** these override
the (empty) placeholders in `appsettings.json`. `appsettings.json` and
`appsettings.Development.json` contain only non‑secret values (issuer, audience,
token lifetime, log levels).

For non‑Development hosting, supply the same keys via environment variables
(`ConnectionStrings__DefaultConnection`, `Jwt__Key`) or your platform's secret
store.

---

## 3 — Run migrations and start the API

The API **applies pending migrations automatically on startup** (in every
environment), and in Development it also **seeds demo data** — you do not
normally need to run `dotnet ef`.

```bash
dotnet run --project StudioFlow.API --launch-profile http
```

* API: `http://localhost:5235`
* Swagger UI: `http://localhost:5235/swagger` (Development only)

To run migrations manually:

```bash
dotnet ef database update \
  -p StudioFlow.Data -s StudioFlow.API --context StudioFlowDbContext -- --environment Development
```

Two migrations exist: `InitialCreate` and `AddClassTags`.

---

## 4 — Run the React client (optional, for the full demo)

```bash
cd studioflow-client
npm install
npm run dev            # http://localhost:5173
```

The Vite dev server proxies `/api/*` to `http://localhost:5235`, so no CORS
configuration is needed on the API.

---

## 5 — Deploying to Render (Docker)

The `Dockerfile` at the repository root builds and publishes `StudioFlow.API`
(.NET 8, multi‑stage) and runs `StudioFlow.API.dll` on **HTTP port 8080**.
`ASPNETCORE_ENVIRONMENT` is not set, so the container runs as **Production**
(no Swagger, no seed).

Create a Render **Web Service** (runtime: Docker) and set these environment variables:

| Variable | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | Npgsql key/value format: `Host=…;Port=5432;Database=…;Username=…;Password=…` (Render shows a `postgresql://` URL — convert it) |
| `Jwt__Key` | A new random secret, **at least 32 characters** |
| `Cors__AllowedOrigins` | The React client's origin, e.g. its `https://….onrender.com` URL (comma‑separated for several; no path) |
| `PORT` | `8080` — tells Render which port the container listens on |

`Jwt:Issuer` (`StudioFlow`), `Jwt:Audience` (`StudioFlowClient`) and
`Jwt:ExpiryMinutes` (`120`) already come from `appsettings.json`.

On startup the API applies any pending migrations to the configured database.
Demo data is **not** seeded outside Development, so a new deployment has no
users: register through the client, and promote an admin by setting
`"Role" = 0` for that row in the `Users` table.

---

## Authentication & roles

* `POST /api/auth/register` — public. Always creates a **Member** (the role is set
  by the server; clients cannot choose it).
* `POST /api/auth/login` — public. Returns a JWT plus `{ userId, name, email, role }`.
  Registration does **not** return a token — log in afterwards.
* Every other protected call sends `Authorization: Bearer <token>`.
* The JWT carries the user id (`sub` / `nameidentifier`), `email` and `role`
  claims; the API validates issuer, audience, signing key and lifetime.
* 401 → `UNAUTHORIZED`, 403 → `FORBIDDEN`, both in the uniform error shape below.

### Roles

| Role | Can |
|---|---|
| **Member** | Browse / search / filter classes, view details, register, cancel a registration, join / leave a waiting list, view own registrations. |
| **Instructor** | View classes, view the **participants of their own classes** (the API checks ownership; other classes return 403). |
| **Admin** | All Member/Instructor read access **plus** create / edit / cancel classes, view any class's participants, and full CRUD for rooms and instructors. |

Role authorization is enforced by the API (`[Authorize(Roles = …)]` plus
resource‑ownership checks in the service layer). The React client mirrors it for
navigation only.

### Demo users

All seeded users share the password **`Password123!`** (this is the example
password from the project spec, used only for the local demo — it is stored
hashed, never in plaintext):

| Email | Role |
|---|---|
| `admin@studioflow.local` | Admin |
| `instructor@studioflow.local` | Instructor (Ilana Cohen) |
| `member1@studioflow.local` … `member3@studioflow.local` | Member |

---

## API surface (spec §21)

| Method | Route | Access |
|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | public |
| GET | `/api/classes`, `/api/classes/{id}` | public |
| POST / PUT | `/api/classes`, `/api/classes/{id}` | Admin |
| POST | `/api/classes/{id}/cancel` | Admin |
| GET | `/api/classes/{id}/participants` | Admin, or the class's instructor |
| POST / DELETE | `/api/classes/{id}/register` | Member |
| GET | `/api/me/registrations` | Member |
| POST / DELETE | `/api/classes/{id}/waitlist` | Member |
| GET / GET / POST / PUT / DELETE | `/api/rooms[/{id}]` | Admin |
| GET / GET / POST / PUT / DELETE | `/api/instructors[/{id}]` | Admin |

`GET /api/classes` supports **server‑side** paging and filtering:
`?page=&pageSize=&search=&status=&date=&instructorId=&roomId=`.

### Error shape

Every error response is:

```json
{ "code": "NOT_FOUND", "message": "…", "correlationId": "…" }
```

`code` ∈ `VALIDATION_ERROR` (400), `UNAUTHORIZED` (401), `FORBIDDEN` (403),
`NOT_FOUND` (404), `CONFLICT` (409), `CONCURRENCY_CONFLICT` (409),
`INTERNAL_ERROR` (500). Stack traces and EF Core details are never returned.

---

## Registration vs. waiting list

These are **separate, explicit actions**:

* `POST /api/classes/{id}/register` on a full class returns **409 `CONFLICT`**
  ("This class is full."). It does **not** silently add you to the waiting list.
* `POST /api/classes/{id}/waitlist` is how you join the list. Position is
  assigned as *(current waiting count) + 1*.
* When a member cancels a registration, the first waiting member is **promoted**
  in the same database transaction: their waiting entry becomes `Promoted`, a new
  active `Registration` is created for them, and `Class.RegisteredCount` is kept
  correct.
* Re‑registering (or re‑joining the list) after a cancellation **reactivates the
  existing row** rather than inserting a duplicate — the
  `Registration(MemberId, ClassId)` unique index is respected.

---

## Concurrency — the last available seat

The contended resource is `Class` capacity. `Class` has a `uint Version` property
mapped read‑only to PostgreSQL's system **`xmin`** column and marked as the EF
concurrency token (`ClassConfiguration`).

Registration flow (in `RegistrationService`):

1. Load the `Class` **tracked** (so `xmin` travels with it).
2. Check the business rules (active, not started, not already registered, seat free).
3. Increment `Class.RegisteredCount` and stage the `Registration`.
4. Commit **once** through `IUnitOfWork.SaveChangesAsync`.

If two requests race for the final seat, PostgreSQL bumps `xmin` on the first
commit; the second commit's `UPDATE … WHERE xmin = <stale>` affects zero rows and
EF throws `DbUpdateConcurrencyException`. The Data layer's `UnitOfWork`
**translates that into `ConcurrencyConflictException`** (so Service and API never
depend on EF Core), and the API returns **409 `CONCURRENCY_CONFLICT`** with the
message *"The last available seat was taken by another user."* The losing
transaction is rolled back whole — no partial registration is persisted.

Result: exactly one 201, exactly one 409, exactly one registration row.

---

## Logging (NLog)

Application code uses `Microsoft.Extensions.Logging` / `ILogger<T>`; **NLog is the
provider underneath** (`Program.cs` → `builder.Host.UseNLog()`; config in
`StudioFlow.API/nlog.config`).

* **Console** (coloured) and a **file** target: `StudioFlow.API/bin/Debug/net8.0/logs/studioflow-<date>.log`.
* Every line carries the request **correlation id** (`cid=…`), set by
  `CorrelationIdMiddleware` and reused from an incoming `X-Correlation-Id` header.
* Levels: `Debug` (dev diagnostics), `Information` (logins, registrations,
  cancellations, waitlist promotions), `Warning` (handled conflicts incl.
  concurrency), `Error` (unhandled exceptions → 500).
* **Never logged:** passwords, password hashes, JWT tokens, request bodies or
  headers. Framework SQL/routing noise below `Warning` is suppressed.

---

## Tests

```bash
# Docker PostgreSQL must be running for the concurrency proof.
docker start studioflow-pg
dotnet test StudioFlow.sln
```

* **Service unit tests** (`StudioFlow.Tests`, xUnit + Moq) — repositories and
  `IUnitOfWork` are mocked. They cover the §14/§15/§16/§18 business rules:
  registration success / full‑class conflict / duplicate / cancelled / started,
  cancellation, waiting‑list join / position assignment / promotion, class
  capacity and schedule‑overlap validation, instructor‑ownership authorization
  for participants, room/instructor delete‑guards, and the auth flows.
* **`LastSeatConcurrencyProofTests`** (spec §37/§38) — the real thing: two
  independent `StudioFlowDbContext` instances, one free seat, the loser gets
  `ConcurrencyConflictException`, the database ends with exactly one row. It uses
  the local Docker database and cleans up after itself. Override the connection
  with the `STUDIOFLOW_TEST_CONNECTION` environment variable if yours differs;
  without a reachable database this single test fails with a clear message while
  the rest pass.

---

## Deliberate limitations / deferred work

Documented so nothing looks missing by accident:

* **No user‑management UI or API.** Spec §13 mentions "Admin manages users" but
  §21 defines no user endpoints, so there is none. Admins create instructor
  accounts via `POST /api/instructors`; members self‑register.
* **Instructor "My Classes" in the client is matched by name**, because the JWT
  carries only the user id and `GET /api/instructors*` is Admin‑only, so an
  instructor cannot obtain their own `instructorId` for the server‑side
  `?instructorId=` filter. It is a display convenience — every write/participant
  call is still authorized by the API.
* **No tag management / assignment.** Tags are seeded reference data shown on the
  class detail screen; §21 defines no tag endpoints and the class DTOs have no
  tag field.
* **Instructors cannot edit classes.** §21 makes `PUT /api/classes/{id}`
  Admin‑only; there is no instructor edit endpoint.
* **No member profile screen.** The four member screens in the spec are
  Login/Register, Classes, Class details, My registrations; identity is shown in
  the top bar.
* `UseHttpsRedirection` is in the pipeline (spec §33) but is a no‑op under the
  `http` launch profile, which has no HTTPS port.

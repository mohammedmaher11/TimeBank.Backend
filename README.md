# TimeBank Backend

A community time bank where the currency is **time**. Every hour is equal: a
plumber who fixes a washing machine earns the same hour that pays for an
English lesson. This repository is the **backend-only** deliverable: a REST
API, the domain rules, a real SQL Server schema, seed data, tests, Swagger,
Postman, and database scripts.

> **Scope:** the MVC website and its back-office are intentionally **not** in
> this deliverable. The API is complete and ready for an MVC or mobile client.
> The audit schema already supports `Source = BackOffice` for that future
> client.

## Tech stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 |
| Data | EF Core 10, SQL Server (LocalDB/Express/Developer), code-first migrations |
| Auth | JWT Bearer, BCrypt password hashing, role-based authorization |
| API docs | Swagger / OpenAPI with Bearer support |
| Tests | xUnit, Moq, `WebApplicationFactory<Program>` against real SQL Server |
| Tooling | `dotnet-ef` local tool, PowerShell 5.1 smoke script, Postman collection |

## Repository layout

```
TimeBank.Backend/
├─ TimeBank.Backend.sln
├─ src/
│  ├─ TimeBank.Domain/          entities, enums, domain events, exceptions, HoursPolicy
│  ├─ TimeBank.Application/     services, DTOs, repository/UoW interfaces, mapping
│  ├─ TimeBank.Infrastructure/  EF Core, configurations, migrations, repositories, seeder, JWT, BCrypt
│  └─ TimeBank.API/             controllers, middleware, filters, Swagger, Program.cs
├─ tests/
│  ├─ TimeBank.Core.Tests/      unit tests with Moq
│  └─ TimeBank.IntegrationTests/ real SQL Server HTTP integration tests
├─ database/
│  ├─ sp_GetMemberStatement.sql
│  ├─ indexes.sql
│  └─ reports.sql
├─ docs/
│  ├─ ERD.md
│  ├─ indexes.md
│  ├─ verification.md
│  ├─ sprint-reviews.md
│  ├─ postman-run.md
│  └─ postman/
├─ postman/
│  └─ TimeBank.postman_collection.json
├─ scripts/
│  └─ smoke-test.ps1
├─ .gitignore
├─ dotnet-tools.json
└─ README.md
```

Dependency direction: `API → Application → Domain`; `Infrastructure →
Application/Domain`. The Domain project references no other project and no
EF Core package.

## Prerequisites

- .NET SDK **10.0** (`dotnet --version`).
- SQL Server with a database you can create: SQL Server Express, LocalDB, or
  Developer/Standard. The checked-in development configuration uses
  `localhost\SQLEXPRESS` with Windows integrated authentication.
- `sqlcmd` (optional, for the database scripts).
- PowerShell 5.1+ for the smoke script.
- Postman (optional) for the collection.

The project uses a local tool manifest. Restore it once:

```powershell
dotnet tool restore
```

## Configuration

Development configuration is in `src/TimeBank.API/appsettings.Development.json`.
It contains:

- `ConnectionStrings:DefaultConnection` — SQL Server connection string using
  **Windows integrated authentication** (no password in the file).
- `Jwt:Key` — a **DEV-ONLY** signing key, clearly marked as such. Replace it
  before any deployment.
- `Database:ApplyMigrationsOnStartup` and `Database:SeedOnStartup` — enabled in
  Development so a fresh database is created and seeded automatically.

For a different machine, either edit the development connection string or use
environment variables (recommended for secrets):

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=localhost\SQLEXPRESS;Database=TimeBankDb;Trusted_Connection=True;TrustServerCertificate=True'
$env:Jwt__Key = '<a random secret of at least 32 bytes>'
$env:Jwt__Issuer = 'TimeBank.API'
$env:Jwt__Audience = 'TimeBank.Client'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
```

`appsettings.json` deliberately has an empty `Jwt:Key` and migrations/seed
disabled, so production must supply both explicitly. Never commit a real key
or a SQL password. The API refuses to start if `Jwt:Key` is missing or shorter
than 32 bytes.

## Database setup

### Option A — automatic (development)

Start the API with the Development environment; `Program.cs` applies migrations
and runs the idempotent seeder:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src\TimeBank.API --urls http://localhost:5080
```

### Option B — explicit migrations

```powershell
dotnet tool restore
dotnet ef database update `
  --project src\TimeBank.Infrastructure `
  --startup-project src\TimeBank.API
```

The migrations are:

1. `20261025013240_InitialCreate`
2. `20261025013402_AddMemberStatementProcedure`

Then start the API; with `Database:SeedOnStartup=true` it seeds on first run.
The seeder is idempotent: it only inserts data when the corresponding tables
are empty, and it hashes seed passwords with BCrypt.

### Seed data

| Entity | Rows |
|---|---|
| Members | 10 |
| Categories | 10 |
| Skills | 34 |
| MemberSkills | 25 |
| HelpRequests | 7 |
| LedgerEntries | 10 (one 2-hour welcome bonus per member) |
| Badges | 3 definitions (no awards until a member earns one) |
| MemberBadges | 0 (awards are created by completed exchanges) |

All seeded members use the development password **`Sa3a@2026`**. The seeded
moderator is **`mona.adel@sa3a.test`**. These are development fixtures, not
production accounts.

### Apply the standalone database scripts

```powershell
sqlcmd -S "localhost\SQLEXPRESS" -C -E -d TimeBankDb -i database\sp_GetMemberStatement.sql
sqlcmd -S "localhost\SQLEXPRESS" -C -E -d TimeBankDb -i database\indexes.sql
sqlcmd -S "localhost\SQLEXPRESS" -C -E -d TimeBankDb -i database\reports.sql
```

`-C` trusts the local self-signed certificate. The scripts are idempotent;
`reports.sql` prints several reports and ends with an example statement.

### Reset a development database

> This deletes all data in `TimeBankDb`. Only use it on a development machine.

```powershell
sqlcmd -S "localhost\SQLEXPRESS" -C -E -d master -Q "IF DB_ID(N'TimeBankDb') IS NOT NULL BEGIN ALTER DATABASE [TimeBankDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TimeBankDb]; END"
dotnet ef database update --project src\TimeBank.Infrastructure --startup-project src\TimeBank.API
```

Then start the API once to seed.

## Run the API

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src\TimeBank.API --urls http://localhost:5080
```

| Resource | URL |
|---|---|
| API base | http://localhost:5080 |
| Swagger UI | http://localhost:5080/swagger |
| OpenAPI JSON | http://localhost:5080/swagger/v1/swagger.json |
| Health | http://localhost:5080/health |
| Community stats (minimal API) | http://localhost:5080/api/stats |

Swagger supports protected endpoints: call `POST /api/auth/login`, copy the
`token`, click **Authorize**, and paste the token without the word `Bearer`.

## API surface

| Method | Route | Access | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | anonymous | Register with existing skills; returns JWT + member |
| POST | `/api/auth/login` | anonymous | BCrypt login; returns JWT |
| GET | `/api/categories` | anonymous | Categories with their fixed skills |
| GET | `/api/requests?categoryId=&district=&status=` | anonymous | Request board with filters |
| GET | `/api/requests/{id}` | anonymous | Request details |
| POST | `/api/requests` | member | Create a request |
| POST | `/api/requests/{id}/offers` | member | Send an offer (BR-06) |
| GET | `/api/requests/{id}/offers` | request owner | See offers; others get 403 |
| POST | `/api/offers/{id}/accept` | request owner | Accept one offer (BR-07) |
| POST | `/api/requests/{id}/complete` | request owner | Complete atomically (BR-08) |
| POST | `/api/requests/{id}/cancel` | request owner | Cancel (BR-09) |
| POST | `/api/requests/{id}/reviews` | either party | Review once after completion (BR-11) |
| GET | `/api/me` | member | Current member summary |
| GET | `/api/me/balance` | member | `{ balance, locked, available }` |
| GET | `/api/me/statement?from=&to=` | member | Stored-procedure statement |
| PUT | `/api/me/skills` | member | Replace skills with existing skill ids |
| GET | `/api/members` | anonymous | Member directory |
| GET | `/api/members/{id}` | anonymous | Profile with skills, badges, rating, hours |
| GET | `/api/admin/members` | moderator | Member list |
| PUT | `/api/admin/members/{id}/suspend` | moderator | Suspend + audit |
| PUT | `/api/admin/members/{id}/reinstate` | moderator | Reinstate + audit |
| GET | `/api/admin/audit-logs?take=` | moderator | Audit trail |
| POST/PUT/DELETE | `/api/categories` | moderator | Category CRUD + audit |
| GET | `/api/stats` | anonymous | Minimal-API community counters |

All errors are RFC 7807 `application/problem+json` with a stable `code`
extension (for example `CONFLICT`, `INSUFFICIENT_HOURS`, `FORBIDDEN`,
`BUSINESS_RULE`). Validation failures from data annotations return 400.

## Business rules

The numbered rules from the project brief are implemented in the domain and
application layers, not in controllers:

- **BR-01/BR-02/BR-03** — every hour is equal; new members get a `+2`
  `WelcomeBonus`; hours are half-hour steps, requests are 0.5–8, ledger entries
  are never zero.
- **BR-04/BR-05** — the ledger is append-only and is the only source of the
  balance; `Available = Balance − Locked`, where locked is the accepted hours
  of the member's `InProgress` requests.
- **BR-06** — offer only on an Open request, not your own, one offer per member
  per request, and at least one skill in the request's category.
- **BR-07** — accepting checks available balance, rejects all other pending
  offers, moves to `InProgress`, and locks the accepted hours.
- **BR-08** — completion writes the requester's debit, the helper's credit, and
  the `Completed` status in one transaction; a repeat is a 409 with no second
  transfer.
- **BR-09** — Open/InProgress can be cancelled; Completed/Cancelled are final.
- **BR-10** — "Needs Love" is computed for Open requests older than 14 days.
- **BR-11** — after completion each party can review the other once, rating
  1–5.
- **BR-12** — `ExchangeCompleted` raises a domain event; `BadgeService` awards
  First Hand, Ten Hours Hero, and Good Neighbor once each.
- **BR-13/BR-14** — Moderator-only admin actions; suspension is checked against
  the current database on every mutating action, so an old JWT gets 403.
- **BR-15** — successful moderator actions are written to `AuditLogs` by
  `AuditFilter`.

See `docs/verification.md` for the evidence behind each rule.

## Tests and verification

### Unit tests (50)

```powershell
dotnet test tests\TimeBank.Core.Tests\TimeBank.Core.Tests.csproj
```

These are Moq-based tests of the domain/application rules: offer conditions,
accept/reject, atomic completion intent, repeat operations, cancellation,
reviews, badges, balance, member skills, and half-hour validation.

### Integration tests (11)

Requires SQL Server and permission to create/drop a database:

```powershell
dotnet test tests\TimeBank.IntegrationTests\TimeBank.IntegrationTests.csproj
```

Each fixture creates a **unique real database** (`TimeBankDb_Test_{guid}`),
applies the real migrations, runs the real seeder, and drops the database when
the fixture is disposed. There is no InMemory provider anywhere.

The suite covers health/Swagger/seed data, the member directory, registration
+ welcome bonus, unknown-skill rollback, the full exchange and reviews,
suspension with an old JWT, category audit, insufficient balance, parallel
accepts, a real forced ledger failure with rollback, and
schema/index/procedure assertions.

### Run everything

```powershell
dotnet build TimeBank.Backend.sln
dotnet test TimeBank.Backend.sln
```

Verified on 2026-10-25: build **0 warnings, 0 errors**; **50/50 unit** and
**11/11 integration** tests passed. The same commands were also run from a
clean extraction of the delivered ZIP.

### Live smoke test (60 checks)

Start the API, then in another PowerShell window:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-test.ps1
```

The script creates unique members, so it can be run repeatedly against a
development database.

### Postman

Import `postman/TimeBank.postman_collection.json` into Postman. The collection
variable `baseUrl` defaults to `http://localhost:5080`; change it if needed.
Run the whole collection in order with the Collection Runner. It covers:

- health, stats, categories;
- register two members with skills, login the seeded moderator;
- create request, invalid-hours 400, offer, duplicate-offer 409;
- owner-only offers 403, accept, repeated-accept 409;
- balance before/after completion, complete, repeated-complete 409,
  cancel-completed 409;
- reviews by both parties, duplicate-review 409;
- balances, statement (stored procedure), directory/profile with badge;
- member-calls-admin 403, suspend, **old token after suspension 403**,
  suspended login 403, reinstate, login again;
- moderator category CRUD and audit-log assertions;
- final stats.

Run the same collection headlessly with Node.js 18+ and Newman:

```powershell
npm install -g newman
newman run postman\TimeBank.postman_collection.json `
  --reporters cli,json `
  --reporter-json-export .run\newman-result.json
```

The collection was executed against the real API and SQL Server on
**2026-10-25** with Newman **6.2.3**: **37/37 requests and 37/37 assertions
passed, 0 failures, exit code 0**. The full command, variable reference,
troubleshooting table, and the defect this run caught are documented in
[`docs/postman-run.md`](docs/postman-run.md).

## Design notes

- **Encapsulation:** entity state changes only through methods such as
  `HelpRequest.AcceptOffer`, `Complete`, and `Cancel`; setters are private.
- **Repository + Unit of Work:** repositories expose intention-revealing
  queries; `UnitOfWork.ExecuteInTransactionAsync` wraps a `ReadCommitted`
  transaction and an explicit rollback on failure.
- **Factory:** `LedgerEntryFactory` creates `WelcomeBonus`, `ExchangeDebit`,
  and `ExchangeCredit` entries consistently.
- **Observer via domain events:** `ExchangeCompletedDomainEvent` is dispatched
  after the exchange commits, so badge awarding cannot roll back the transfer.
- **Concurrency:** `HelpRequests.RowVersion` is an optimistic token. For
  balance-affecting operations, `sp_getapplock` serializes work per member
  inside the transaction. This closes the "two simultaneous accepts" gap for
  the same requester's balance.
- **No retry-on-failure:** EF Core's retrying execution strategy is deliberately
  disabled because a blind retry of an explicit transaction could duplicate a
  transfer. The API uses a single explicit transaction with rollback.

## Known limitations

- **Backend only.** The MVC website/back-office is not included. The schema
  supports `AuditSource.BackOffice` and an `Actor` string, but there is no MVC
  UI in this repository.
- **Dev key.** `appsettings.Development.json` contains a DEV-ONLY JWT key and
  seeded dev passwords. Replace them for any real deployment.
- **No refresh tokens or token revocation list.** Suspension is enforced by
  checking the current database state on mutating actions; an old token can
  still call read-only endpoints until it expires.
- **List endpoints are unpaginated.** The board and directory return all rows.
  Pagination is a sensible next step for production data volumes.
- **CORS is open in Development only.** Non-development environments use HTTPS
  redirection and do not enable the permissive policy.
- **Simultaneous non-balance state changes** rely on the `rowversion` token and
  current-state checks; the balance path additionally uses `sp_getapplock`.
- **No background jobs.** Badge evaluation runs after completion in the same
  request; there is no outbox/queue.

## Author

Prepared as the backend deliverable for the **TimeBank** final project.
Add your name and LinkedIn URL here before publishing if required by your
submission template.

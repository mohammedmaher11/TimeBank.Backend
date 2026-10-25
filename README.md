# TimeBank Backend

TimeBank is a community platform where members exchange services using time instead of money. One hour of work is worth one time credit, regardless of the service provided. For example, a member who repairs a washing machine earns the same number of credits as someone who gives an English lesson for the same amount of time.

This repository contains the backend: the REST API, business rules, SQL Server database, EF Core migrations, seed data, automated tests, Swagger documentation, Postman collection, and SQL scripts.

The MVC website and back-office interface are not included. The API is intended to be used by an MVC application, mobile app, or another HTTP client. The audit schema also includes `Source = BackOffice` for future integration.

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10, ASP.NET Core 10 |
| Database | SQL Server, EF Core 10, code-first migrations |
| Authentication | JWT Bearer, BCrypt password hashing |
| Authorization | Role-based access control |
| API documentation | Swagger / OpenAPI |
| Unit testing | xUnit, Moq |
| Integration testing | `WebApplicationFactory<Program>`, SQL Server |
| Development tools | `dotnet-ef`, PowerShell 5.1+, Postman |

## Repository Structure

```text
TimeBank.Backend/
├── TimeBank.Backend.sln
├── src/
│   ├── TimeBank.Domain/
│   ├── TimeBank.Application/
│   ├── TimeBank.Infrastructure/
│   └── TimeBank.API/
├── tests/
│   ├── TimeBank.Core.Tests/
│   └── TimeBank.IntegrationTests/
├── database/
│   ├── sp_GetMemberStatement.sql
│   ├── indexes.sql
│   └── reports.sql
├── docs/
│   ├── ERD.md
│   ├── indexes.md
│   ├── verification.md
│   ├── sprint-reviews.md
│   ├── postman-run.md
│   └── postman/
├── postman/
│   └── TimeBank.postman_collection.json
├── scripts/
│   └── smoke-test.ps1
├── .gitignore
├── dotnet-tools.json
└── README.md
```

The solution is split into four projects:

- **Domain:** entities, enums, domain events, exceptions, and `HoursPolicy`.
- **Application:** services, DTOs, mapping, and repository and unit-of-work interfaces.
- **Infrastructure:** EF Core configuration, migrations, repositories, database seeding, and security implementations.
- **API:** controllers, middleware, filters, Swagger, and application startup.

The main dependency direction is `API → Application → Domain`. Infrastructure implements the abstractions defined by the inner layers. The Domain project does not reference the other projects or EF Core.

## Prerequisites

Install the following before running the project:

- .NET SDK 10.0. Check the installed version with `dotnet --version`.
- SQL Server Express, LocalDB, or Developer/Standard edition.
- `sqlcmd` for running the standalone SQL scripts (optional).
- PowerShell 5.1 or later for the smoke-test script.
- Postman for running the supplied collection (optional).

The repository includes a local .NET tool manifest. Restore the tools with:

```powershell
dotnet tool restore
```

## Configuration

The API reads its settings from `appsettings.json`, the development configuration, and environment variables.

The development connection string should point to a SQL Server instance available on your machine. The examples below use SQL Server Express with Windows integrated authentication.

Set the development environment and connection string:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__DefaultConnection = 'Server=localhost\SQLEXPRESS;Database=TimeBankDb;Trusted_Connection=True;TrustServerCertificate=True'
```

Configure the JWT settings:

```powershell
$env:Jwt__Key = '<a random secret of at least 32 bytes>'
$env:Jwt__Issuer = 'TimeBank.API'
$env:Jwt__Audience = 'TimeBank.Client'
```

The API requires a JWT signing key of at least 32 bytes and will not start if the key is missing or too short.

Database migration and seeding options are controlled by:

- `Database:ApplyMigrationsOnStartup`
- `Database:SeedOnStartup`

Both can be enabled for local development. For production, supply the required settings through the deployment environment and do not use development credentials or keys.

**Configuration files containing local settings or secrets are excluded from the repository.** Create the necessary local configuration files on your machine or supply the settings through environment variables.

Never commit a production JWT key, database password, or other real credentials.

## Database Setup

### Option A: Apply migrations on startup

For local development, enable migrations and seeding in the development configuration, then run the API:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'

dotnet run --project src\TimeBank.API --urls http://localhost:5080
```

When the corresponding settings are enabled, the application applies the EF Core migrations and runs the seeder at startup.

### Option B: Apply migrations manually

Restore the local tools and update the database:

```powershell
dotnet tool restore

dotnet ef database update `
  --project src\TimeBank.Infrastructure `
  --startup-project src\TimeBank.API
```

The migrations included in the project are:

1. `20261025013240_InitialCreate`
2. `20261025013402_AddMemberStatementProcedure`

Start the API after the database has been created. If seeding is enabled, the application inserts the initial data.

The seeder checks whether the relevant tables already contain data before inserting records. Seed passwords are hashed using BCrypt.

### Seed Data

The development database is seeded with the following records:

| Entity | Records |
|---|---:|
| Members | 10 |
| Categories | 10 |
| Skills | 34 |
| MemberSkills | 25 |
| HelpRequests | 7 |
| LedgerEntries | 10 |
| Badge definitions | 3 |
| MemberBadges | 0 |

Each seeded member receives a two-hour welcome bonus. Badge awards are created when the corresponding exchange conditions are met.

The seed data uses development-only credentials. The seeded moderator account is `mona.adel@sa3a.test`, and the shared development password is `Sa3a@2026`. These credentials are intended for local testing only and must not be used for real accounts.

### Run the SQL Scripts

The `database` directory contains a stored procedure, index definitions, and reporting queries.

Run them against the development database using:

```powershell
sqlcmd -S "localhost\SQLEXPRESS" -C -E -d TimeBankDb -i database\sp_GetMemberStatement.sql

sqlcmd -S "localhost\SQLEXPRESS" -C -E -d TimeBankDb -i database\indexes.sql

sqlcmd -S "localhost\SQLEXPRESS" -C -E -d TimeBankDb -i database\reports.sql
```

The options `-E` and `-C` use Windows authentication and trust the local SQL Server certificate, respectively. Use certificate settings appropriate for your environment.

The scripts are designed to be rerun safely. `reports.sql` executes several reports and finishes with an example statement query.

### Reset the Development Database

**Warning:** This deletes the `TimeBankDb` database and all its data. Run it only against a development database.

```powershell
sqlcmd -S "localhost\SQLEXPRESS" -C -E -d master -Q "IF DB_ID(N'TimeBankDb') IS NOT NULL BEGIN ALTER DATABASE [TimeBankDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TimeBankDb]; END"

dotnet ef database update --project src\TimeBank.Infrastructure --startup-project src\TimeBank.API
```

Start the API afterward to run the seeder if seeding is enabled.

## Running the API

Start the application with the Development environment:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'

dotnet run --project src\TimeBank.API --urls http://localhost:5080
```

The default local URLs are:

| Resource | URL |
|---|---|
| API base URL | http://localhost:5080 |
| Swagger UI | http://localhost:5080/swagger |
| OpenAPI document | http://localhost:5080/swagger/v1/swagger.json |
| Health check | http://localhost:5080/health |
| Community statistics | http://localhost:5080/api/stats |

Swagger supports JWT-protected endpoints. Call `POST /api/auth/login`, copy the returned token, click **Authorize**, and paste the token without the `Bearer` prefix.

## API Endpoints

The API includes authentication, requests, offers, member profiles, balances, reviews, moderation, and community statistics.

| Method | Route | Access | Description |
|---|---|---|---|
| POST | `/api/auth/register` | Anonymous | Register a member with existing skills and return a JWT |
| POST | `/api/auth/login` | Anonymous | Authenticate a member and return a JWT |
| GET | `/api/categories` | Anonymous | List categories and their skills |
| GET | `/api/requests?categoryId=&district=&status=` | Anonymous | List requests with filters |
| GET | `/api/requests/{id}` | Anonymous | Get request details |
| POST | `/api/requests` | Member | Create a help request |
| POST | `/api/requests/{id}/offers` | Member | Submit an offer (BR-06) |
| GET | `/api/requests/{id}/offers` | Request owner | View offers for a request |
| POST | `/api/offers/{id}/accept` | Request owner | Accept an offer (BR-07) |
| POST | `/api/requests/{id}/complete` | Request owner | Complete an exchange (BR-08) |
| POST | `/api/requests/{id}/cancel` | Request owner | Cancel a request (BR-09) |
| POST | `/api/requests/{id}/reviews` | Either party | Submit a review after completion (BR-11) |
| GET | `/api/me` | Member | Get the current member's summary |
| GET | `/api/me/balance` | Member | Get balance, locked hours, and available hours |
| GET | `/api/me/statement?from=&to=` | Member | Retrieve the stored-procedure statement |
| PUT | `/api/me/skills` | Member | Replace the member's skill IDs |
| GET | `/api/members` | Anonymous | List members |
| GET | `/api/members/{id}` | Anonymous | Get a member's profile, skills, badges, rating, and hours |
| GET | `/api/admin/members` | Moderator | List members for moderation |
| PUT | `/api/admin/members/{id}/suspend` | Moderator | Suspend a member and write an audit record |
| PUT | `/api/admin/members/{id}/reinstate` | Moderator | Reinstate a member and write an audit record |
| GET | `/api/admin/audit-logs?take=` | Moderator | Retrieve audit logs |
| POST/PUT/DELETE | `/api/categories` | Moderator | Create, update, and delete categories |
| GET | `/api/stats` | Anonymous | Return community counters |

Errors use RFC 7807 `application/problem+json` responses with a `code` extension. Examples include `CONFLICT`, `INSUFFICIENT_HOURS`, `FORBIDDEN`, and `BUSINESS_RULE`. Validation errors from data annotations return HTTP 400.

## Business Rules

The business rules are implemented in the Domain and Application layers rather than being handled entirely by the controllers.

- **BR-01 / BR-02 / BR-03 — Time credits:** Every hour has the same value. New members receive a `+2` `WelcomeBonus`. Hours use half-hour increments, requests range from 0.5 to 8 hours, and ledger entries cannot be zero.
- **BR-04 / BR-05 — Balances:** The ledger is append-only and is the source of balance calculations. `Available = Balance − Locked`, where locked hours come from the member's `InProgress` requests.
- **BR-06 — Offers:** Offers can be submitted only for open requests. Members cannot offer on their own requests, submit duplicate offers for the same request, or offer without a skill in the request's category.
- **BR-07 — Accepting offers:** Acceptance checks the available balance, rejects other pending offers, changes the request to `InProgress`, and locks the accepted hours.
- **BR-08 — Completing exchanges:** Completion records the requester's debit, the helper's credit, and the `Completed` status in one transaction. Repeating the operation returns HTTP 409 without transferring the hours again.
- **BR-09 — Cancellation:** Open and in-progress requests can be cancelled. Completed and cancelled requests are final.
- **BR-10 — Needs Love:** The status is calculated for open requests that are more than 14 days old.
- **BR-11 — Reviews:** After completion, each party can review the other once. Ratings range from 1 to 5.
- **BR-12 — Badges:** Completing an exchange raises an `ExchangeCompleted` domain event. `BadgeService` awards First Hand, Ten Hours Hero, and Good Neighbor when their conditions are met, without awarding the same badge twice.
- **BR-13 / BR-14 — Moderation:** Administrative actions require the moderator role. Mutating actions check the member's current database status, so an old JWT does not bypass a suspension.
- **BR-15 — Audit logging:** Successful moderator actions are recorded in `AuditLogs` by `AuditFilter`.

The rule-by-rule verification details are available in [`docs/verification.md`](docs/verification.md).

## Tests and Verification

### Unit Tests

The project includes 50 unit tests.

Run them with:

```powershell
dotnet test tests\TimeBank.Core.Tests\TimeBank.Core.Tests.csproj
```

The Moq-based tests cover domain and application behavior, including offer validation, accepting and rejecting offers, exchange completion, repeated operations, cancellation, reviews, badge awards, balance calculations, member skills, and half-hour validation.

### Integration Tests

The integration test project contains 11 tests. SQL Server and permission to create and drop databases are required.

```powershell
dotnet test tests\TimeBank.IntegrationTests\TimeBank.IntegrationTests.csproj
```

Each fixture creates a separate database with a name such as `TimeBankDb_Test_{guid}`, applies the actual migrations, runs the seeder, and drops the database when the fixture is disposed.

The tests use real SQL Server rather than an EF Core InMemory provider.

Coverage includes:

- Health checks, Swagger, and seed data.
- Member directory and registration.
- Welcome bonuses and rollback after invalid skill selection.
- The exchange and review workflow.
- Suspension when a member has an old JWT.
- Category audit logging.
- Insufficient balances and concurrent offer acceptance.
- Transaction rollback after a forced ledger failure.
- Database schema, indexes, and stored procedure checks.

### Build and Run All Tests

```powershell
dotnet build TimeBank.Backend.sln
dotnet test TimeBank.Backend.sln
```

The recorded verification run on **2026-10-25** reported:

- Build: 0 warnings and 0 errors.
- Unit tests: 50/50 passed.
- Integration tests: 11/11 passed.

The same commands were also run from a clean extraction of the delivered ZIP.

### Smoke Test

The PowerShell smoke-test script performs 60 checks against a running API.

Start the API first, then open another PowerShell window and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-test.ps1
```

The script creates unique members so it can be run repeatedly against a development database.

### Postman Collection

Import `postman/TimeBank.postman_collection.json` into Postman. The `baseUrl` collection variable defaults to `http://localhost:5080`.

Run the collection in order using the Collection Runner. It covers:

- Health checks, statistics, and categories.
- Registration of two members with skills and moderator login.
- Request creation, invalid-hours validation, and duplicate offers.
- Request-owner authorization and offer acceptance.
- Balance checks before and after completion.
- Repeated completion and cancellation of completed requests.
- Reviews by both parties and duplicate-review rejection.
- Member balances, statements, profiles, and badges.
- Moderator access checks, suspension, and reinstatement.
- Category CRUD operations and audit-log checks.
- Final community statistics.

The collection can also be run from the command line using Node.js 18+ and Newman.

Install Newman:

```powershell
npm install -g newman
```

Run the collection:

```powershell
newman run postman\TimeBank.postman_collection.json `
  --reporters cli,json `
  --reporter-json-export .run\newman-result.json
```

The recorded run against the real API and SQL Server on **2026-10-25**, using Newman **6.2.3**, completed with 37/37 requests and 37/37 assertions passing, zero failures, and exit code 0.

The command, collection variables, troubleshooting notes, and the defect identified during that run are documented in [`docs/postman-run.md`](docs/postman-run.md).

## Design Notes

- **Domain model:** Entity state changes are handled through methods such as `HelpRequest.AcceptOffer`, `Complete`, and `Cancel`. Setters are private where appropriate.
- **Repository and Unit of Work:** Repositories expose queries for application use. `UnitOfWork.ExecuteInTransactionAsync` manages a `ReadCommitted` transaction and rolls it back on failure.
- **Ledger entry creation:** `LedgerEntryFactory` creates `WelcomeBonus`, `ExchangeDebit`, and `ExchangeCredit` entries consistently.
- **Domain events:** `ExchangeCompletedDomainEvent` is dispatched after the exchange transaction commits. Badge processing therefore does not roll back the hour transfer.
- **Concurrency:** `HelpRequests.RowVersion` provides an optimistic concurrency token. Balance-affecting operations also use `sp_getapplock` to serialize work per member within the transaction, addressing simultaneous acceptance attempts against the same requester's balance.
- **Transaction retries:** EF Core's retrying execution strategy is disabled for this path because blindly retrying an explicit transaction could duplicate a transfer. The operation uses one explicit transaction with rollback on failure.

## Known Limitations

- **No frontend:** The MVC website and back-office interface are not part of this repository. The audit schema includes `AuditSource.BackOffice` and an `Actor` string, but there is no MVC UI.
- **Development credentials:** Seed accounts and development JWT settings are for local testing only. They must be replaced or configured appropriately before deployment.
- **No refresh tokens or token revocation list:** Suspended members are blocked from mutating actions through a database status check. Existing tokens can still access read-only endpoints until they expire, where those endpoints permit access.
- **No pagination:** The request board and member directory currently return all matching rows.
- **CORS:** The permissive CORS policy is enabled only in Development. Non-development environments use HTTPS redirection and do not enable that policy.
- **Concurrent state changes:** Non-balance state changes rely on the row-version token and current-state checks. Balance-related operations additionally use `sp_getapplock`.
- **No background job queue:** Badge evaluation runs after an exchange completes as part of the request flow. There is no outbox or message queue.

## Author

LinkedIn: www.linkedin.com/in/mohamed-maherrr

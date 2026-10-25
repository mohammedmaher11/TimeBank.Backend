# Sprint reviews

The backend was delivered in four review slices. Each slice has a goal, the
evidence used in review, and the follow-ups that were carried into the next
slice. This is a delivery log, not a claim about a specific team calendar.

## Sprint 1 — Domain and database foundation

**Goal:** make the business rules executable without any web or database
dependency, and make the schema reproducible from migrations.

**Delivered**

- `TimeBank.Domain`: encapsulated entities (`Member`, `HelpRequest`, `Offer`,
  `LedgerEntry`, `Review`, `Badge`, `AuditLog`) with private setters and
  state-transition methods.
- `HoursPolicy` (BR-01/BR-03), `LedgerEntryFactory`, custom domain exceptions,
  and the `ExchangeCompletedDomainEvent`.
- `TimeBank.Infrastructure`: `TimeBankDbContext`, one Fluent API configuration
  per aggregate, and the `InitialCreate` migration.
- The `AddMemberStatementProcedure` migration installs
  `sp_GetMemberStatement`.
- `DataSeeder` reads `Seed/seed-data.json` with `System.Text.Json`, hashes
  passwords with BCrypt, and is idempotent.

**Review evidence**

- `dotnet ef database update` created the schema on real SQL Server.
- Seeded counts: 10 members, 10 categories, 34 skills, 25 member-skills,
  7 requests, 10 ledger entries, 3 badges.
- `Database_HasRequiredTablesConstraintsIndexesAndProcedure` later became the
  automated regression check for this slice.

**Retro / follow-up**

- The first schema attempt used a circular cascade path between
  `HelpRequests.AcceptedOfferId` and `Offers.HelpRequestId`; SQL Server rejected
  it. Fixed with `OnDelete(NoAction)` on the accepted-offer FK.
- Decision: no stored balance column. Every balance is derived from the ledger.

## Sprint 2 — Authentication, public API, and moderation

**Goal:** expose the domain through a JWT-protected REST API with correct
status codes and a consistent error shape.

**Delivered**

- `AuthService` with BCrypt, duplicate-email protection, welcome bonus, and JWT
  issuance.
- Public categories, requests board, request details, member directory, member
  profile, and `/api/stats` minimal API.
- `MeController`: profile, balance, statement, and skill replacement.
- `AdminController`: member list, suspend/reinstate, audit log; role policy
  `Moderator`.
- `ExceptionHandlingMiddleware` maps domain exceptions to RFC 7807
  `ProblemDetails` with a stable `code` extension.
- `RequestLoggingMiddleware` logs method, path, status, and elapsed ms.
- `AuditFilter` writes `AuditLogs` after successful moderator actions.
- Swagger with a Bearer security scheme.

**Review evidence**

- `Register_WithSkills_CreatesWelcomeBonusAndJwt` and
  `Register_WithUnknownSkill_RollsBackAndReturns400`.
- `Member_CannotCallModeratorEndpoints_AndSuspensionInvalidatesOldJwt`.
- `Moderator_CategoryCrud_IsAudited`.
- Live smoke test covers login, role checks, and the public read endpoints.

**Retro / follow-up**

- Suspension is checked against the database on every mutating action, not
  against the JWT. This is what makes an old token return 403 (BR-14).
- The audit write is deliberately best-effort: a failed audit insert is logged
  but does not turn a successful business action into a 500.

## Sprint 3 — Exchange workflow, balance locking, and concurrency

**Goal:** make offer → accept → complete → review safe under repeated and
simultaneous requests.

**Delivered**

- `ExchangeService` for create/offer/accept/complete/cancel/review.
- Explicit transaction + `sp_getapplock` per member in `UnitOfWork`, so two
  simultaneous accepts against the same balance cannot both pass.
- `HelpRequests.RowVersion` optimistic concurrency token.
- Atomic completion: status update plus two opposite ledger entries in one
  commit.
- Badge evaluation after commit through the domain-event dispatcher.
- Stored-procedure statement with opening balance and SQL window-function
  running balance.

**Review evidence**

- `FullExchangeFlow_TransfersHoursAndAwardsFirstHandOnce` (real SQL Server).
- `ConcurrentAccepts_SerializeTheRequesterBalance`: two parallel HTTP accepts,
  exactly one 200 and one 409.
- `Complete_WhenLedgerInsertFails_RollsBackStatusAndBothEntries`: a temporary
  CHECK constraint rejects the credit row; the request stays `InProgress` and
  no ledger rows remain. Removing the constraint and retrying succeeds.
- `AcceptOffer_WithInsufficientAvailableBalance_Returns409`.

**Retro / follow-up**

- EF Core's retry-on-failure execution strategy was removed. It conflicts with
  explicit transactions and `sp_getapplock`; a blind retry could duplicate a
  transfer.
- The concurrency test proved the lock is necessary: without it, both accepts
  could read the same available balance before either saved.

## Sprint 4 — Hardening, documentation, and hand-off

**Goal:** make the solution reproducible on a clean machine and easy to review.

**Delivered**

- 50 unit tests and 11 real-SQL integration tests.
- 60-check PowerShell smoke script for a running API.
- Postman collection covering the full workflow plus repeated complete 409,
  old-token-after-suspension 403, duplicate offer/review, role checks, and
  category audit.
- `database/` scripts (`sp_GetMemberStatement.sql`, `indexes.sql`,
  `reports.sql`), ERD, index rationale, verification matrix, and this log.
- Traditional `TimeBank.Backend.sln` for Visual Studio, `.gitignore`, and a
  README with exact commands.

**Review evidence**

- `dotnet build TimeBank.Backend.sln` — 0 warnings, 0 errors.
- `dotnet test` — unit and integration suites green.
- Postman/Newman run — see `docs/postman-run.md`.
- Database reset and seed check documented in the README.

**Retro / follow-up**

- The original brief includes an MVC website; this deliverable is intentionally
  **backend-only** as requested. The API, schema, and audit model are ready for
  the MVC project, but no `TimeBank.Web` project is included here.
- The dev JWT key lives only in `appsettings.Development.json` and is marked
  DEV-ONLY. Production must supply `Jwt__Key` through user secrets or
  environment variables.

# Verification matrix — BR-01 to BR-15

This file records **how each business rule is proven**, not just where it is
implemented. The evidence types are:

- **Unit** — xUnit + Moq test in `tests/TimeBank.Core.Tests`.
- **Integration** — real HTTP request against `WebApplicationFactory<Program>`
  using a unique real SQL Server database created by the real migrations.
- **HTTP** — a request in the Postman collection / smoke script.
- **SQL** — a documented query or constraint check.

## Test runs (latest)

| Suite | Command | Result |
|---|---|---|
| Unit tests | `dotnet test tests\TimeBank.Core.Tests\TimeBank.Core.Tests.csproj` | **50 passed, 0 failed** |
| Integration tests | `dotnet test tests\TimeBank.IntegrationTests\TimeBank.IntegrationTests.csproj` | **11 passed, 0 failed** |
| Live smoke test | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-test.ps1` | **60/60 checks passed** (see note below) |
| Postman / Newman | `newman run postman\TimeBank.postman_collection.json` | **37/37 requests, 37/37 assertions, 0 failed** (2026-10-25, Newman 6.2.3; see `docs/postman-run.md`) |

> The smoke script creates fresh members on each run. It is safe to run
> repeatedly because every email contains a timestamp; it does not truncate
> the database.

### Final hand-off snapshot

The development database was reset after the live runs and recreated by the
real migrations and seeder. The final `TimeBankDb` state is seed-only:

| Table | Rows |
|---|---|
| Members | 10 |
| Categories | 10 |
| Skills | 34 |
| MemberSkills | 25 |
| HelpRequests | 7 |
| LedgerEntries | 10 |
| Offers, Reviews, MemberBadges, AuditLogs | 0 |
| `__EFMigrationsHistory` | 2 migrations |

`dotnet build TimeBank.Backend.sln` finished with **0 warnings, 0 errors**.
The source-only ZIP was extracted to a clean folder, restored, built with the
same result, and its test suite passed **50/50 unit + 11/11 integration**.

## Matrix

| Rule | What is proven | Evidence |
|---|---|---|
| **BR-01** Every hour is equal | The schema has no rate, price, or category multiplier column. Every ledger entry stores the same `Hours` unit; reports sum actual hours without weighting. | `docs/ERD.md` (no rate column), `database/reports.sql` report 1, `LedgerEntry`/`LedgerEntryFactory` |
| **BR-02** Welcome hours | A new member receives exactly one `WelcomeBonus` ledger entry of `+2.0` inside the registration transaction. | Unit: registration is covered indirectly by `AuthService` flow tests; Integration: `Register_WithSkills_CreatesWelcomeBonusAndJwt` asserts `balance = 2`, `locked = 0`, `available = 2`; SQL: `reports.sql` report 6 |
| **BR-03** Half-hour units, 0.5–8 hours, non-zero ledger | Domain `HoursPolicy` rejects non-half-hour, below-minimum, above-maximum, and zero values. Database CHECK constraints enforce the same at the boundary. | Unit: `HoursPolicy_AcceptsHalfHourStepsInRange`, `HoursPolicy_Rejects...`, `LedgerEntry_RejectsZeroHours`, `HelpRequest_Complete_AllowsHalfHourActualHoursUpToAccepted`; Integration: `Database_HasRequiredTablesConstraintsIndexesAndProcedure`; HTTP: Postman request `07 - Error: invalid estimated hours -> 400`; smoke invalid request check |
| **BR-04** Ledger is the truth and append-only | Balance is `SUM(Hours)`; `LedgerEntryRepository` exposes only `Add`/`AddRange` and read queries — no update or delete. | Unit: `GetBalance_ReturnsAvailableAsBalanceMinusLocked`, `GetBalance_WithNoLedgerEntries_ReturnsZero`; Integration: balances after exchange; SQL: `reports.sql` report 5; code: `LedgerEntryRepository.cs` |
| **BR-05** Available = Balance − Locked | Locked hours are the `AcceptedHours` of the member's `InProgress` requests. | Unit: `GetBalance_ReturnsAvailableAsBalanceMinusLocked`; Integration: `FullExchangeFlow_TransfersHoursAndAwardsFirstHandOnce` (after accept: 2 balance, 1.5 locked, 0.5 available); HTTP: Postman balance requests |
| **BR-06** Offer conditions | Own request → 403; duplicate → 409; no skill in category → 400; request no longer Open → 409. | Unit: `SendOffer_OnOwnRequest_ThrowsForbiddenException`, `SendOffer_SecondOfferFromSameMember_ThrowsConflictException`, `SendOffer_WithoutSkillInCategory_ThrowsBusinessRuleException`, `SendOffer_WhenRequestIsNoLongerOpen_ThrowsConflictException`; Integration: full flow duplicate offer 409 and owner-only offers 403; HTTP: Postman duplicate offer 409; smoke own-offer/duplicate checks |
| **BR-07** Accept one offer only | Insufficient available balance → 409 `INSUFFICIENT_HOURS`; accepting rejects all other pending offers, moves to `InProgress`, and locks hours; a second accept → 409. | Unit: `AcceptOffer_WithInsufficientAvailableBalance_ThrowsInsufficientHoursException`, `AcceptOffer_RejectsAllOtherPendingOffersAndMovesRequestToInProgress`, `AcceptOffer_OnRequestAlreadyInProgress_ThrowsConflictException`; Integration: `AcceptOffer_WithInsufficientAvailableBalance_Returns409`, `ConcurrentAccepts_SerializeTheRequesterBalance`, full flow repeated accept 409; HTTP: Postman repeated accept 409 |
| **BR-08** Complete atomically | Both opposite ledger entries and the status change are committed once; actual hours must be 0.5..accepted; a repeat → 409 with no second transfer. | Unit: `Complete_CreatesOppositeLedgerEntriesAndCommitsOnce`, `Complete_WhenRequestIsAlreadyCompleted_ThrowsConflictAndAddsNoSecondEntries`, `Complete_WithActualHoursAboveAccepted_ThrowsBusinessRuleException`; Integration: `Complete_WhenLedgerInsertFails_RollsBackStatusAndBothEntries` forces a real SQL CHECK failure and asserts status stayed `InProgress` and zero ledger rows, then the retry succeeds; HTTP: Postman repeated complete 409 |
| **BR-09** Cancel rules | Only Open/InProgress can be cancelled; Completed/Cancelled are final; cancelling InProgress releases the lock because the status leaves InProgress. | Unit: `Cancel_OnCompletedRequest_ThrowsConflictException`, `Cancel_InProgressRequest_ReleasesLockByLeavingInProgressState`; HTTP: Postman `Cancel completed request -> 409` |
| **BR-10** Needs Love | Computed from `Status == Open` and `CreatedAt` older than 14 days; never stored. | Unit: `HelpRequest_NeedsLove_IsComputedAfterFourteenDays`; Integration: request projection is covered by the list/detail assertions; code: `HelpRequest.NeedsLove` and `HelpRequestRepository.SummaryProjection` |
| **BR-11** Reviews | Only the two exchange parties, only after Completed, one review per person per exchange; rating 1–5. | Unit: `AddReview_SecondReviewBySamePerson_ThrowsConflictException`, `AddReview_BeforeExchangeIsCompleted_ThrowsConflictException`, `AddReview_ByUnrelatedMember_ThrowsForbiddenException`, `AddReview_ByRequester_CreatesReviewForHelper`, `AddReview_ByHelper_CreatesReviewForRequester`; Integration: full flow both reviews + duplicate 409; HTTP: Postman duplicate review 409 |
| **BR-12** Automatic badges | `ExchangeCompleted` raises a domain event; `BadgeService` awards First Hand, Ten Hours Hero, and Good Neighbor once each. | Unit: `Evaluate_FirstCompletedExchange_AwardsFirstHand`, `Evaluate_WhenFirstHandWasAlreadyAwarded_DoesNotAwardItTwice`, `Evaluate_WhenHelperReachedTenHours_AwardsTenHoursHero`, `Evaluate_WhenHelperHelpedFiveDifferentMembers_AwardsGoodNeighbor`; Integration: full flow asserts exactly one `FIRST_HAND` badge after completion |
| **BR-13** Roles | Only Moderator can manage categories or suspend members; Member receives 403. | Integration: `Member_CannotCallModeratorEndpoints_AndSuspensionInvalidatesOldJwt`, `Moderator_CategoryCrud_IsAudited`; HTTP: Postman member-calls-admin 403 and moderator category CRUD; JWT role claim in `JwtTokenService` |
| **BR-14** Suspension | Suspended members cannot log in, create requests, or send offers — even with a JWT issued before suspension. The check reads the current database state. | Unit: `SendOffer_WhenMemberIsSuspended_ThrowsForbiddenExceptionBeforeLoadingRequest`, `SetSkills_WhenMemberIsSuspended_ThrowsForbiddenException`, `Member_SuspendAndReinstate_AreIdempotentStateChecks`; Integration: `Member_CannotCallModeratorEndpoints_AndSuspensionInvalidatesOldJwt` (old token → 403); HTTP: Postman suspend → old token request 403 + suspended login 403; smoke same |
| **BR-15** Audit every admin action | `AuditFilter` writes `AuditLogs` after successful moderator actions, with actor = JWT member id and source = Api. | Integration: `Moderator_CategoryCrud_IsAudited` asserts Create/Update/Delete entries; HTTP: Postman audit-log request after category CRUD and suspension; SQL below |

## SQL checks

Run these against the real database after a smoke/Postman flow:

```sql
-- BR-04: ledger sum is the balance; no stored balance column exists.
SELECT m.[Id], m.[FullName], ISNULL(SUM(l.[Hours]), 0) AS [LedgerBalance]
FROM [dbo].[Members] AS m
LEFT JOIN [dbo].[LedgerEntries] AS l ON l.[MemberId] = m.[Id]
GROUP BY m.[Id], m.[FullName]
ORDER BY m.[Id];

-- BR-08: every completed exchange has exactly one debit and one credit that cancel out.
SELECT r.[Id],
       SUM(CASE WHEN l.[Type] = 2 THEN l.[Hours] ELSE 0 END) AS [Debit],
       SUM(CASE WHEN l.[Type] = 3 THEN l.[Hours] ELSE 0 END) AS [Credit],
       SUM(l.[Hours]) AS [Net]
FROM [dbo].[HelpRequests] AS r
JOIN [dbo].[LedgerEntries] AS l ON l.[ReferenceId] = r.[Id] AND l.[Type] IN (2, 3)
WHERE r.[Status] = 3
GROUP BY r.[Id]
HAVING SUM(l.[Hours]) <> 0;

-- BR-15: moderator actions are recorded.
SELECT TOP (20) [Actor], [Source], [Action], [Target], [Details], [CreatedAt]
FROM [dbo].[AuditLogs]
ORDER BY [CreatedAt] DESC, [Id] DESC;

-- BR-02: every member has exactly one 2-hour welcome bonus.
SELECT m.[Id], COUNT(l.[Id]) AS [WelcomeRows], ISNULL(SUM(l.[Hours]), 0) AS [WelcomeHours]
FROM [dbo].[Members] AS m
LEFT JOIN [dbo].[LedgerEntries] AS l ON l.[MemberId] = m.[Id] AND l.[Type] = 1
GROUP BY m.[Id]
HAVING COUNT(l.[Id]) <> 1 OR ISNULL(SUM(l.[Hours]), 0) <> 2;
```

The last two queries should return **no rows** for a healthy database. The
first query is the documented reconciliation between the ledger and the
`/api/me/balance` response.

## What is deliberately not proven by a unit test

- A Moq test proves the application called `SaveChangesAsync` once; it does
  **not** prove SQL Server rolled back a failed transaction. That is why the
  integration test `Complete_WhenLedgerInsertFails_RollsBackStatusAndBothEntries`
  runs against a real database and a real failing constraint.
- The simultaneous-accept case is proven by the integration test
  `ConcurrentAccepts_SerializeTheRequesterBalance`, which fires two real HTTP
  accepts in parallel and asserts exactly one succeeds. The implementation
  serializes balance-affecting work with `sp_getapplock` inside the explicit
  transaction; see `UnitOfWork.cs`.
- The MVC back-office is **not part of this backend-only deliverable**. The
  `AuditSource.BackOffice` enum and the `Actor` string column are ready for it,
  but no MVC project is included here.

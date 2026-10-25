# Indexing decisions

All indexes below are created by the EF Core migration
`20261025013240_InitialCreate` and are repeated idempotently in
`database/indexes.sql`. The goal is to support the actual query shapes used by
the API, not to index every column.

## Summary

| Index | Columns (in order) | Type | Main query it serves |
|---|---|---|---|
| `IX_Members_Email` | `Email` | Unique | Login, registration duplicate check |
| `IX_Members_District` | `District` | Non-unique | Members directory / district filters |
| `IX_Categories_Name` | `Name` | Unique | Category CRUD duplicate check |
| `IX_Skills_CategoryId_Name` | `CategoryId, Name` | Unique | Skills grouped by category; prevents duplicate skill names inside a category |
| `IX_MemberSkills_SkillId` | `SkillId` | Non-unique | "Who has this skill?" and FK lookups |
| `IX_HelpRequests_Status_CategoryId_District` | `Status, CategoryId, District` | Non-unique | Requests board filter |
| `IX_HelpRequests_CreatedAt` | `CreatedAt` | Non-unique | Default newest-first board ordering |
| `IX_HelpRequests_RequesterId` | `RequesterId` | Non-unique | "My requests", locked-hours calculation |
| `IX_HelpRequests_AcceptedHelperId` | `AcceptedHelperId` | Non-unique | Helper history, badge counts, top helpers |
| `IX_HelpRequests_AcceptedOfferId` | `AcceptedOfferId` | Non-unique | FK lookup for the accepted offer |
| `IX_HelpRequests_CategoryId` | `CategoryId` | Non-unique | FK lookup / category filter |
| `IX_Offers_HelpRequestId_HelperId` | `HelpRequestId, HelperId` | Unique | BR-06 one offer per member per request; duplicate check |
| `IX_Offers_HelpRequestId_Status` | `HelpRequestId, Status` | Non-unique | Owner's offer list; rejecting other pending offers |
| `IX_Offers_HelperId` | `HelperId` | Non-unique | A helper's offer history |
| `IX_LedgerEntries_MemberId_CreatedAt` | `MemberId, CreatedAt` | Non-unique | Balance `SUM`, statement period and running total |
| `IX_LedgerEntries_MemberId_Type` | `MemberId, Type` | Non-unique | Exchange-credit totals for badges |
| `IX_LedgerEntries_ReferenceId` | `ReferenceId` | Non-unique | Exchange pair lookups and integrity reports |
| `IX_Reviews_HelpRequestId_ReviewerId` | `HelpRequestId, ReviewerId` | Unique | BR-11 one review per person per exchange |
| `IX_Reviews_RevieweeId` | `RevieweeId` | Non-unique | Rating average and profile reviews |
| `IX_Reviews_ReviewerId` | `ReviewerId` | Non-unique | "Reviews I wrote" / FK lookup |
| `IX_Badges_Code` | `Code` | Unique | Badge lookup by stable code |
| `IX_MemberBadges_BadgeId` | `BadgeId` | Non-unique | Members who hold a badge |
| `IX_AuditLogs_CreatedAt` | `CreatedAt` | Non-unique | Moderator log, newest first |
| `IX_AuditLogs_Actor` | `Actor` | Non-unique | Filtering the log by moderator |

## Why these columns, in this order

### `IX_HelpRequests_Status_CategoryId_District`

The public board has three optional filters and a default view of open
requests. `Status` is first because the common board query is "show me what is
still open"; the remaining columns let SQL Server seek by category and
district after that. This is a **working index for the current query shape**,
not a claim that it is optimal for every combination. If category-only
filtering becomes the dominant workload, measure first and consider a second
index starting with `CategoryId` (or a filtered index on `Status = 1`).

### `IX_HelpRequests_CreatedAt`

The list endpoint orders by `CreatedAt DESC`. Without this index SQL Server
would sort the filtered set. It also supports the "Needs Love" candidate scan
(open requests older than 14 days).

### `IX_LedgerEntries_MemberId_CreatedAt`

The balance is `SUM(Hours)` for one member; the statement is a date range for
one member ordered by `CreatedAt, Id`. `MemberId` is the equality predicate, so
it comes first; `CreatedAt` makes the range and ordering a seek rather than a
scan. The stored procedure still computes the running total with a window
function, but the index supplies the rows in the required order.

### `IX_LedgerEntries_MemberId_Type`

Badge evaluation asks for the sum of `ExchangeCredit` hours for one member.
`(MemberId, Type)` narrows that to the credit rows before the aggregate.

### `IX_LedgerEntries_ReferenceId`

Exchange completion writes exactly two rows with the same `ReferenceId` (the
help request id). This index makes the pair lookup and the ledger-integrity
report cheap, and it is deliberately **not** a foreign key because the ledger
is an append-only historical record.

### `IX_Offers_HelpRequestId_HelperId` (unique)

This is both a query index and a database-level guard for BR-06. The domain
checks for a duplicate offer, and the unique index closes the race where two
simultaneous requests both pass the in-memory check. `HelpRequestId` first
matches the owner's offer list; `HelperId` completes the uniqueness key.

### `IX_Reviews_HelpRequestId_ReviewerId` (unique)

Same reasoning for BR-11: the application checks "already reviewed", and the
unique index is the final guard. It also supports loading a request's reviews
by reviewer.

### `IX_Members_Email` (unique)

Login and registration both look up by normalized email. Uniqueness is a
business rule (one account per email), so it belongs in the database as well as
in `AuthService`.

### Unique indexes that encode business rules

- `IX_Categories_Name` — category names are unique.
- `IX_Skills_CategoryId_Name` — a skill name is unique within its category.
- `IX_Badges_Code` — badge codes are stable identifiers used by `BadgeService`.
- `IX_Offers_HelpRequestId_HelperId` — one offer per member per request.
- `IX_Reviews_HelpRequestId_ReviewerId` — one review per reviewer per exchange.
- Primary keys on `MemberSkills(MemberId, SkillId)` and
  `MemberBadges(MemberId, BadgeId)` — one row per pair.

## Deliberately not indexed

| Candidate | Why not |
|---|---|
| `NeedsLove` | It is computed from `Status` and `CreatedAt` (BR-10), not stored. Indexing the two inputs is enough. |
| `HelpRequests.ActualHours` | Reporting aggregates scan completed requests; a filtered index could be added after measuring real data volumes. |
| `LedgerEntries.CreatedAt` alone | Every ledger query is scoped to one member first; the composite `(MemberId, CreatedAt)` is the useful shape. |
| `AuditLogs.Details` | Free text is not filtered in the current API. |

## Verifying index usage

Use SQL Server Management Studio or `sqlcmd`:

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

SELECT SUM([Hours]) FROM [dbo].[LedgerEntries] WHERE [MemberId] = 2;

SELECT [Id], [CreatedAt], [Description], [Type], [Hours]
FROM [dbo].[LedgerEntries]
WHERE [MemberId] = 2 AND [CreatedAt] >= '2026-10-01' AND [CreatedAt] < '2026-11-01'
ORDER BY [CreatedAt], [Id];
```

The integration test `Database_HasRequiredTablesConstraintsIndexesAndProcedure`
also asserts that the expected indexes and the stored procedure exist in a
freshly migrated database.

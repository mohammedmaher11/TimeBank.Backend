# Entity–Relationship Diagram

The schema is created by EF Core migrations (`InitialCreate` and
`AddMemberStatementProcedure`). It contains the eleven tables required by the
project brief plus the stored procedure used by the account statement.

```mermaid
erDiagram
    MEMBERS ||--o{ MEMBER_SKILLS : "has skills"
    SKILLS ||--o{ MEMBER_SKILLS : "selected by"
    CATEGORIES ||--o{ SKILLS : "contains"
    CATEGORIES ||--o{ HELP_REQUESTS : "classifies"
    MEMBERS ||--o{ HELP_REQUESTS : "requests"
    HELP_REQUESTS ||--o{ OFFERS : "receives"
    MEMBERS ||--o{ OFFERS : "makes"
    HELP_REQUESTS |o--o| OFFERS : "accepts one"
    HELP_REQUESTS ||--o{ REVIEWS : "is reviewed in"
    MEMBERS ||--o{ REVIEWS : "writes"
    MEMBERS ||--o{ REVIEWS : "receives"
    MEMBERS ||--o{ LEDGER_ENTRIES : "owns"
    MEMBERS ||--o{ MEMBER_BADGES : "earns"
    BADGES ||--o{ MEMBER_BADGES : "awarded as"

    MEMBERS {
        int Id PK
        nvarchar(120) FullName
        nvarchar(200) Email UK
        nvarchar(200) PasswordHash
        nvarchar(100) District
        nvarchar(1000) Bio
        nvarchar(500) PhotoPath
        int Role "Member=1, Moderator=2"
        bit IsSuspended
        datetime2 CreatedAt
    }

    CATEGORIES {
        int Id PK
        nvarchar(100) Name UK
        nvarchar(500) Description
        datetime2 CreatedAt
    }

    SKILLS {
        int Id PK
        nvarchar(100) Name
        int CategoryId FK
    }

    MEMBER_SKILLS {
        int MemberId PK,FK
        int SkillId PK,FK
        int Level "Beginner=1, Intermediate=2, Expert=3"
    }

    HELP_REQUESTS {
        int Id PK
        int RequesterId FK
        nvarchar(150) Title
        nvarchar(2000) Description
        int CategoryId FK
        nvarchar(100) District
        decimal(5,2) EstimatedHours
        date NeededBy
        int Status "Open=1, InProgress=2, Completed=3, Cancelled=4"
        int AcceptedOfferId FK
        int AcceptedHelperId FK
        decimal(5,2) AcceptedHours
        decimal(5,2) ActualHours
        datetime2 CreatedAt
        datetime2 CompletedAt
        datetime2 CancelledAt
        rowversion RowVersion
    }

    OFFERS {
        int Id PK
        int HelpRequestId FK
        int HelperId FK
        nvarchar(1000) Message
        decimal(5,2) Hours
        int Status "Pending=1, Accepted=2, Rejected=3"
        datetime2 CreatedAt
        datetime2 RespondedAt
    }

    LEDGER_ENTRIES {
        int Id PK
        int MemberId FK
        decimal(5,2) Hours "positive or negative, never zero"
        int Type "WelcomeBonus=1, ExchangeDebit=2, ExchangeCredit=3, GiftSent=4, GiftReceived=5"
        nvarchar(300) Description
        int ReferenceId "HelpRequest id for exchanges"
        datetime2 CreatedAt
    }

    REVIEWS {
        int Id PK
        int HelpRequestId FK
        int ReviewerId FK
        int RevieweeId FK
        int Rating "1..5"
        nvarchar(300) Comment
        datetime2 CreatedAt
    }

    BADGES {
        int Id PK
        nvarchar(50) Code UK
        nvarchar(100) Name
        nvarchar(20) Icon
        nvarchar(300) Description
    }

    MEMBER_BADGES {
        int MemberId PK,FK
        int BadgeId PK,FK
        datetime2 EarnedAt
    }

    AUDIT_LOGS {
        int Id PK
        nvarchar(100) Actor "member id or BackOffice"
        int Source "Api=1, BackOffice=2"
        nvarchar(200) Action
        nvarchar(200) Target
        nvarchar(1000) Details
        datetime2 CreatedAt
    }
```

## Relationship and delete behaviour

| Relationship | Cardinality | On delete | Why |
|---|---|---|---|
| Member → MemberSkill | 1 : 0..n | Cascade | A member's skill links have no meaning without the member. |
| Skill → MemberSkill | 1 : 0..n | Restrict | Skills are fixed seed data; deleting a skill must not silently remove members' claims. |
| Category → Skill | 1 : 0..n | Cascade | A category owns its skills. |
| Category → HelpRequest | 1 : 0..n | Restrict | A category with requests cannot be deleted (also checked in `CategoryService`). |
| Member → HelpRequest | 1 : 0..n | Restrict | History must survive; members are never hard-deleted. |
| HelpRequest → Offer | 1 : 0..n | Cascade | Offers are children of the request. |
| Member → Offer | 1 : 0..n | Restrict | Keep the helper's history. |
| HelpRequest → AcceptedOffer | 0..1 : 0..1 | NoAction | Circular FK (request points at offer, offer points at request); SQL Server needs `NO ACTION` to avoid multiple cascade paths. |
| HelpRequest → Review | 1 : 0..n | Cascade | Reviews belong to the exchange. |
| Member → Review (reviewer / reviewee) | 1 : 0..n | Restrict | Reviews are historical evidence; members are not hard-deleted. |
| Member → LedgerEntry | 1 : 0..n | Restrict | The ledger is append-only and must never be cascaded away. |
| Badge → MemberBadge | 1 : 0..n | Cascade | Award rows are meaningless without the badge definition. |
| Member → MemberBadge | 1 : 0..n | Cascade | Award rows belong to the member. |
| AuditLog | standalone | n/a | `Actor` is deliberately a string: it can be a member id or the fixed `BackOffice` value. |

## Important schema notes

- `LedgerEntries.ReferenceId` is intentionally **not** a foreign key. The
  ledger is an append-only historical record; it must remain readable even if
  a request is later removed by an administrative maintenance job. The
  application treats it as the help-request id for exchange entries.
- `HelpRequests.RowVersion` is a SQL Server `rowversion` column used as an
  optimistic concurrency token. EF Core includes it in `UPDATE ... WHERE`
  predicates, so two simultaneous state changes cannot both succeed.
- The balance is **not** stored. It is always `SUM(LedgerEntries.Hours)` for a
  member; locked hours are the sum of `AcceptedHours` for that member's
  `InProgress` requests.
- Check constraints enforce the half-hour rule, non-zero ledger entries,
  request/offer hour ranges, and review ratings at the database boundary as a
  second line of defence behind the domain rules.

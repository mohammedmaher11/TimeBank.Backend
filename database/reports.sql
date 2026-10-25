/*
    TimeBank
    Reporting queries.

    Enum values stored in the database:
        RequestStatus:    Open = 1, InProgress = 2, Completed = 3, Cancelled = 4
        LedgerEntryType:  WelcomeBonus = 1, ExchangeDebit = 2, ExchangeCredit = 3,
                          GiftSent = 4, GiftReceived = 5

    Run all reports:
        sqlcmd -S "localhost\SQLEXPRESS" -C -E -d TimeBankDb -i database\reports.sql
*/
SET NOCOUNT ON;
GO

/* -------------------------------------------------------------------------
   1) Hours exchanged per category in the current calendar month.
      Uses the completed request as the unit of exchange, so the report is
      independent of whether a ledger row was written by an older app build.
   ------------------------------------------------------------------------- */
PRINT '1) Hours exchanged per category this month';
SELECT
    c.[Name]                         AS [Category],
    COUNT(r.[Id])                    AS [CompletedExchanges],
    ISNULL(SUM(r.[ActualHours]), 0)  AS [HoursExchanged]
FROM [dbo].[Categories] AS c
LEFT JOIN [dbo].[HelpRequests] AS r
    ON r.[CategoryId] = c.[Id]
   AND r.[Status] = 3 -- Completed
   AND r.[CompletedAt] >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1)
GROUP BY c.[Name]
ORDER BY [HoursExchanged] DESC, [Category];
GO

/* -------------------------------------------------------------------------
   2) Members who have never completed a help as the accepted helper.
      Useful for onboarding / community outreach.
   ------------------------------------------------------------------------- */
PRINT '2) Members who have never helped anyone';
SELECT
    m.[Id],
    m.[FullName],
    m.[District],
    m.[CreatedAt]
FROM [dbo].[Members] AS m
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[HelpRequests] AS r
    WHERE r.[AcceptedHelperId] = m.[Id]
      AND r.[Status] = 3 -- Completed
)
ORDER BY m.[CreatedAt], m.[Id];
GO

/* -------------------------------------------------------------------------
   3) Busiest districts by open requests.
   ------------------------------------------------------------------------- */
PRINT '3) Busiest districts by open requests';
SELECT
    r.[District],
    COUNT(*) AS [OpenRequests],
    ISNULL(SUM(r.[EstimatedHours]), 0) AS [EstimatedHours]
FROM [dbo].[HelpRequests] AS r
WHERE r.[Status] = 1 -- Open
GROUP BY r.[District]
ORDER BY [OpenRequests] DESC, [District];
GO

/* -------------------------------------------------------------------------
   4) Top helpers this month (completed exchanges, by actual hours given).
   ------------------------------------------------------------------------- */
PRINT '4) Top helpers this month';
SELECT TOP (10)
    m.[Id]                          AS [MemberId],
    m.[FullName],
    COUNT(*)                        AS [CompletedExchanges],
    SUM(r.[ActualHours])            AS [HoursGiven]
FROM [dbo].[HelpRequests] AS r
JOIN [dbo].[Members] AS m ON m.[Id] = r.[AcceptedHelperId]
WHERE r.[Status] = 3 -- Completed
  AND r.[CompletedAt] >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1)
GROUP BY m.[Id], m.[FullName]
ORDER BY [HoursGiven] DESC, [CompletedExchanges] DESC, m.[Id];
GO

/* -------------------------------------------------------------------------
   5) Ledger integrity check.
      For every completed request the requester's ExchangeDebit and the
      helper's ExchangeCredit must cancel out exactly. The query returns
      only broken exchanges (expected: zero rows).
   ------------------------------------------------------------------------- */
PRINT '5) Broken exchange pairs in the ledger (expected: no rows)';
SELECT
    r.[Id] AS [HelpRequestId],
    SUM(CASE WHEN l.[Type] = 2 THEN l.[Hours] ELSE 0 END) AS [DebitHours],
    SUM(CASE WHEN l.[Type] = 3 THEN l.[Hours] ELSE 0 END) AS [CreditHours],
    SUM(l.[Hours]) AS [NetHours]
FROM [dbo].[HelpRequests] AS r
JOIN [dbo].[LedgerEntries] AS l
    ON l.[ReferenceId] = r.[Id]
   AND l.[Type] IN (2, 3)
WHERE r.[Status] = 3 -- Completed
GROUP BY r.[Id]
HAVING SUM(l.[Hours]) <> 0
    OR SUM(CASE WHEN l.[Type] = 2 THEN 1 ELSE 0 END) <> 1
    OR SUM(CASE WHEN l.[Type] = 3 THEN 1 ELSE 0 END) <> 1;
GO

/* -------------------------------------------------------------------------
   6) Welcome-bonus audit: every member should have exactly one 2-hour
      WelcomeBonus entry (BR-02). The query returns members that do not.
   ------------------------------------------------------------------------- */
PRINT '6) Members missing exactly one 2-hour welcome bonus (expected: no rows)';
SELECT
    m.[Id],
    m.[FullName],
    COUNT(l.[Id]) AS [WelcomeBonusRows],
    ISNULL(SUM(l.[Hours]), 0) AS [WelcomeBonusHours]
FROM [dbo].[Members] AS m
LEFT JOIN [dbo].[LedgerEntries] AS l
    ON l.[MemberId] = m.[Id]
   AND l.[Type] = 1 -- WelcomeBonus
GROUP BY m.[Id], m.[FullName]
HAVING COUNT(l.[Id]) <> 1 OR ISNULL(SUM(l.[Hours]), 0) <> 2;
GO

/* -------------------------------------------------------------------------
   7) Example: a member statement through the stored procedure.
      Change @MemberId to inspect another member.
   ------------------------------------------------------------------------- */
PRINT '7) Example member statement (MemberId = 2)';
DECLARE @MemberId INT = 2;
EXEC [dbo].[sp_GetMemberStatement]
    @MemberId = @MemberId,
    @From = '2026-10-01T00:00:00',
    @To   = '2026-11-01T00:00:00';
GO

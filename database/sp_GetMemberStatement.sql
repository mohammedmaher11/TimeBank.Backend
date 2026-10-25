/*
    TimeBank
    Stored procedure: sp_GetMemberStatement

    Returns two result sets:
      1) the opening balance immediately before @From
      2) ledger entries in [@From, @To) with a running balance produced by
         SUM(Hours) OVER (ORDER BY CreatedAt, Id)

    This is the same definition that is installed by the EF Core migration
    AddMemberStatementProcedure. It is kept here as a standalone database
    script so it can be reviewed and re-applied without running migrations.

    Example:
        EXEC dbo.sp_GetMemberStatement
            @MemberId = 2,
            @From = '2026-10-01T00:00:00',
            @To   = '2026-11-01T00:00:00';
*/
CREATE OR ALTER PROCEDURE [dbo].[sp_GetMemberStatement]
    @MemberId INT,
    @From DATETIME2,
    @To DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    IF @From IS NULL SET @From = '1900-01-01T00:00:00';
    IF @To IS NULL SET @To = DATEADD(DAY, 1, SYSUTCDATETIME());

    DECLARE @OpeningBalance DECIMAL(18,2);

    SELECT @OpeningBalance = ISNULL(SUM([Hours]), 0)
    FROM [dbo].[LedgerEntries]
    WHERE [MemberId] = @MemberId
      AND [CreatedAt] < @From;

    -- Result set 1: opening balance before the requested period.
    SELECT @OpeningBalance AS [OpeningBalance];

    -- Result set 2: statement rows with the running balance (window function).
    SELECT
        [Id],
        [CreatedAt],
        [Description],
        [Type],
        [Hours],
        @OpeningBalance + SUM([Hours]) OVER (
            ORDER BY [CreatedAt], [Id]
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS [RunningBalance]
    FROM [dbo].[LedgerEntries]
    WHERE [MemberId] = @MemberId
      AND [CreatedAt] >= @From
      AND [CreatedAt] < @To
    ORDER BY [CreatedAt], [Id];
END;
GO

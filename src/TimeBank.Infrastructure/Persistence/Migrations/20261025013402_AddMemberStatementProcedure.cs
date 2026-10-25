using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeBank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberStatementProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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

                    -- Opening balance before the requested period.
                    SELECT @OpeningBalance AS [OpeningBalance];

                    -- Statement rows with the running balance (SQL window function).
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
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS [dbo].[sp_GetMemberStatement];");
        }
    }
}

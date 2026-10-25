using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.DTOs.Balances;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;

namespace TimeBank.Infrastructure.Persistence.Repositories;

/// <summary>
/// BR-04: append-only repository. There is deliberately no Update or Delete method.
/// </summary>
public sealed class LedgerEntryRepository : ILedgerEntryRepository
{
    private readonly TimeBankDbContext _context;

    public LedgerEntryRepository(TimeBankDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default) =>
        _context.LedgerEntries.AddAsync(entry, cancellationToken).AsTask();

    public Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default) =>
        _context.LedgerEntries.AddRangeAsync(entries, cancellationToken);

    public Task<decimal> GetBalanceAsync(int memberId, CancellationToken cancellationToken = default) =>
        _context.LedgerEntries.AsNoTracking()
            .Where(l => l.MemberId == memberId)
            .SumAsync(l => (decimal?)l.Hours, cancellationToken)
            .ContinueWith(t => t.Result ?? 0m, cancellationToken);

    public Task<decimal> GetExchangeCreditsAsync(int memberId, CancellationToken cancellationToken = default) =>
        _context.LedgerEntries.AsNoTracking()
            .Where(l => l.MemberId == memberId && l.Type == LedgerEntryType.ExchangeCredit)
            .SumAsync(l => (decimal?)l.Hours, cancellationToken)
            .ContinueWith(t => t.Result ?? 0m, cancellationToken);

    public Task<bool> AnyAsync(int memberId, CancellationToken cancellationToken = default) =>
        _context.LedgerEntries.AnyAsync(l => l.MemberId == memberId, cancellationToken);

    /// <summary>
    /// Calls the required stored procedure from EF Core's underlying connection.
    /// The SP returns the opening balance as its first result set and the statement
    /// rows with a SQL Server window-function running balance as its second.
    /// </summary>
    public async Task<StatementDto> GetStatementAsync(
        int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "sp_GetMemberStatement";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 30;

            AddParameter(command, "@MemberId", SqlDbType.Int, memberId);
            AddParameter(command, "@From", SqlDbType.DateTime2, from.ToDateTime(TimeOnly.MinValue));
            AddParameter(command, "@To", SqlDbType.DateTime2, to.AddDays(1).ToDateTime(TimeOnly.MinValue));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var opening = 0m;
            if (await reader.ReadAsync(cancellationToken))
                opening = reader.GetDecimal(0);

            var entries = new List<StatementEntryDto>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    // Result-set order: Id, CreatedAt, Description, Type, Hours, RunningBalance.
                    var type = (LedgerEntryType)reader.GetInt32(3);
                    entries.Add(new StatementEntryDto(
                        reader.GetInt32(0),
                        reader.GetDateTime(1),
                        reader.GetString(2),
                        type.ToString(),
                        reader.GetDecimal(4),
                        reader.GetDecimal(5)));
                }
            }

            var closing = entries.Count > 0 ? entries[^1].RunningBalance : opening;
            return new StatementDto(memberId, from, to, opening, entries, closing);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, SqlDbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = ToDbType(type);
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static DbType ToDbType(SqlDbType type) => type switch
    {
        SqlDbType.Int => DbType.Int32,
        SqlDbType.DateTime2 => DbType.DateTime2,
        _ => DbType.Object
    };
}

namespace TimeBank.Application.DTOs.Balances;

public sealed record BalanceDto(decimal Balance, decimal Locked, decimal Available);

public sealed record StatementEntryDto(
    int Id,
    DateTime CreatedAt,
    string Description,
    string Type,
    decimal Hours,
    decimal RunningBalance);

public sealed record StatementDto(
    int MemberId,
    DateOnly From,
    DateOnly To,
    decimal OpeningBalance,
    IReadOnlyList<StatementEntryDto> Entries,
    decimal ClosingBalance);

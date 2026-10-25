namespace TimeBank.Application.DTOs.Stats;

public sealed record TopHelperDto(int MemberId, string FullName, decimal Hours);

public sealed record CategoryDemandDto(string Category, int RequestCount);

public sealed record StatsDto(
    int Members,
    int OpenRequests,
    int CompletedExchanges,
    decimal HoursExchanged,
    IReadOnlyList<TopHelperDto> TopHelpersThisMonth,
    IReadOnlyList<CategoryDemandDto> CategoryDemand);

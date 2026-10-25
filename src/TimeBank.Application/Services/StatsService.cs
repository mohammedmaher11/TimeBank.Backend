using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Stats;

namespace TimeBank.Application.Services;

public sealed class StatsService : IStatsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public StatsService(IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<StatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var members = await _unitOfWork.Members.CountAsync(cancellationToken);
        var openRequests = await _unitOfWork.HelpRequests.CountOpenAsync(cancellationToken);
        var completed = await _unitOfWork.HelpRequests.CountCompletedAsync(cancellationToken);
        var hours = await _unitOfWork.HelpRequests.GetTotalCompletedHoursAsync(cancellationToken);
        var topHelpers = await _unitOfWork.HelpRequests.GetTopHelpersThisMonthAsync(monthStart, cancellationToken);
        var demand = await _unitOfWork.HelpRequests.GetCategoryDemandAsync(cancellationToken);

        return new StatsDto(members, openRequests, completed, hours, topHelpers, demand);
    }
}

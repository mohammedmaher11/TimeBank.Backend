using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Members;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Events;

namespace TimeBank.Application.Services;

/// <summary>
/// BR-12: a separate subscriber that awards badges after ExchangeCompleted.
/// Every award is idempotent (checked here and protected by a unique index).
/// </summary>
public sealed class BadgeService : IBadgeService
{
    public const string FirstHandCode = "FIRST_HAND";
    public const string TenHoursHeroCode = "TEN_HOURS_HERO";
    public const string GoodNeighborCode = "GOOD_NEIGHBOR";

    private readonly IUnitOfWork _unitOfWork;
    private readonly Abstractions.Services.IDateTimeProvider _clock;

    public BadgeService(IUnitOfWork unitOfWork, Abstractions.Services.IDateTimeProvider clock)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task EvaluateAsync(ExchangeCompletedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var toAward = new List<MemberBadge>();

        var firstHand = await _unitOfWork.Badges.GetByCodeAsync(FirstHandCode, cancellationToken);
        var completedAsHelper = await _unitOfWork.HelpRequests.CountCompletedAsHelperAsync(
            domainEvent.HelperId, cancellationToken);

        if (firstHand is not null && completedAsHelper >= 1 &&
            !await _unitOfWork.Badges.HasBadgeAsync(domainEvent.HelperId, firstHand.Id, cancellationToken))
        {
            toAward.Add(MemberBadge.Create(domainEvent.HelperId, firstHand.Id, _clock.UtcNow));
        }

        var tenHours = await _unitOfWork.Badges.GetByCodeAsync(TenHoursHeroCode, cancellationToken);
        var credits = await _unitOfWork.LedgerEntries.GetExchangeCreditsAsync(domainEvent.HelperId, cancellationToken);

        if (tenHours is not null && credits >= 10m &&
            !await _unitOfWork.Badges.HasBadgeAsync(domainEvent.HelperId, tenHours.Id, cancellationToken))
        {
            toAward.Add(MemberBadge.Create(domainEvent.HelperId, tenHours.Id, _clock.UtcNow));
        }

        var goodNeighbor = await _unitOfWork.Badges.GetByCodeAsync(GoodNeighborCode, cancellationToken);
        var distinctMembers = await _unitOfWork.HelpRequests.CountDistinctRequestersHelpedAsync(
            domainEvent.HelperId, cancellationToken);

        if (goodNeighbor is not null && distinctMembers >= 5 &&
            !await _unitOfWork.Badges.HasBadgeAsync(domainEvent.HelperId, goodNeighbor.Id, cancellationToken))
        {
            toAward.Add(MemberBadge.Create(domainEvent.HelperId, goodNeighbor.Id, _clock.UtcNow));
        }

        foreach (var award in toAward)
            await _unitOfWork.Badges.AddMemberBadgeAsync(award, cancellationToken);

        if (toAward.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BadgeDto>> GetForMemberAsync(int memberId, CancellationToken cancellationToken = default) =>
        await _unitOfWork.Badges.GetForMemberAsync(memberId, cancellationToken);
}

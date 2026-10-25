using Moq;
using TimeBank.Application.Services;
using TimeBank.Core.Tests.TestSupport;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Events;

namespace TimeBank.Core.Tests;

public sealed class BadgeServiceTests
{
    private readonly UnitOfWorkMock _uow = new();
    private readonly Mock<TimeBank.Application.Abstractions.Services.IDateTimeProvider> _clock =
        TestFactory.Clock();

    private BadgeService CreateService() => new(_uow.UnitOfWork.Object, _clock.Object);

    private static ExchangeCompletedDomainEvent Completed(int helperId = 2, decimal hours = 1.5m) =>
        new(RequestId: 1, RequesterId: 1, HelperId: helperId, Hours: hours, OccurredOn: TestFactory.Now);

    private void NoOtherBadges()
    {
        _uow.Badges
            .Setup(r => r.GetByCodeAsync(BadgeService.TenHoursHeroCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Badge?)null);
        _uow.Badges
            .Setup(r => r.GetByCodeAsync(BadgeService.GoodNeighborCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Badge?)null);
    }

    private void ArrangeFirstHand(bool alreadyAwarded)
    {
        var firstHand = TestFactory.CreateBadge(1, BadgeService.FirstHandCode);
        _uow.Badges
            .Setup(r => r.GetByCodeAsync(BadgeService.FirstHandCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstHand);
        _uow.HelpRequests
            .Setup(r => r.CountCompletedAsHelperAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _uow.Badges
            .Setup(r => r.HasBadgeAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alreadyAwarded);
        NoOtherBadges();

        _uow.LedgerEntries
            .Setup(r => r.GetExchangeCreditsAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1.5m);
        _uow.HelpRequests
            .Setup(r => r.CountDistinctRequestersHelpedAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task Evaluate_FirstCompletedExchange_AwardsFirstHand()
    {
        ArrangeFirstHand(alreadyAwarded: false);
        MemberBadge? captured = null;

        _uow.Badges
            .Setup(r => r.AddMemberBadgeAsync(It.IsAny<MemberBadge>(), It.IsAny<CancellationToken>()))
            .Callback<MemberBadge, CancellationToken>((award, _) => captured = award)
            .Returns(Task.CompletedTask);

        await CreateService().EvaluateAsync(Completed());

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.MemberId);
        Assert.Equal(1, captured.BadgeId);
        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Evaluate_WhenFirstHandWasAlreadyAwarded_DoesNotAwardItTwice()
    {
        ArrangeFirstHand(alreadyAwarded: true);

        await CreateService().EvaluateAsync(Completed());

        _uow.Badges.Verify(
            r => r.AddMemberBadgeAsync(It.IsAny<MemberBadge>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Evaluate_WhenHelperReachedTenHours_AwardsTenHoursHero()
    {
        var tenHours = TestFactory.CreateBadge(2, BadgeService.TenHoursHeroCode);
        _uow.Badges
            .Setup(r => r.GetByCodeAsync(BadgeService.FirstHandCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Badge?)null);
        _uow.Badges
            .Setup(r => r.GetByCodeAsync(BadgeService.TenHoursHeroCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenHours);
        _uow.Badges
            .Setup(r => r.GetByCodeAsync(BadgeService.GoodNeighborCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Badge?)null);
        _uow.LedgerEntries
            .Setup(r => r.GetExchangeCreditsAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        _uow.Badges
            .Setup(r => r.HasBadgeAsync(2, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _uow.HelpRequests
            .Setup(r => r.CountDistinctRequestersHelpedAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        MemberBadge? captured = null;
        _uow.Badges
            .Setup(r => r.AddMemberBadgeAsync(It.IsAny<MemberBadge>(), It.IsAny<CancellationToken>()))
            .Callback<MemberBadge, CancellationToken>((award, _) => captured = award)
            .Returns(Task.CompletedTask);

        await CreateService().EvaluateAsync(Completed(hours: 10m));

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.BadgeId);
    }

    [Fact]
    public async Task Evaluate_WhenHelperHelpedFiveDifferentMembers_AwardsGoodNeighbor()
    {
        var goodNeighbor = TestFactory.CreateBadge(3, BadgeService.GoodNeighborCode);
        _uow.Badges
            .Setup(r => r.GetByCodeAsync(BadgeService.FirstHandCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Badge?)null);
        _uow.Badges
            .Setup(r => r.GetByCodeAsync(BadgeService.TenHoursHeroCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Badge?)null);
        _uow.Badges
            .Setup(r => r.GetByCodeAsync(BadgeService.GoodNeighborCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(goodNeighbor);
        _uow.LedgerEntries
            .Setup(r => r.GetExchangeCreditsAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3m);
        _uow.HelpRequests
            .Setup(r => r.CountDistinctRequestersHelpedAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _uow.Badges
            .Setup(r => r.HasBadgeAsync(2, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        MemberBadge? captured = null;
        _uow.Badges
            .Setup(r => r.AddMemberBadgeAsync(It.IsAny<MemberBadge>(), It.IsAny<CancellationToken>()))
            .Callback<MemberBadge, CancellationToken>((award, _) => captured = award)
            .Returns(Task.CompletedTask);

        await CreateService().EvaluateAsync(Completed());

        Assert.NotNull(captured);
        Assert.Equal(3, captured!.BadgeId);
    }
}

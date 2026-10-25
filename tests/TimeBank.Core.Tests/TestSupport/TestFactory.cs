using System.Reflection;
using Moq;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Domain.Common;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;

namespace TimeBank.Core.Tests.TestSupport;

/// <summary>
/// Builds real domain entities (not mocks) so the tests exercise the same
/// encapsulation and state transitions used by the application.
/// </summary>
public static class TestFactory
{
    public static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    public static void SetId(object entity, int id)
    {
        var property = typeof(BaseEntity).GetProperty(
            nameof(BaseEntity.Id),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

        property.SetValue(entity, id);
    }

    public static Member CreateMember(
        int id,
        bool suspended = false,
        MemberRole role = MemberRole.Member,
        string? email = null)
    {
        var member = Member.Create(
            $"Member {id}",
            email ?? $"member{id}@timebank.test",
            "bcrypt-hash",
            "Nasr City",
            null,
            null,
            role,
            Now.AddDays(-30));

        SetId(member, id);

        if (suspended)
            member.Suspend();

        return member;
    }

    public static HelpRequest Request(
        int id,
        int requesterId = 1,
        decimal estimatedHours = 1.5m,
        int categoryId = 1,
        DateTime? createdAt = null)
    {
        var request = HelpRequest.Create(
            requesterId,
            $"Request {id}",
            "A request created by the test suite.",
            categoryId,
            "Nasr City",
            estimatedHours,
            DateOnly.FromDateTime(Now).AddDays(7),
            createdAt ?? Now.AddDays(-1));

        SetId(request, id);
        return request;
    }

    public static Offer AddOffer(
        HelpRequest request,
        int offerId,
        int helperId,
        decimal hours = 1.5m)
    {
        var offer = request.AddOffer(helperId, $"Offer {offerId}", hours, Now);
        SetId(offer, offerId);
        return offer;
    }

    public static HelpRequest InProgressRequest(
        int id = 1,
        int requesterId = 1,
        int helperId = 2,
        decimal hours = 1.5m,
        int offerId = 10,
        DateTime? createdAt = null)
    {
        var request = Request(id, requesterId, hours, createdAt: createdAt);
        AddOffer(request, offerId, helperId, hours);
        request.AcceptOffer(offerId, availableHours: 10m, Now);
        return request;
    }

    public static Badge CreateBadge(int id, string code)
    {
        var badge = Badge.Create(code, $"{code} name", "icon", $"{code} description");
        SetId(badge, id);
        return badge;
    }

    public static Mock<IDateTimeProvider> Clock(DateTime? now = null)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(now ?? Now);
        return clock;
    }
}

/// <summary>
/// Creates a loose IUnitOfWork whose repositories are individual mocks and whose
/// ExecuteInTransactionAsync simply invokes the operation (the real transaction is
/// covered by the SQL Server integration tests).
/// </summary>
public sealed class UnitOfWorkMock
{
    public Mock<IUnitOfWork> UnitOfWork { get; } = new(MockBehavior.Loose);
    public Mock<IMemberRepository> Members { get; } = new();
    public Mock<ICategoryRepository> Categories { get; } = new();
    public Mock<ISkillRepository> Skills { get; } = new();
    public Mock<IMemberSkillRepository> MemberSkills { get; } = new();
    public Mock<IHelpRequestRepository> HelpRequests { get; } = new();
    public Mock<IOfferRepository> Offers { get; } = new();
    public Mock<ILedgerEntryRepository> LedgerEntries { get; } = new();
    public Mock<IReviewRepository> Reviews { get; } = new();
    public Mock<IBadgeRepository> Badges { get; } = new();
    public Mock<IAuditLogRepository> AuditLogs { get; } = new();

    public UnitOfWorkMock()
    {
        UnitOfWork.SetupGet(u => u.Members).Returns(Members.Object);
        UnitOfWork.SetupGet(u => u.Categories).Returns(Categories.Object);
        UnitOfWork.SetupGet(u => u.Skills).Returns(Skills.Object);
        UnitOfWork.SetupGet(u => u.MemberSkills).Returns(MemberSkills.Object);
        UnitOfWork.SetupGet(u => u.HelpRequests).Returns(HelpRequests.Object);
        UnitOfWork.SetupGet(u => u.Offers).Returns(Offers.Object);
        UnitOfWork.SetupGet(u => u.LedgerEntries).Returns(LedgerEntries.Object);
        UnitOfWork.SetupGet(u => u.Reviews).Returns(Reviews.Object);
        UnitOfWork.SetupGet(u => u.Badges).Returns(Badges.Object);
        UnitOfWork.SetupGet(u => u.AuditLogs).Returns(AuditLogs.Object);

        UnitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        UnitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<int?>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((int? _, Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct));
    }
}

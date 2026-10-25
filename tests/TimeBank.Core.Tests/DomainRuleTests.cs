using TimeBank.Core.Tests.TestSupport;
using TimeBank.Domain.Common;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Core.Tests;

public sealed class DomainRuleTests
{
    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(1.5)]
    [InlineData(8)]
    public void HoursPolicy_AcceptsHalfHourStepsInRange(decimal hours)
    {
        HoursPolicy.EnsurePositiveHalfHour(hours, "Hours", 0.5m, 8m);
        Assert.True(HoursPolicy.IsHalfHourStep(hours));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.25)]
    [InlineData(8.5)]
    public void HoursPolicy_RejectsInvalidAmounts(decimal hours)
    {
        Assert.Throws<BusinessRuleException>(
            () => HoursPolicy.EnsurePositiveHalfHour(hours, "Hours", 0.5m, 8m));
    }

    [Fact]
    public void LedgerEntry_RejectsZeroHours()
    {
        Assert.Throws<BusinessRuleException>(() => LedgerEntry.Create(
            memberId: 1,
            hours: 0m,
            type: LedgerEntryType.ExchangeCredit,
            description: "Zero is not allowed.",
            referenceId: null,
            createdAt: TestFactory.Now));
    }

    [Fact]
    public void LedgerEntry_DebitMustBeNegative()
    {
        Assert.Throws<BusinessRuleException>(() => LedgerEntry.Create(
            memberId: 1,
            hours: 1m,
            type: LedgerEntryType.ExchangeDebit,
            description: "Debit with a positive sign.",
            referenceId: null,
            createdAt: TestFactory.Now));
    }

    [Fact]
    public void HelpRequest_NeedsLove_IsComputedAfterFourteenDays()
    {
        var request = TestFactory.Request(1, createdAt: TestFactory.Now.AddDays(-15));

        Assert.True(request.NeedsLove(TestFactory.Now));
        Assert.False(request.NeedsLove(TestFactory.Now.AddDays(-2)));
    }

    [Fact]
    public void HelpRequest_InProgress_DoesNotNeedLove()
    {
        var request = TestFactory.InProgressRequest(createdAt: TestFactory.Now.AddDays(-30));

        Assert.False(request.NeedsLove(TestFactory.Now));
    }

    [Fact]
    public void HelpRequest_AcceptOffer_LocksHoursAndRejectsOtherOffers()
    {
        var request = TestFactory.Request(1);
        var accepted = TestFactory.AddOffer(request, 10, helperId: 2);
        var other = TestFactory.AddOffer(request, 11, helperId: 3);

        request.AcceptOffer(10, availableHours: 2m, TestFactory.Now);

        Assert.Equal(RequestStatus.InProgress, request.Status);
        Assert.Equal(10, request.AcceptedOfferId);
        Assert.Equal(2, request.AcceptedHelperId);
        Assert.Equal(1.5m, request.AcceptedHours);
        Assert.Equal(OfferStatus.Accepted, accepted.Status);
        Assert.Equal(OfferStatus.Rejected, other.Status);
    }

    [Fact]
    public void HelpRequest_Complete_AllowsHalfHourActualHoursUpToAccepted()
    {
        var request = TestFactory.InProgressRequest(hours: 1.5m);

        request.Complete(0.5m, TestFactory.Now);

        Assert.Equal(RequestStatus.Completed, request.Status);
        Assert.Equal(0.5m, request.ActualHours);
        Assert.Single(request.DomainEvents);
    }

    [Fact]
    public void HelpRequest_Cancel_IsFinal()
    {
        var request = TestFactory.Request(1);
        request.Cancel(TestFactory.Now);

        Assert.Equal(RequestStatus.Cancelled, request.Status);
        Assert.Throws<ConflictException>(() => request.Cancel(TestFactory.Now));
    }

    [Fact]
    public void Member_SuspendAndReinstate_AreIdempotentStateChecks()
    {
        var member = TestFactory.CreateMember(1);

        member.Suspend();
        Assert.True(member.IsSuspended);
        Assert.Throws<ConflictException>(member.Suspend);

        member.Reinstate();
        Assert.False(member.IsSuspended);
        Assert.Throws<ConflictException>(member.Reinstate);
    }
}

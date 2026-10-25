using Moq;
using TimeBank.Application.Abstractions.Events;
using TimeBank.Application.Services;
using TimeBank.Core.Tests.TestSupport;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Events;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Core.Tests;

public sealed class ExchangeAcceptCompleteTests
{
    private readonly UnitOfWorkMock _uow = new();
    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly Mock<TimeBank.Application.Abstractions.Services.IDateTimeProvider> _clock =
        TestFactory.Clock();

    private ExchangeService CreateService() =>
        new(_uow.UnitOfWork.Object, _clock.Object, _dispatcher.Object);

    private void ArrangeAccept(
        HelpRequest request,
        Offer offer,
        int requesterId = 1,
        decimal balance = 5m,
        decimal locked = 0m)
    {
        _uow.Members.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(requesterId));
        _uow.Offers.Setup(r => r.GetByIdAsync(offer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _uow.HelpRequests
            .Setup(r => r.GetByIdWithOffersAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _uow.LedgerEntries
            .Setup(r => r.GetBalanceAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _uow.HelpRequests
            .Setup(r => r.GetLockedHoursAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(locked);
    }

    [Fact]
    public async Task AcceptOffer_WithInsufficientAvailableBalance_ThrowsInsufficientHoursException()
    {
        var request = TestFactory.Request(1, requesterId: 1);
        var offer = TestFactory.AddOffer(request, 10, helperId: 2, hours: 1.5m);
        ArrangeAccept(request, offer, balance: 2m, locked: 1m); // available = 1 < 1.5

        var exception = await Assert.ThrowsAsync<InsufficientHoursException>(
            () => CreateService().AcceptOfferAsync(1, 10));

        Assert.Contains("Insufficient available hours", exception.Message);
        Assert.Equal(RequestStatus.Open, request.Status);
        Assert.Equal(OfferStatus.Pending, offer.Status);
    }

    [Fact]
    public async Task AcceptOffer_RejectsAllOtherPendingOffersAndMovesRequestToInProgress()
    {
        var request = TestFactory.Request(1, requesterId: 1);
        var accepted = TestFactory.AddOffer(request, 10, helperId: 2, hours: 1.5m);
        var other = TestFactory.AddOffer(request, 11, helperId: 3, hours: 1m);
        ArrangeAccept(request, accepted);

        var result = await CreateService().AcceptOfferAsync(1, 10);

        Assert.Equal("InProgress", result.Status);
        Assert.Equal(10, result.AcceptedOfferId);
        Assert.Equal(1.5m, result.AcceptedHours);
        Assert.Equal(RequestStatus.InProgress, request.Status);
        Assert.Equal(OfferStatus.Accepted, accepted.Status);
        Assert.Equal(OfferStatus.Rejected, other.Status);
        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcceptOffer_OnRequestAlreadyInProgress_ThrowsConflictException()
    {
        var request = TestFactory.InProgressRequest(id: 1, requesterId: 1, helperId: 2);
        var accepted = request.Offers.Single();
        ArrangeAccept(request, accepted);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().AcceptOfferAsync(1, accepted.Id));

        Assert.Contains("Only an Open request", exception.Message);
    }

    [Fact]
    public async Task AcceptOffer_WhenCallerIsNotTheRequester_ThrowsForbiddenException()
    {
        var request = TestFactory.Request(1, requesterId: 1);
        var offer = TestFactory.AddOffer(request, 10, helperId: 3);

        _uow.Members.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(2));
        _uow.Offers.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => CreateService().AcceptOfferAsync(2, 10));

        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Complete_CreatesOppositeLedgerEntriesAndCommitsOnce()
    {
        var request = TestFactory.InProgressRequest(id: 1, requesterId: 1, helperId: 2, hours: 1.5m);
        List<LedgerEntry>? captured = null;

        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1));
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _uow.LedgerEntries
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<LedgerEntry>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<LedgerEntry>, CancellationToken>((entries, _) => captured = entries.ToList())
            .Returns(Task.CompletedTask);
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().CompleteAsync(1, 1, actualHours: 1.5m);

        Assert.Equal("Completed", result.Status);
        Assert.Equal(1.5m, result.ActualHours);
        Assert.Equal(RequestStatus.Completed, request.Status);
        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Count);

        var debit = Assert.Single(captured, e => e.Type == LedgerEntryType.ExchangeDebit);
        var credit = Assert.Single(captured, e => e.Type == LedgerEntryType.ExchangeCredit);
        Assert.Equal(1, debit.MemberId);
        Assert.Equal(-1.5m, debit.Hours);
        Assert.Equal(2, credit.MemberId);
        Assert.Equal(1.5m, credit.Hours);
        Assert.Equal(debit.ReferenceId, credit.ReferenceId);

        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<IEnumerable<IDomainEvent>>(events =>
                    events.Count() == 1 && events.Single() is ExchangeCompletedDomainEvent),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Complete_WhenRequestIsAlreadyCompleted_ThrowsConflictAndAddsNoSecondEntries()
    {
        var request = TestFactory.InProgressRequest(id: 1, requesterId: 1, helperId: 2, hours: 1.5m);

        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1));
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _uow.LedgerEntries
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<LedgerEntry>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.CompleteAsync(1, 1, 1.5m);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.CompleteAsync(1, 1, 1.5m));

        Assert.Contains("Only an InProgress request", exception.Message);
        _uow.LedgerEntries.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<LedgerEntry>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Complete_WithActualHoursAboveAccepted_ThrowsBusinessRuleException()
    {
        var request = TestFactory.InProgressRequest(id: 1, requesterId: 1, helperId: 2, hours: 1.5m);

        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1));
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CompleteAsync(1, 1, actualHours: 2m));

        Assert.Contains("cannot exceed", exception.Message);
        Assert.Equal(RequestStatus.InProgress, request.Status);
        _uow.LedgerEntries.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<LedgerEntry>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Complete_WhenCallerIsNotTheRequester_ThrowsForbiddenException()
    {
        var request = TestFactory.InProgressRequest(id: 1, requesterId: 1, helperId: 3);

        _uow.Members.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(2));
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => CreateService().CompleteAsync(2, 1, 1.5m));
    }

    [Fact]
    public async Task Cancel_OnCompletedRequest_ThrowsConflictException()
    {
        var request = TestFactory.InProgressRequest(id: 1, requesterId: 1, helperId: 2);
        request.Complete(1.5m, TestFactory.Now);

        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1));
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().CancelAsync(1, 1));

        Assert.Contains("cannot be cancelled", exception.Message);
        Assert.Equal(RequestStatus.Completed, request.Status);
    }

    [Fact]
    public async Task Cancel_InProgressRequest_ReleasesLockByLeavingInProgressState()
    {
        var request = TestFactory.InProgressRequest(id: 1, requesterId: 1, helperId: 2);

        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1));
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await CreateService().CancelAsync(1, 1);

        Assert.Equal("Cancelled", result.Status);
        Assert.Equal(RequestStatus.Cancelled, request.Status);
        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

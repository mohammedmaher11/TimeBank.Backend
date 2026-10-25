using Moq;
using TimeBank.Application.Abstractions.Events;
using TimeBank.Application.DTOs.Requests;
using TimeBank.Application.Services;
using TimeBank.Core.Tests.TestSupport;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Core.Tests;

public sealed class ExchangeOfferTests
{
    private readonly UnitOfWorkMock _uow = new();
    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly Mock<TimeBank.Application.Abstractions.Services.IDateTimeProvider> _clock =
        TestFactory.Clock();

    private ExchangeService CreateService() =>
        new(_uow.UnitOfWork.Object, _clock.Object, _dispatcher.Object);

    private static SendOfferRequest Offer(string message = "I can help.") =>
        new() { Message = message, Hours = 1.5m };

    [Fact]
    public async Task SendOffer_OnOwnRequest_ThrowsForbiddenException()
    {
        var requester = TestFactory.CreateMember(1);
        var request = TestFactory.Request(1, requesterId: 1);

        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(
            () => CreateService().SendOfferAsync(1, 1, Offer()));

        Assert.Contains("own request", exception.Message);
        _uow.Offers.Verify(
            r => r.AddAsync(It.IsAny<Offer>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendOffer_SecondOfferFromSameMember_ThrowsConflictException()
    {
        var helper = TestFactory.CreateMember(2);
        var request = TestFactory.Request(1, requesterId: 1);
        TestFactory.AddOffer(request, 10, helperId: 2);

        _uow.Members.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(helper);
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().SendOfferAsync(2, 1, Offer("A second offer.")));

        Assert.Contains("already sent an offer", exception.Message);
        _uow.Offers.Verify(
            r => r.AddAsync(It.IsAny<Offer>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendOffer_WithoutSkillInCategory_ThrowsBusinessRuleException()
    {
        var helper = TestFactory.CreateMember(3);
        var request = TestFactory.Request(1, requesterId: 1, categoryId: 4);

        _uow.Members.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(helper);
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _uow.MemberSkills
            .Setup(r => r.HasSkillInCategoryAsync(3, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().SendOfferAsync(3, 1, Offer()));

        Assert.Contains("skill in the request's category", exception.Message);
        _uow.Offers.Verify(
            r => r.AddAsync(It.IsAny<Offer>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendOffer_WhenRequestIsNoLongerOpen_ThrowsConflictException()
    {
        var helper = TestFactory.CreateMember(3);
        var request = TestFactory.InProgressRequest(id: 1, requesterId: 1, helperId: 2);

        _uow.Members.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(helper);
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().SendOfferAsync(3, 1, Offer()));

        Assert.Contains("Open", exception.Message);
    }

    [Fact]
    public async Task SendOffer_WhenAllRulesPass_CreatesOfferAndSavesOnce()
    {
        var helper = TestFactory.CreateMember(2);
        var request = TestFactory.Request(1, requesterId: 1, categoryId: 1);

        _uow.Members.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(helper);
        _uow.HelpRequests.Setup(r => r.GetByIdWithOffersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _uow.MemberSkills
            .Setup(r => r.HasSkillInCategoryAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateService().SendOfferAsync(2, 1, Offer());

        _uow.Offers.Verify(
            r => r.AddAsync(It.Is<Offer>(o =>
                o.HelpRequestId == 1 &&
                o.HelperId == 2 &&
                o.Hours == 1.5m &&
                o.Status == TimeBank.Domain.Enums.OfferStatus.Pending),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendOffer_WhenMemberIsSuspended_ThrowsForbiddenExceptionBeforeLoadingRequest()
    {
        var suspended = TestFactory.CreateMember(2, suspended: true);

        _uow.Members.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspended);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => CreateService().SendOfferAsync(2, 1, Offer()));

        _uow.HelpRequests.Verify(
            r => r.GetByIdWithOffersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

using Moq;
using TimeBank.Application.Abstractions.Events;
using TimeBank.Application.DTOs.Reviews;
using TimeBank.Application.Services;
using TimeBank.Core.Tests.TestSupport;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Core.Tests;

public sealed class ExchangeReviewTests
{
    private readonly UnitOfWorkMock _uow = new();
    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly Mock<TimeBank.Application.Abstractions.Services.IDateTimeProvider> _clock =
        TestFactory.Clock();

    private ExchangeService CreateService() =>
        new(_uow.UnitOfWork.Object, _clock.Object, _dispatcher.Object);

    private static CreateReviewRequest Review(int rating = 5) =>
        new() { Rating = rating, Comment = "Great exchange." };

    private HelpRequest CompletedRequest()
    {
        var request = TestFactory.InProgressRequest(id: 1, requesterId: 1, helperId: 2);
        request.Complete(1.5m, TestFactory.Now);
        return request;
    }

    [Fact]
    public async Task AddReview_SecondReviewBySamePerson_ThrowsConflictException()
    {
        var request = CompletedRequest();

        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1));
        _uow.HelpRequests.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _uow.Reviews.Setup(r => r.ExistsAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().AddReviewAsync(1, 1, Review()));

        Assert.Contains("already reviewed", exception.Message);
        _uow.Reviews.Verify(
            r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddReview_BeforeExchangeIsCompleted_ThrowsConflictException()
    {
        var request = TestFactory.Request(1, requesterId: 1);

        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1));
        _uow.HelpRequests.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateService().AddReviewAsync(1, 1, Review()));

        Assert.Contains("only be added after", exception.Message);
    }

    [Fact]
    public async Task AddReview_ByUnrelatedMember_ThrowsForbiddenException()
    {
        var request = CompletedRequest();

        _uow.Members.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(3));
        _uow.HelpRequests.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => CreateService().AddReviewAsync(3, 1, Review()));
    }

    [Fact]
    public async Task AddReview_ByRequester_CreatesReviewForHelper()
    {
        var request = CompletedRequest();
        Review? captured = null;

        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1));
        _uow.HelpRequests.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _uow.Reviews.Setup(r => r.ExistsAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _uow.Reviews
            .Setup(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()))
            .Callback<Review, CancellationToken>((review, _) => captured = review)
            .Returns(Task.CompletedTask);

        await CreateService().AddReviewAsync(1, 1, Review(rating: 4));

        Assert.NotNull(captured);
        Assert.Equal(1, captured!.ReviewerId);
        Assert.Equal(2, captured.RevieweeId);
        Assert.Equal(4, captured.Rating);
        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddReview_ByHelper_CreatesReviewForRequester()
    {
        var request = CompletedRequest();
        Review? captured = null;

        _uow.Members.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(2));
        _uow.HelpRequests.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _uow.Reviews.Setup(r => r.ExistsAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _uow.Reviews
            .Setup(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()))
            .Callback<Review, CancellationToken>((review, _) => captured = review)
            .Returns(Task.CompletedTask);

        await CreateService().AddReviewAsync(2, 1, Review(rating: 5));

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.ReviewerId);
        Assert.Equal(1, captured.RevieweeId);
    }
}

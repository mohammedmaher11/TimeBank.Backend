using TimeBank.Application.Abstractions.Events;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Requests;
using TimeBank.Application.DTOs.Reviews;
using TimeBank.Application.Factories;
using TimeBank.Application.Mapping;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Events;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Application.Services;

public sealed class ExchangeService : IExchangeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ExchangeService(
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IDomainEventDispatcher eventDispatcher)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<int> CreateRequestAsync(
        int requesterId, CreateHelpRequestRequest request, CancellationToken cancellationToken = default)
    {
        var requester = await _unitOfWork.Members.GetByIdAsync(requesterId, cancellationToken)
            ?? throw new NotFoundException("Member was not found.");

        // BR-14: check the current database state, not a JWT claim.
        requester.EnsureActive();

        var category = await _unitOfWork.Categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        var entity = HelpRequest.Create(
            requesterId,
            request.Title,
            request.Description,
            category.Id,
            request.District,
            request.EstimatedHours,
            request.NeededBy,
            _clock.UtcNow);

        await _unitOfWork.HelpRequests.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<int> SendOfferAsync(
        int helperId, int requestId, SendOfferRequest request, CancellationToken cancellationToken = default)
    {
        var helper = await _unitOfWork.Members.GetByIdAsync(helperId, cancellationToken)
            ?? throw new NotFoundException("Member was not found.");

        // BR-14: suspended members cannot send offers, even with an old token.
        helper.EnsureActive();

        var helpRequest = await _unitOfWork.HelpRequests.GetByIdWithOffersAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Request was not found.");

        // BR-06: validate the cheap, unambiguous conditions first so the API returns
        // 409/403 rather than a generic 400 when more than one rule is broken.
        if (helpRequest.Status != RequestStatus.Open)
            throw new ConflictException(
                $"Offers can only be sent while the request is Open. Current status: {helpRequest.Status}.");

        if (helpRequest.RequesterId == helperId)
            throw new ForbiddenException("You cannot offer to help with your own request.");

        if (helpRequest.Offers.Any(o => o.HelperId == helperId))
            throw new ConflictException("You have already sent an offer on this request.");

        // BR-06: at least one skill in the request's category.
        if (!await _unitOfWork.MemberSkills.HasSkillInCategoryAsync(helperId, helpRequest.CategoryId, cancellationToken))
            throw new BusinessRuleException("You need at least one skill in the request's category to offer help.");

        var offer = helpRequest.AddOffer(helperId, request.Message, request.Hours, _clock.UtcNow);
        await _unitOfWork.Offers.AddAsync(offer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return offer.Id;
    }

    public async Task<RequestStatusDto> AcceptOfferAsync(
        int requesterId, int offerId, CancellationToken cancellationToken = default)
    {
        RequestStatusDto? result = null;

        await _unitOfWork.ExecuteInTransactionAsync(requesterId, async ct =>
        {
            var requester = await _unitOfWork.Members.GetByIdAsync(requesterId, ct)
                ?? throw new NotFoundException("Member was not found.");
            requester.EnsureActive();

            var offer = await _unitOfWork.Offers.GetByIdAsync(offerId, ct)
                ?? throw new NotFoundException("Offer was not found.");

            var helpRequest = await _unitOfWork.HelpRequests.GetByIdWithOffersAsync(offer.HelpRequestId, ct)
                ?? throw new NotFoundException("Request was not found.");

            if (helpRequest.RequesterId != requesterId)
                throw new ForbiddenException("Only the request owner can accept an offer.");

            // BR-05: Available = Balance - Locked.
            var balance = await _unitOfWork.LedgerEntries.GetBalanceAsync(requesterId, ct);
            var locked = await _unitOfWork.HelpRequests.GetLockedHoursAsync(requesterId, ct);
            var available = balance - locked;

            // BR-07: state + balance checks, lock the hours, reject all other offers.
            helpRequest.AcceptOffer(offerId, available, _clock.UtcNow);

            await _unitOfWork.SaveChangesAsync(ct);
            result = helpRequest.ToStatus();
        }, cancellationToken);

        return result ?? throw new InvalidOperationException("Accepting the offer did not complete.");
    }

    public async Task<RequestStatusDto> CompleteAsync(
        int requesterId, int requestId, decimal actualHours, CancellationToken cancellationToken = default)
    {
        RequestStatusDto? result = null;
        var domainEvents = new List<IDomainEvent>();

        await _unitOfWork.ExecuteInTransactionAsync(requesterId, async ct =>
        {
            var requester = await _unitOfWork.Members.GetByIdAsync(requesterId, ct)
                ?? throw new NotFoundException("Member was not found.");
            requester.EnsureActive();

            var helpRequest = await _unitOfWork.HelpRequests.GetByIdWithOffersAsync(requestId, ct)
                ?? throw new NotFoundException("Request was not found.");

            if (helpRequest.RequesterId != requesterId)
                throw new ForbiddenException("Only the request owner can confirm completion.");

            // BR-08: validates InProgress state and actual hours (0.5 .. accepted).
            helpRequest.Complete(actualHours, _clock.UtcNow);

            if (helpRequest.AcceptedHelperId is null)
                throw new ConflictException("The request has no accepted helper.");

            // BR-08: both ledger entries and the status change are saved in one commit.
            var debit = LedgerEntryFactory.ExchangeDebit(requesterId, helpRequest.Id, actualHours, _clock.UtcNow);
            var credit = LedgerEntryFactory.ExchangeCredit(
                helpRequest.AcceptedHelperId.Value, helpRequest.Id, actualHours, _clock.UtcNow);

            await _unitOfWork.LedgerEntries.AddRangeAsync([debit, credit], ct);
            await _unitOfWork.SaveChangesAsync(ct);

            domainEvents.AddRange(helpRequest.DomainEvents);
            helpRequest.ClearDomainEvents();
            result = helpRequest.ToStatus();
        }, cancellationToken);

        // BR-12: the exchange is already committed; badge awarding is a separate subscriber.
        if (domainEvents.Count > 0)
            await _eventDispatcher.DispatchAsync(domainEvents, cancellationToken);

        return result ?? throw new InvalidOperationException("Completing the request did not finish.");
    }

    public async Task<RequestStatusDto> CancelAsync(
        int requesterId, int requestId, CancellationToken cancellationToken = default)
    {
        RequestStatusDto? result = null;

        await _unitOfWork.ExecuteInTransactionAsync(requesterId, async ct =>
        {
            var requester = await _unitOfWork.Members.GetByIdAsync(requesterId, ct)
                ?? throw new NotFoundException("Member was not found.");
            requester.EnsureActive();

            var helpRequest = await _unitOfWork.HelpRequests.GetByIdWithOffersAsync(requestId, ct)
                ?? throw new NotFoundException("Request was not found.");

            if (helpRequest.RequesterId != requesterId)
                throw new ForbiddenException("Only the request owner can cancel it.");

            // BR-09: cancelling InProgress releases the lock because the status leaves InProgress.
            helpRequest.Cancel(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            result = helpRequest.ToStatus();
        }, cancellationToken);

        return result ?? throw new InvalidOperationException("Cancelling the request did not finish.");
    }

    public async Task<int> AddReviewAsync(
        int reviewerId, int requestId, CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        var reviewer = await _unitOfWork.Members.GetByIdAsync(reviewerId, cancellationToken)
            ?? throw new NotFoundException("Member was not found.");
        reviewer.EnsureActive();

        var helpRequest = await _unitOfWork.HelpRequests.GetByIdAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Request was not found.");

        if (helpRequest.Status != RequestStatus.Completed)
            throw new ConflictException("Reviews can only be added after the request is Completed.");

        if (helpRequest.AcceptedHelperId is null)
            throw new ConflictException("The request has no accepted helper.");

        var isRequester = helpRequest.RequesterId == reviewerId;
        var isHelper = helpRequest.AcceptedHelperId.Value == reviewerId;

        if (!isRequester && !isHelper)
            throw new ForbiddenException("Only the two parties of the exchange can review each other.");

        if (await _unitOfWork.Reviews.ExistsAsync(requestId, reviewerId, cancellationToken))
            throw new ConflictException("You have already reviewed this exchange.");

        var revieweeId = isRequester ? helpRequest.AcceptedHelperId.Value : helpRequest.RequesterId;

        var review = Review.Create(
            requestId,
            reviewerId,
            revieweeId,
            request.Rating,
            request.Comment,
            _clock.UtcNow);

        await _unitOfWork.Reviews.AddAsync(review, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return review.Id;
    }
}

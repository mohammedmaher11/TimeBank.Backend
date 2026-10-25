using TimeBank.Domain.Common;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Events;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Entities;

/// <summary>
/// A help request. Its status can only move through AcceptOffer, Complete and Cancel,
/// and every transition validates the current state (BR-07, BR-08, BR-09).
/// </summary>
public class HelpRequest : BaseEntity
{
    public const int NeedsLoveAfterDays = 14;

    private readonly List<Offer> _offers = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    private HelpRequest()
    {
    }

    public int RequesterId { get; private set; }
    public Member Requester { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
    public string District { get; private set; } = null!;
    public decimal EstimatedHours { get; private set; }
    public DateOnly NeededBy { get; private set; }
    public RequestStatus Status { get; private set; }
    public int? AcceptedOfferId { get; private set; }
    public int? AcceptedHelperId { get; private set; }
    public decimal? AcceptedHours { get; private set; }
    public decimal? ActualHours { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    /// <summary>Optimistic concurrency token. Makes simultaneous state changes fail safely with 409.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<Offer> Offers => _offers;
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public static HelpRequest Create(
        int requesterId,
        string title,
        string description,
        int categoryId,
        string district,
        decimal estimatedHours,
        DateOnly neededBy,
        DateTime createdAt)
    {
        if (requesterId <= 0)
            throw new BusinessRuleException("A requester is required.");

        if (string.IsNullOrWhiteSpace(title))
            throw new BusinessRuleException("Request title is required.");

        if (title.Trim().Length > 150)
            throw new BusinessRuleException("Request title cannot exceed 150 characters.");

        if (string.IsNullOrWhiteSpace(description))
            throw new BusinessRuleException("Request description is required.");

        if (description.Trim().Length > 2000)
            throw new BusinessRuleException("Request description cannot exceed 2000 characters.");

        if (categoryId <= 0)
            throw new BusinessRuleException("A category is required.");

        if (string.IsNullOrWhiteSpace(district))
            throw new BusinessRuleException("District is required.");

        // BR-03: estimated hours are between 0.5 and 8 in half-hour steps.
        HoursPolicy.EnsurePositiveHalfHour(
            estimatedHours, "Estimated hours", HoursPolicy.MinRequestHours, HoursPolicy.MaxRequestHours);

        if (neededBy == default)
            throw new BusinessRuleException("A needed-by date is required.");

        return new HelpRequest
        {
            RequesterId = requesterId,
            Title = title.Trim(),
            Description = description.Trim(),
            CategoryId = categoryId,
            District = district.Trim(),
            EstimatedHours = estimatedHours,
            NeededBy = neededBy,
            Status = RequestStatus.Open,
            CreatedAt = createdAt
        };
    }

    /// <summary>BR-06: only on an Open request, never your own, one offer per member per request.</summary>
    public Offer AddOffer(int helperId, string message, decimal hours, DateTime now)
    {
        if (Status != RequestStatus.Open)
            throw new ConflictException($"Offers can only be sent while the request is Open. Current status: {Status}.");

        if (helperId == RequesterId)
            throw new ForbiddenException("You cannot offer to help with your own request.");

        if (_offers.Any(o => o.HelperId == helperId))
            throw new ConflictException("You have already sent an offer on this request.");

        HoursPolicy.EnsurePositiveHalfHour(hours, "Offer hours", HoursPolicy.MinRequestHours, HoursPolicy.MaxRequestHours);

        var offer = Offer.Create(Id, helperId, message, hours, now);
        _offers.Add(offer);
        return offer;
    }

    /// <summary>
    /// BR-07: accepting checks the current state and the requester's available balance,
    /// locks the accepted hours, rejects every other pending offer, and moves the request to InProgress.
    /// </summary>
    public Offer AcceptOffer(int offerId, decimal availableHours, DateTime now)
    {
        if (Status != RequestStatus.Open)
            throw new ConflictException($"Only an Open request can accept an offer. Current status: {Status}.");

        var offer = _offers.FirstOrDefault(o => o.Id == offerId)
            ?? throw new NotFoundException("Offer was not found on this request.");

        if (offer.Status != OfferStatus.Pending)
            throw new ConflictException($"Only a Pending offer can be accepted. Current status: {offer.Status}.");

        if (offer.HelperId == RequesterId)
            throw new ForbiddenException("You cannot accept your own offer.");

        if (availableHours < offer.Hours)
            throw new InsufficientHoursException(
                $"Insufficient available hours. Required: {offer.Hours}, available: {availableHours}.");

        offer.Accept(now);

        foreach (var other in _offers.Where(o => o.Id != offer.Id && o.Status == OfferStatus.Pending))
            other.Reject(now);

        Status = RequestStatus.InProgress;
        AcceptedOfferId = offer.Id;
        AcceptedHelperId = offer.HelperId;
        AcceptedHours = offer.Hours;
        return offer;
    }

    /// <summary>
    /// BR-08: only the requester (checked by the application service) can complete an InProgress request.
    /// Actual hours must be between 0.5 and the accepted hours. Raises ExchangeCompleted.
    /// </summary>
    public void Complete(decimal actualHours, DateTime now)
    {
        if (Status != RequestStatus.InProgress)
            throw new ConflictException($"Only an InProgress request can be completed. Current status: {Status}.");

        if (AcceptedHours is null || AcceptedHelperId is null)
            throw new ConflictException("The request has no accepted offer.");

        HoursPolicy.EnsurePositiveHalfHour(
            actualHours, "Actual hours", HoursPolicy.MinRequestHours, AcceptedHours.Value);

        Status = RequestStatus.Completed;
        ActualHours = actualHours;
        CompletedAt = now;

        _domainEvents.Add(new ExchangeCompletedDomainEvent(
            Id, RequesterId, AcceptedHelperId.Value, actualHours, now));
    }

    /// <summary>BR-09: Open or InProgress requests can be cancelled; Completed and Cancelled are final.</summary>
    public void Cancel(DateTime now)
    {
        if (Status is RequestStatus.Completed or RequestStatus.Cancelled)
            throw new ConflictException($"A {Status} request cannot be cancelled.");

        Status = RequestStatus.Cancelled;
        CancelledAt = now;
    }

    /// <summary>BR-10: computed, never stored.</summary>
    public bool NeedsLove(DateTime now) =>
        Status == RequestStatus.Open && now - CreatedAt > TimeSpan.FromDays(NeedsLoveAfterDays);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

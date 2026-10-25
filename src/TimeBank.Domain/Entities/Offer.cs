using TimeBank.Domain.Common;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Entities;

/// <summary>An offer to help with a specific request. One per member per request.</summary>
public class Offer : BaseEntity
{
    private Offer()
    {
    }

    public int HelpRequestId { get; private set; }
    public HelpRequest HelpRequest { get; private set; } = null!;
    public int HelperId { get; private set; }
    public Member Helper { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public decimal Hours { get; private set; }
    public OfferStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }

    internal static Offer Create(int helpRequestId, int helperId, string message, decimal hours, DateTime now)
    {
        if (helpRequestId <= 0)
            throw new BusinessRuleException("An offer must belong to a request.");

        if (helperId <= 0)
            throw new BusinessRuleException("An offer must have a helper.");

        if (string.IsNullOrWhiteSpace(message))
            throw new BusinessRuleException("Offer message is required.");

        if (message.Trim().Length > 1000)
            throw new BusinessRuleException("Offer message cannot exceed 1000 characters.");

        HoursPolicy.EnsurePositiveHalfHour(hours, "Offer hours");

        return new Offer
        {
            HelpRequestId = helpRequestId,
            HelperId = helperId,
            Message = message.Trim(),
            Hours = hours,
            Status = OfferStatus.Pending,
            CreatedAt = now
        };
    }

    public void Accept(DateTime now)
    {
        if (Status != OfferStatus.Pending)
            throw new ConflictException($"Only a Pending offer can be accepted. Current status: {Status}.");

        Status = OfferStatus.Accepted;
        RespondedAt = now;
    }

    public void Reject(DateTime now)
    {
        if (Status != OfferStatus.Pending)
            throw new ConflictException($"Only a Pending offer can be rejected. Current status: {Status}.");

        Status = OfferStatus.Rejected;
        RespondedAt = now;
    }
}

namespace TimeBank.Domain.Events;

/// <summary>
/// BR-12: raised once when an exchange completes. BadgeService listens to this event,
/// so badge rules can evolve without changing the exchange workflow.
/// </summary>
public sealed record ExchangeCompletedDomainEvent(
    int RequestId,
    int RequesterId,
    int HelperId,
    decimal Hours,
    DateTime OccurredOn) : IDomainEvent;

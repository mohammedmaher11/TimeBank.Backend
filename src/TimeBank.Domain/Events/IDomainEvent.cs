namespace TimeBank.Domain.Events;

/// <summary>Marker for domain events raised by entities.</summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

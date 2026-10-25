using TimeBank.Domain.Events;

namespace TimeBank.Application.Abstractions.Events;

/// <summary>
/// Dispatches domain events after the business transaction has committed.
/// The badge handler is a separate subscriber (BR-12), keeping ExchangeService focused.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

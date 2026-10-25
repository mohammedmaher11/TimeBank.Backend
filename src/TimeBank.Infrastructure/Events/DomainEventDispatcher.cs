using Microsoft.Extensions.Logging;
using TimeBank.Application.Abstractions.Events;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Domain.Events;

namespace TimeBank.Infrastructure.Events;

/// <summary>
/// In-process observer. ExchangeService raises ExchangeCompleted; this dispatcher
/// forwards it to BadgeService without the exchange workflow knowing about badges.
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IBadgeService _badgeService;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IBadgeService badgeService, ILogger<DomainEventDispatcher> logger)
    {
        _badgeService = badgeService;
        _logger = logger;
    }

    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            if (domainEvent is not ExchangeCompletedDomainEvent completed)
                continue;

            try
            {
                await _badgeService.EvaluateAsync(completed, cancellationToken);
            }
            catch (Exception ex)
            {
                // The exchange is already committed. A badge failure must not turn a
                // successful transfer into an HTTP 500; it is logged for follow-up.
                _logger.LogError(
                    ex,
                    "Failed to award badges after request {RequestId} completed.",
                    completed.RequestId);
            }
        }
    }
}

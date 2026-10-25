using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Common;

/// <summary>
/// BR-01 and BR-03: time is the only currency, it is measured in hours,
/// and every amount must be a positive/negative half-hour step (never zero for ledger entries).
/// </summary>
public static class HoursPolicy
{
    public const decimal Step = 0.5m;
    public const decimal MinRequestHours = 0.5m;
    public const decimal MaxRequestHours = 8m;

    public static bool IsHalfHourStep(decimal value) => value % Step == 0m;

    public static void EnsurePositiveHalfHour(decimal hours, string field, decimal min = Step, decimal? max = null)
    {
        if (hours <= 0m)
            throw new BusinessRuleException($"{field} must be greater than zero.");

        if (!IsHalfHourStep(hours))
            throw new BusinessRuleException($"{field} must be in steps of {Step} hours.");

        if (hours < min)
            throw new BusinessRuleException($"{field} must be at least {min} hours.");

        if (max.HasValue && hours > max.Value)
            throw new BusinessRuleException($"{field} cannot exceed {max.Value} hours.");
    }

    public static void EnsureNonZeroHalfHour(decimal hours, string field)
    {
        if (hours == 0m)
            throw new BusinessRuleException($"{field} cannot be zero.");

        if (!IsHalfHourStep(hours))
            throw new BusinessRuleException($"{field} must be in steps of {Step} hours.");
    }
}

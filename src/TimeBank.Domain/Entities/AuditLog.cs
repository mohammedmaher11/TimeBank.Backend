using TimeBank.Domain.Common;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Entities;

/// <summary>BR-15: every administrative action is recorded.</summary>
public class AuditLog : BaseEntity
{
    private AuditLog()
    {
    }

    /// <summary>A member id as text, or the fixed value "BackOffice" for the MVC back-office.</summary>
    public string Actor { get; private set; } = null!;
    public AuditSource Source { get; private set; }
    public string Action { get; private set; } = null!;
    public string Target { get; private set; } = null!;
    public string? Details { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static AuditLog Create(
        string actor,
        AuditSource source,
        string action,
        string target,
        string? details,
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new BusinessRuleException("An audit actor is required.");

        if (string.IsNullOrWhiteSpace(action))
            throw new BusinessRuleException("An audit action is required.");

        if (string.IsNullOrWhiteSpace(target))
            throw new BusinessRuleException("An audit target is required.");

        return new AuditLog
        {
            Actor = actor.Trim(),
            Source = source,
            Action = action.Trim(),
            Target = target.Trim(),
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            CreatedAt = createdAt
        };
    }
}

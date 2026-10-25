using TimeBank.Domain.Common;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Entities;

/// <summary>
/// BR-04: the ledger is the truth and is append-only. This entity has no public
/// state-changing method and the repository exposes Add only (no Update/Delete).
/// </summary>
public class LedgerEntry : BaseEntity
{
    private LedgerEntry()
    {
    }

    public int MemberId { get; private set; }
    public Member Member { get; private set; } = null!;
    public decimal Hours { get; private set; }
    public LedgerEntryType Type { get; private set; }
    public string Description { get; private set; } = null!;
    public int? ReferenceId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static LedgerEntry Create(
        int memberId,
        decimal hours,
        LedgerEntryType type,
        string description,
        int? referenceId,
        DateTime createdAt)
    {
        if (memberId <= 0)
            throw new BusinessRuleException("A ledger entry must belong to a member.");

        // BR-03: positive or negative, never zero, always a half-hour step.
        HoursPolicy.EnsureNonZeroHalfHour(hours, "Ledger hours");

        if (!Enum.IsDefined(type))
            throw new BusinessRuleException("Invalid ledger entry type.");

        if (string.IsNullOrWhiteSpace(description))
            throw new BusinessRuleException("Ledger description is required.");

        var isDebit = type is LedgerEntryType.ExchangeDebit or LedgerEntryType.GiftSent;
        var isCredit = type is LedgerEntryType.WelcomeBonus or LedgerEntryType.ExchangeCredit or LedgerEntryType.GiftReceived;

        if (isDebit && hours > 0m)
            throw new BusinessRuleException("Debit entries must have negative hours.");

        if (isCredit && hours < 0m)
            throw new BusinessRuleException("Credit entries must have positive hours.");

        return new LedgerEntry
        {
            MemberId = memberId,
            Hours = hours,
            Type = type,
            Description = description.Trim(),
            ReferenceId = referenceId,
            CreatedAt = createdAt
        };
    }
}

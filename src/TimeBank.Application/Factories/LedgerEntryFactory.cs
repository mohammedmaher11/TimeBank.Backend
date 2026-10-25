using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;

namespace TimeBank.Application.Factories;

/// <summary>
/// Factory pattern: every ledger entry is created through one place, so the sign,
/// type and description rules for each case stay consistent (BR-02, BR-04, BR-08).
/// </summary>
public static class LedgerEntryFactory
{
    public const decimal WelcomeBonusHours = 2m;

    public static LedgerEntry WelcomeBonus(int memberId, DateTime now) =>
        LedgerEntry.Create(
            memberId,
            WelcomeBonusHours,
            LedgerEntryType.WelcomeBonus,
            "Welcome bonus: 2 hours to start exchanging.",
            null,
            now);

    public static LedgerEntry ExchangeDebit(int requesterId, int requestId, decimal hours, DateTime now) =>
        LedgerEntry.Create(
            requesterId,
            -hours,
            LedgerEntryType.ExchangeDebit,
            $"Exchange debit for request #{requestId}.",
            requestId,
            now);

    public static LedgerEntry ExchangeCredit(int helperId, int requestId, decimal hours, DateTime now) =>
        LedgerEntry.Create(
            helperId,
            hours,
            LedgerEntryType.ExchangeCredit,
            $"Exchange credit for request #{requestId}.",
            requestId,
            now);
}

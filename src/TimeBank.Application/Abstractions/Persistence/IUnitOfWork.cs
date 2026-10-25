using TimeBank.Application.Abstractions.Persistence;

namespace TimeBank.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    IMemberRepository Members { get; }
    ICategoryRepository Categories { get; }
    ISkillRepository Skills { get; }
    IMemberSkillRepository MemberSkills { get; }
    IHelpRequestRepository HelpRequests { get; }
    IOfferRepository Offers { get; }
    ILedgerEntryRepository LedgerEntries { get; }
    IReviewRepository Reviews { get; }
    IBadgeRepository Badges { get; }
    IAuditLogRepository AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the operation inside one database transaction. When a member id is supplied,
    /// the implementation also takes a SQL Server application lock for that member's balance,
    /// serialising concurrent accepts/completes that could otherwise overdraw the ledger.
    /// </summary>
    Task ExecuteInTransactionAsync(
        int? memberBalanceLockId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}

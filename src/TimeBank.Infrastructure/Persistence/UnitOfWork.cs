using System.Data;
using Microsoft.EntityFrameworkCore;
using TimeBank.Application.Abstractions.Persistence;

namespace TimeBank.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly TimeBankDbContext _context;

    public UnitOfWork(TimeBankDbContext context)
    {
        _context = context;
        Members = new Repositories.MemberRepository(context);
        Categories = new Repositories.CategoryRepository(context);
        Skills = new Repositories.SkillRepository(context);
        MemberSkills = new Repositories.MemberSkillRepository(context);
        HelpRequests = new Repositories.HelpRequestRepository(context);
        Offers = new Repositories.OfferRepository(context);
        LedgerEntries = new Repositories.LedgerEntryRepository(context);
        Reviews = new Repositories.ReviewRepository(context);
        Badges = new Repositories.BadgeRepository(context);
        AuditLogs = new Repositories.AuditLogRepository(context);
    }

    public IMemberRepository Members { get; }
    public ICategoryRepository Categories { get; }
    public ISkillRepository Skills { get; }
    public IMemberSkillRepository MemberSkills { get; }
    public IHelpRequestRepository HelpRequests { get; }
    public IOfferRepository Offers { get; }
    public ILedgerEntryRepository LedgerEntries { get; }
    public IReviewRepository Reviews { get; }
    public IBadgeRepository Badges { get; }
    public IAuditLogRepository AuditLogs { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public async Task ExecuteInTransactionAsync(
        int? memberBalanceLockId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            if (memberBalanceLockId.HasValue)
            {
                // Serialises balance-affecting operations for this member. This closes the
                // known limitation for two simultaneous accepts against the same balance.
                var resource = $"TimeBank:Balance:{memberBalanceLockId.Value}";
                await _context.Database.ExecuteSqlRawAsync(
                    """
                    DECLARE @result int;
                    EXEC @result = sp_getapplock
                        @Resource = {0},
                        @LockMode = 'Exclusive',
                        @LockOwner = 'Transaction',
                        @LockTimeout = 15000;
                    IF @result < 0
                        THROW 50001, 'Could not acquire the member balance lock.', 1;
                    """,
                    [resource],
                    cancellationToken);
            }

            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch
            {
                // Preserve the original exception.
            }

            throw;
        }
    }
}

using TimeBank.Domain.Common;
using TimeBank.Domain.Entities;

namespace TimeBank.Application.Abstractions.Persistence;

public interface IRepository<TEntity> where TEntity : BaseEntity
{
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Remove(TEntity entity);
}

public interface IMemberRepository : IRepository<Member>
{
    Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Member?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<List<DTOs.Members.MemberSummaryDto>> ListSummariesAsync(CancellationToken cancellationToken = default);
    Task<List<DTOs.Members.MemberProfileDto>> ListProfilesAsync(CancellationToken cancellationToken = default);
    Task<DTOs.Members.MemberProfileDto?> GetProfileAsync(int id, CancellationToken cancellationToken = default);
}

public interface ICategoryRepository : IRepository<Category>
{
    Task<List<DTOs.Categories.CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DTOs.Categories.CategoryDto?> GetDtoByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, int? excludingId = null, CancellationToken cancellationToken = default);
    Task<bool> HasRequestsAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> HasMemberSkillsAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
}

public interface ISkillRepository
{
    Task<List<int>> GetExistingIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    Task<List<DTOs.Categories.SkillDto>> GetAllAsync(CancellationToken cancellationToken = default);
}

public interface IMemberSkillRepository
{
    Task<List<MemberSkill>> GetByMemberAsync(int memberId, CancellationToken cancellationToken = default);
    Task<bool> HasSkillInCategoryAsync(int memberId, int categoryId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<MemberSkill> memberSkills, CancellationToken cancellationToken = default);
    void RemoveRange(IEnumerable<MemberSkill> memberSkills);
}

public interface IHelpRequestRepository : IRepository<HelpRequest>
{
    Task<HelpRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<HelpRequest?> GetByIdWithOffersAsync(int id, CancellationToken cancellationToken = default);
    Task<DTOs.Requests.RequestSummaryDto?> GetDetailsAsync(int id, DateTime now, CancellationToken cancellationToken = default);
    Task<List<DTOs.Requests.RequestSummaryDto>> ListAsync(
        DTOs.Requests.RequestFilter filter, DateTime now, CancellationToken cancellationToken = default);
    Task<List<DTOs.Requests.OfferDto>> GetOffersAsync(int requestId, CancellationToken cancellationToken = default);
    Task<int> CountOpenAsync(CancellationToken cancellationToken = default);
    Task<int> CountCompletedAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetTotalCompletedHoursAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetLockedHoursAsync(int memberId, CancellationToken cancellationToken = default);
    Task<int> CountCompletedAsHelperAsync(int memberId, CancellationToken cancellationToken = default);
    Task<int> CountDistinctRequestersHelpedAsync(int memberId, CancellationToken cancellationToken = default);
    Task<List<DTOs.Stats.TopHelperDto>> GetTopHelpersThisMonthAsync(
        DateTime monthStart, CancellationToken cancellationToken = default);
    Task<List<DTOs.Stats.CategoryDemandDto>> GetCategoryDemandAsync(CancellationToken cancellationToken = default);
}

public interface IOfferRepository : IRepository<Offer>
{
    Task<Offer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Offer?> GetByRequestAndHelperAsync(int requestId, int helperId, CancellationToken cancellationToken = default);
    Task<List<DTOs.Requests.OfferDto>> ListByRequestAsync(int requestId, CancellationToken cancellationToken = default);
    Task<int> CountByRequestAsync(int requestId, CancellationToken cancellationToken = default);
}

/// <summary>
/// BR-04: append-only. This interface intentionally has Add methods only — no Update, no Delete.
/// </summary>
public interface ILedgerEntryRepository
{
    Task AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default);
    Task<decimal> GetBalanceAsync(int memberId, CancellationToken cancellationToken = default);
    Task<decimal> GetExchangeCreditsAsync(int memberId, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(int memberId, CancellationToken cancellationToken = default);
    Task<DTOs.Balances.StatementDto> GetStatementAsync(
        int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public interface IReviewRepository
{
    Task AddAsync(Review review, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int requestId, int reviewerId, CancellationToken cancellationToken = default);
    Task<double?> GetAverageRatingAsync(int memberId, CancellationToken cancellationToken = default);
    Task<List<DTOs.Reviews.ReviewDto>> ListForMemberAsync(int memberId, CancellationToken cancellationToken = default);
    Task<List<DTOs.Reviews.ReviewDto>> ListForRequestAsync(int requestId, CancellationToken cancellationToken = default);
}

public interface IBadgeRepository
{
    Task AddAsync(Badge badge, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    Task<Badge?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<DTOs.Members.BadgeDto>> GetForMemberAsync(int memberId, CancellationToken cancellationToken = default);
    Task<bool> HasBadgeAsync(int memberId, int badgeId, CancellationToken cancellationToken = default);
    Task AddMemberBadgeAsync(MemberBadge memberBadge, CancellationToken cancellationToken = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    Task<List<DTOs.Admin.AuditLogDto>> ListAsync(int take, CancellationToken cancellationToken = default);
}

using TimeBank.Application.DTOs.Auth;
using TimeBank.Application.DTOs.Balances;
using TimeBank.Application.DTOs.Requests;
using TimeBank.Domain.Events;

namespace TimeBank.Application.Abstractions.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public interface IExchangeService
{
    Task<int> CreateRequestAsync(int requesterId, CreateHelpRequestRequest request, CancellationToken cancellationToken = default);
    Task<int> SendOfferAsync(int helperId, int requestId, SendOfferRequest request, CancellationToken cancellationToken = default);
    Task<RequestStatusDto> AcceptOfferAsync(int requesterId, int offerId, CancellationToken cancellationToken = default);
    Task<RequestStatusDto> CompleteAsync(int requesterId, int requestId, decimal actualHours, CancellationToken cancellationToken = default);
    Task<RequestStatusDto> CancelAsync(int requesterId, int requestId, CancellationToken cancellationToken = default);
    Task<int> AddReviewAsync(int reviewerId, int requestId, DTOs.Reviews.CreateReviewRequest request, CancellationToken cancellationToken = default);
}

public interface IBalanceService
{
    Task<BalanceDto> GetBalanceAsync(int memberId, CancellationToken cancellationToken = default);
    Task<StatementDto> GetStatementAsync(int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public interface IMemberSkillService
{
    Task SetSkillsAsync(int memberId, IReadOnlyCollection<DTOs.Auth.SkillSelectionRequest> skills, CancellationToken cancellationToken = default);
}

public interface IBadgeService
{
    Task EvaluateAsync(ExchangeCompletedDomainEvent domainEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DTOs.Members.BadgeDto>> GetForMemberAsync(int memberId, CancellationToken cancellationToken = default);
}

public interface IRequestQueryService
{
    Task<List<RequestSummaryDto>> ListAsync(RequestFilter filter, CancellationToken cancellationToken = default);
    Task<RequestSummaryDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<OfferDto>> GetOffersAsync(int requesterId, int requestId, CancellationToken cancellationToken = default);
}

public interface IMemberQueryService
{
    Task<DTOs.Members.MemberProfileDto> GetProfileAsync(int id, CancellationToken cancellationToken = default);
    Task<List<DTOs.Members.MemberDirectoryDto>> GetDirectoryAsync(CancellationToken cancellationToken = default);
    Task<DTOs.Members.MemberSummaryDto> GetMeAsync(int id, CancellationToken cancellationToken = default);
}

public interface ICategoryService
{
    Task<List<DTOs.Categories.CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DTOs.Categories.CategoryDto> CreateAsync(DTOs.Categories.CategoryCreateRequest request, CancellationToken cancellationToken = default);
    Task<DTOs.Categories.CategoryDto> UpdateAsync(int id, DTOs.Categories.CategoryUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface IStatsService
{
    Task<DTOs.Stats.StatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}

public interface IModerationService
{
    Task SuspendAsync(int memberId, string? reason, CancellationToken cancellationToken = default);
    Task ReinstateAsync(int memberId, CancellationToken cancellationToken = default);
    Task<List<DTOs.Admin.AuditLogDto>> GetAuditLogsAsync(int take, CancellationToken cancellationToken = default);
    Task<List<DTOs.Members.MemberSummaryDto>> ListMembersAsync(CancellationToken cancellationToken = default);
}

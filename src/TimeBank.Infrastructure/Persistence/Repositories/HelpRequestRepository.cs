using Microsoft.EntityFrameworkCore;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.DTOs.Requests;
using TimeBank.Application.DTOs.Stats;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;

namespace TimeBank.Infrastructure.Persistence.Repositories;

public sealed class HelpRequestRepository : Repository<HelpRequest>, IHelpRequestRepository
{
    public HelpRequestRepository(TimeBankDbContext context) : base(context)
    {
    }

    public Task<HelpRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<HelpRequest?> GetByIdWithOffersAsync(int id, CancellationToken cancellationToken = default) =>
        Set.Include(r => r.Offers).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<RequestSummaryDto?> GetDetailsAsync(
        int id, DateTime now, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(SummaryProjection(now))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<List<RequestSummaryDto>> ListAsync(
        RequestFilter filter, DateTime now, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (filter.CategoryId.HasValue)
            query = query.Where(r => r.CategoryId == filter.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(filter.District))
        {
            var district = filter.District.Trim();
            query = query.Where(r => r.District == district);
        }

        if (filter.Status.HasValue)
            query = query.Where(r => r.Status == filter.Status.Value);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(SummaryProjection(now))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OfferDto>> GetOffersAsync(int requestId, CancellationToken cancellationToken = default) =>
        await Context.Offers.AsNoTracking()
            .Where(o => o.HelpRequestId == requestId)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new OfferDto(
                o.Id,
                o.HelpRequestId,
                o.HelperId,
                o.Helper.FullName,
                o.Message,
                o.Hours,
                o.Status == OfferStatus.Accepted
                    ? "Accepted"
                    : o.Status == OfferStatus.Rejected ? "Rejected" : "Pending",
                o.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task<int> CountOpenAsync(CancellationToken cancellationToken = default) =>
        Set.CountAsync(r => r.Status == RequestStatus.Open, cancellationToken);

    public Task<int> CountCompletedAsync(CancellationToken cancellationToken = default) =>
        Set.CountAsync(r => r.Status == RequestStatus.Completed, cancellationToken);

    public Task<decimal> GetTotalCompletedHoursAsync(CancellationToken cancellationToken = default) =>
        Set.Where(r => r.Status == RequestStatus.Completed)
            .SumAsync(r => (decimal?)r.ActualHours, cancellationToken)
            .ContinueWith(t => t.Result ?? 0m, cancellationToken);

    public Task<decimal> GetLockedHoursAsync(int memberId, CancellationToken cancellationToken = default) =>
        Set.Where(r => r.RequesterId == memberId && r.Status == RequestStatus.InProgress)
            .SumAsync(r => (decimal?)r.AcceptedHours, cancellationToken)
            .ContinueWith(t => t.Result ?? 0m, cancellationToken);

    public Task<int> CountCompletedAsHelperAsync(int memberId, CancellationToken cancellationToken = default) =>
        Set.CountAsync(
            r => r.Status == RequestStatus.Completed && r.AcceptedHelperId == memberId,
            cancellationToken);

    public Task<int> CountDistinctRequestersHelpedAsync(int memberId, CancellationToken cancellationToken = default) =>
        Set.Where(r => r.Status == RequestStatus.Completed && r.AcceptedHelperId == memberId)
            .Select(r => r.RequesterId)
            .Distinct()
            .CountAsync(cancellationToken);

    public async Task<List<TopHelperDto>> GetTopHelpersThisMonthAsync(
        DateTime monthStart, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Completed &&
                        r.CompletedAt >= monthStart &&
                        r.AcceptedHelperId != null)
            .GroupBy(r => r.AcceptedHelperId!.Value)
            .Select(g => new { MemberId = g.Key, Hours = g.Sum(r => r.ActualHours ?? 0m) })
            .OrderByDescending(x => x.Hours)
            .ThenBy(x => x.MemberId)
            .Take(3)
            .Join(
                Context.Members.AsNoTracking(),
                x => x.MemberId,
                m => m.Id,
                (x, m) => new TopHelperDto(x.MemberId, m.FullName, x.Hours))
            .ToListAsync(cancellationToken);

    public async Task<List<CategoryDemandDto>> GetCategoryDemandAsync(CancellationToken cancellationToken = default) =>
        await (from r in Set.AsNoTracking()
               where r.Status == RequestStatus.Open
               join c in Context.Categories.AsNoTracking() on r.CategoryId equals c.Id
               group r by c.Name into g
               orderby g.Count() descending, g.Key
               select new CategoryDemandDto(g.Key, g.Count()))
            .Take(5)
            .ToListAsync(cancellationToken);

    private static System.Linq.Expressions.Expression<Func<HelpRequest, RequestSummaryDto>> SummaryProjection(
        DateTime now) => r => new RequestSummaryDto(
        r.Id,
        r.RequesterId,
        r.Requester.FullName,
        r.Title,
        r.Description,
        r.CategoryId,
        r.Category.Name,
        r.District,
        r.EstimatedHours,
        r.NeededBy,
        r.Status == RequestStatus.Open
            ? "Open"
            : r.Status == RequestStatus.InProgress
                ? "InProgress"
                : r.Status == RequestStatus.Completed ? "Completed" : "Cancelled",
        r.CreatedAt,
        r.Status == RequestStatus.Open && r.CreatedAt < now.AddDays(-HelpRequest.NeedsLoveAfterDays),
        r.Offers.Count,
        r.AcceptedHours,
        r.ActualHours);
}

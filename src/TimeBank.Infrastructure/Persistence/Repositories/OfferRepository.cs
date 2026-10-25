using Microsoft.EntityFrameworkCore;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.DTOs.Requests;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;

namespace TimeBank.Infrastructure.Persistence.Repositories;

public sealed class OfferRepository : Repository<Offer>, IOfferRepository
{
    public OfferRepository(TimeBankDbContext context) : base(context)
    {
    }

    public Task<Offer?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Offer?> GetByRequestAndHelperAsync(
        int requestId, int helperId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(o => o.HelpRequestId == requestId && o.HelperId == helperId, cancellationToken);

    public async Task<List<OfferDto>> ListByRequestAsync(int requestId, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
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

    public Task<int> CountByRequestAsync(int requestId, CancellationToken cancellationToken = default) =>
        Set.CountAsync(o => o.HelpRequestId == requestId, cancellationToken);
}

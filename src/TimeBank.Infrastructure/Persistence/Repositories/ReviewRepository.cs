using Microsoft.EntityFrameworkCore;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.DTOs.Reviews;
using TimeBank.Domain.Entities;

namespace TimeBank.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository : IReviewRepository
{
    private readonly TimeBankDbContext _context;

    public ReviewRepository(TimeBankDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(Review review, CancellationToken cancellationToken = default) =>
        _context.Reviews.AddAsync(review, cancellationToken).AsTask();

    public Task<bool> ExistsAsync(int requestId, int reviewerId, CancellationToken cancellationToken = default) =>
        _context.Reviews.AnyAsync(r => r.HelpRequestId == requestId && r.ReviewerId == reviewerId, cancellationToken);

    public Task<double?> GetAverageRatingAsync(int memberId, CancellationToken cancellationToken = default) =>
        _context.Reviews.AsNoTracking()
            .Where(r => r.RevieweeId == memberId)
            .AverageAsync(r => (double?)r.Rating, cancellationToken);

    public async Task<List<ReviewDto>> ListForMemberAsync(int memberId, CancellationToken cancellationToken = default) =>
        await _context.Reviews.AsNoTracking()
            .Where(r => r.RevieweeId == memberId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(
                r.Id,
                r.HelpRequestId,
                r.ReviewerId,
                r.Reviewer.FullName,
                r.RevieweeId,
                r.Reviewee.FullName,
                r.Rating,
                r.Comment,
                r.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<List<ReviewDto>> ListForRequestAsync(int requestId, CancellationToken cancellationToken = default) =>
        await _context.Reviews.AsNoTracking()
            .Where(r => r.HelpRequestId == requestId)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ReviewDto(
                r.Id,
                r.HelpRequestId,
                r.ReviewerId,
                r.Reviewer.FullName,
                r.RevieweeId,
                r.Reviewee.FullName,
                r.Rating,
                r.Comment,
                r.CreatedAt))
            .ToListAsync(cancellationToken);
}

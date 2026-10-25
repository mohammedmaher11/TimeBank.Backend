using Microsoft.EntityFrameworkCore;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.DTOs.Admin;
using TimeBank.Application.DTOs.Members;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;

namespace TimeBank.Infrastructure.Persistence.Repositories;

public sealed class BadgeRepository : IBadgeRepository
{
    private readonly TimeBankDbContext _context;

    public BadgeRepository(TimeBankDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(Badge badge, CancellationToken cancellationToken = default) =>
        _context.Badges.AddAsync(badge, cancellationToken).AsTask();

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        _context.Badges.AnyAsync(cancellationToken);

    public Task<Badge?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _context.Badges.FirstOrDefaultAsync(b => b.Code == normalized, cancellationToken);
    }

    public async Task<List<BadgeDto>> GetForMemberAsync(int memberId, CancellationToken cancellationToken = default) =>
        await _context.MemberBadges.AsNoTracking()
            .Where(mb => mb.MemberId == memberId)
            .OrderBy(mb => mb.Badge.Name)
            .Select(mb => new BadgeDto(
                mb.Badge.Code,
                mb.Badge.Name,
                mb.Badge.Icon,
                mb.Badge.Description,
                mb.EarnedAt))
            .ToListAsync(cancellationToken);

    public Task<bool> HasBadgeAsync(int memberId, int badgeId, CancellationToken cancellationToken = default) =>
        _context.MemberBadges.AnyAsync(mb => mb.MemberId == memberId && mb.BadgeId == badgeId, cancellationToken);

    public Task AddMemberBadgeAsync(MemberBadge memberBadge, CancellationToken cancellationToken = default) =>
        _context.MemberBadges.AddAsync(memberBadge, cancellationToken).AsTask();
}

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly TimeBankDbContext _context;

    public AuditLogRepository(TimeBankDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default) =>
        _context.AuditLogs.AddAsync(auditLog, cancellationToken).AsTask();

    public async Task<List<AuditLogDto>> ListAsync(int take, CancellationToken cancellationToken = default) =>
        await _context.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new AuditLogDto(
                a.Id,
                a.Actor,
                a.Source == AuditSource.Api ? "Api" : "BackOffice",
                a.Action,
                a.Target,
                a.Details,
                a.CreatedAt))
            .ToListAsync(cancellationToken);
}

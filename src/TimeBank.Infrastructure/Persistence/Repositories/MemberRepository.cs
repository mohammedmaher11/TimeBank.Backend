using Microsoft.EntityFrameworkCore;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.DTOs.Members;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;

namespace TimeBank.Infrastructure.Persistence.Repositories;

public sealed class MemberRepository : Repository<Member>, IMemberRepository
{
    public MemberRepository(TimeBankDbContext context) : base(context)
    {
    }

    public Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<Member?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(m => m.Email == email, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(m => m.Email == email, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Set.CountAsync(cancellationToken);

    public async Task<List<MemberSummaryDto>> ListSummariesAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .OrderBy(m => m.FullName)
            .Select(m => new MemberSummaryDto(
                m.Id,
                m.FullName,
                m.Email,
                m.District,
                m.PhotoPath,
                m.Role == MemberRole.Moderator ? "Moderator" : "Member",
                m.IsSuspended,
                m.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<List<MemberProfileDto>> ListProfilesAsync(CancellationToken cancellationToken = default) =>
        await ProjectProfiles(Set.AsNoTracking().OrderBy(m => m.FullName))
            .ToListAsync(cancellationToken);

    public async Task<MemberProfileDto?> GetProfileAsync(int id, CancellationToken cancellationToken = default) =>
        await ProjectProfiles(Set.AsNoTracking().Where(m => m.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<MemberProfileDto> ProjectProfiles(IQueryable<Member> query) =>
        query.Select(m => new MemberProfileDto(
            m.Id,
            m.FullName,
            m.District,
            m.Bio,
            m.PhotoPath,
            m.Role == MemberRole.Moderator ? "Moderator" : "Member",
            m.IsSuspended,
            m.CreatedAt,
            m.Skills
                .OrderBy(ms => ms.Skill.Name)
                .Select(ms => new MemberSkillDto(
                    ms.SkillId,
                    ms.Skill.Name,
                    ms.Skill.CategoryId,
                    ms.Skill.Category.Name,
                    ms.Level == SkillLevel.Expert
                        ? "Expert"
                        : ms.Level == SkillLevel.Intermediate ? "Intermediate" : "Beginner"))
                .ToList(),
            m.Badges
                .OrderBy(mb => mb.Badge.Name)
                .Select(mb => new BadgeDto(
                    mb.Badge.Code,
                    mb.Badge.Name,
                    mb.Badge.Icon,
                    mb.Badge.Description,
                    mb.EarnedAt))
                .ToList(),
            Context.Reviews
                .Where(r => r.RevieweeId == m.Id)
                .Average(r => (double?)r.Rating),
            Context.Reviews.Count(r => r.RevieweeId == m.Id),
            Context.LedgerEntries
                .Where(l => l.MemberId == m.Id && l.Type == LedgerEntryType.ExchangeCredit)
                .Sum(l => (decimal?)l.Hours) ?? 0m,
            -(Context.LedgerEntries
                .Where(l => l.MemberId == m.Id && l.Type == LedgerEntryType.ExchangeDebit)
                .Sum(l => (decimal?)l.Hours) ?? 0m)));
}

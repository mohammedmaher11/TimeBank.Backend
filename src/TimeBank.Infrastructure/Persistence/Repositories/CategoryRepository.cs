using Microsoft.EntityFrameworkCore;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.DTOs.Categories;
using TimeBank.Domain.Entities;

namespace TimeBank.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(TimeBankDbContext context) : base(context)
    {
    }

    public async Task<List<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.Skills
                    .OrderBy(s => s.Name)
                    .Select(s => new SkillDto(s.Id, s.Name, s.CategoryId, c.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);

    public async Task<CategoryDto?> GetDtoByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await Set.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.Skills
                    .OrderBy(s => s.Name)
                    .Select(s => new SkillDto(s.Id, s.Name, s.CategoryId, c.Name))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludingId = null, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        return Set.AnyAsync(
            c => c.Name == normalized && (!excludingId.HasValue || c.Id != excludingId.Value),
            cancellationToken);
    }

    public Task<bool> HasRequestsAsync(int id, CancellationToken cancellationToken = default) =>
        Context.HelpRequests.AnyAsync(r => r.CategoryId == id, cancellationToken);

    public Task<bool> HasMemberSkillsAsync(int id, CancellationToken cancellationToken = default) =>
        Context.MemberSkills.AnyAsync(ms => ms.Skill.CategoryId == id, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Set.AnyAsync(cancellationToken);
}

public sealed class SkillRepository : ISkillRepository
{
    private readonly TimeBankDbContext _context;

    public SkillRepository(TimeBankDbContext context)
    {
        _context = context;
    }

    public async Task<List<int>> GetExistingIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var requested = ids.Distinct().ToList();
        return await _context.Skills
            .AsNoTracking()
            .Where(s => requested.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Skills.AnyAsync(s => s.Id == id, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        _context.Skills.AnyAsync(cancellationToken);

    public async Task<List<SkillDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Skills.AsNoTracking()
            .OrderBy(s => s.Category.Name).ThenBy(s => s.Name)
            .Select(s => new SkillDto(s.Id, s.Name, s.CategoryId, s.Category.Name))
            .ToListAsync(cancellationToken);
}

public sealed class MemberSkillRepository : IMemberSkillRepository
{
    private readonly TimeBankDbContext _context;

    public MemberSkillRepository(TimeBankDbContext context)
    {
        _context = context;
    }

    public async Task<List<MemberSkill>> GetByMemberAsync(int memberId, CancellationToken cancellationToken = default) =>
        await _context.MemberSkills
            .Where(ms => ms.MemberId == memberId)
            .ToListAsync(cancellationToken);

    public Task<bool> HasSkillInCategoryAsync(int memberId, int categoryId, CancellationToken cancellationToken = default) =>
        _context.MemberSkills.AnyAsync(
            ms => ms.MemberId == memberId && ms.Skill.CategoryId == categoryId,
            cancellationToken);

    public Task AddRangeAsync(IEnumerable<MemberSkill> memberSkills, CancellationToken cancellationToken = default) =>
        _context.MemberSkills.AddRangeAsync(memberSkills, cancellationToken);

    public void RemoveRange(IEnumerable<MemberSkill> memberSkills) => _context.MemberSkills.RemoveRange(memberSkills);
}

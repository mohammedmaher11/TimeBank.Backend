using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TimeBank.Application.Factories;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;

namespace TimeBank.Infrastructure.Persistence.Seed;

/// <summary>
/// Loads seed-data.json only when the database is empty. Categories and skills are fixed;
/// members receive their skills, 2 welcome hours and the starter requests in one transaction.
/// </summary>
public sealed class DataSeeder
{
    private readonly TimeBankDbContext _context;

    public DataSeeder(TimeBankDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync(string seedFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(seedFilePath))
            throw new FileNotFoundException($"Seed file was not found at '{seedFilePath}'.", seedFilePath);

        if (await _context.Members.AnyAsync(cancellationToken) ||
            await _context.Categories.AnyAsync(cancellationToken) ||
            await _context.Badges.AnyAsync(cancellationToken))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(seedFilePath, cancellationToken);
        var data = JsonSerializer.Deserialize<SeedData>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("seed-data.json could not be parsed.");

        var now = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var badges = data.Badges
                .Select(b => Badge.Create(b.Code, b.Name, b.Icon, b.Description))
                .ToList();
            await _context.Badges.AddRangeAsync(badges, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var categoryMap = new Dictionary<int, Category>();
            foreach (var categorySeed in data.Categories)
            {
                var category = Category.Create(categorySeed.Name, null, now);
                await _context.Categories.AddAsync(category, cancellationToken);
                categoryMap[categorySeed.Id] = category;
            }

            await _context.SaveChangesAsync(cancellationToken);

            var skillMap = new Dictionary<int, Skill>();
            foreach (var categorySeed in data.Categories)
            {
                var category = categoryMap[categorySeed.Id];
                foreach (var skillSeed in categorySeed.Skills)
                {
                    var skill = Skill.Create(skillSeed.Name, category.Id);
                    await _context.Skills.AddAsync(skill, cancellationToken);
                    skillMap[skillSeed.Id] = skill;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(data.DevPassword, workFactor: 12);
            var memberMap = new Dictionary<int, Member>();

            foreach (var memberSeed in data.Members)
            {
                var role = Enum.TryParse<MemberRole>(memberSeed.Role, true, out var parsedRole)
                    ? parsedRole
                    : MemberRole.Member;

                var member = Member.Create(
                    memberSeed.FullName,
                    memberSeed.Email,
                    passwordHash,
                    memberSeed.District,
                    memberSeed.Bio,
                    null,
                    role,
                    now.AddDays(-45));

                await _context.Members.AddAsync(member, cancellationToken);
                memberMap[memberSeed.Id] = member;
            }

            await _context.SaveChangesAsync(cancellationToken);

            foreach (var memberSeed in data.Members)
            {
                var member = memberMap[memberSeed.Id];

                foreach (var skillSeed in memberSeed.Skills)
                {
                    if (!skillMap.TryGetValue(skillSeed.SkillId, out var skill))
                        continue;

                    var level = Enum.TryParse<SkillLevel>(skillSeed.Level, true, out var parsedLevel)
                        ? parsedLevel
                        : SkillLevel.Beginner;

                    await _context.MemberSkills.AddAsync(
                        MemberSkill.Create(member.Id, skill.Id, level), cancellationToken);
                }

                await _context.LedgerEntries.AddAsync(
                    LedgerEntryFactory.WelcomeBonus(member.Id, member.CreatedAt), cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var requestIndex = 0;
            foreach (var requestSeed in data.HelpRequests)
            {
                if (!memberMap.TryGetValue(requestSeed.RequesterId, out var requester) ||
                    !categoryMap.TryGetValue(requestSeed.CategoryId, out var category))
                    continue;

                var neededBy = DateOnly.Parse(requestSeed.NeededBy, CultureInfo.InvariantCulture);
                var createdAt = now.AddDays(requestIndex < 3 ? -20 + requestIndex : -7 + requestIndex);

                var request = HelpRequest.Create(
                    requester.Id,
                    requestSeed.Title,
                    requestSeed.Description,
                    category.Id,
                    requestSeed.District,
                    requestSeed.EstimatedHours,
                    neededBy,
                    createdAt);

                await _context.HelpRequests.AddAsync(request, cancellationToken);
                requestIndex++;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private sealed class SeedData
    {
        public string DevPassword { get; set; } = "Sa3a@2026";
        public List<BadgeSeed> Badges { get; set; } = [];
        public List<CategorySeed> Categories { get; set; } = [];
        public List<MemberSeed> Members { get; set; } = [];
        public List<HelpRequestSeed> HelpRequests { get; set; } = [];
    }

    private sealed class BadgeSeed
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private sealed class CategorySeed
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<SkillSeed> Skills { get; set; } = [];
    }

    private sealed class SkillSeed
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class MemberSeed
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string Role { get; set; } = "Member";
        public List<MemberSkillSeed> Skills { get; set; } = [];
    }

    private sealed class MemberSkillSeed
    {
        public int SkillId { get; set; }
        public string Level { get; set; } = "Beginner";
    }

    private sealed class HelpRequestSeed
    {
        public int Id { get; set; }
        public int RequesterId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string District { get; set; } = string.Empty;
        public decimal EstimatedHours { get; set; }
        public string NeededBy { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
    }
}

using TimeBank.Domain.Common;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Entities;

/// <summary>
/// A person on the platform. State governed by business rules is changed only through methods.
/// BR-14: suspension is checked against the current database state, never a JWT claim.
/// </summary>
public class Member : BaseEntity
{
    private readonly List<MemberSkill> _skills = [];
    private readonly List<MemberBadge> _badges = [];
    private readonly List<HelpRequest> _requests = [];
    private readonly List<Offer> _offers = [];
    private readonly List<LedgerEntry> _ledgerEntries = [];

    private Member()
    {
        // EF Core materialization only.
    }

    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string District { get; private set; } = null!;
    public string? Bio { get; private set; }
    public string? PhotoPath { get; private set; }
    public MemberRole Role { get; private set; }
    public bool IsSuspended { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<MemberSkill> Skills => _skills;
    public IReadOnlyCollection<MemberBadge> Badges => _badges;
    public IReadOnlyCollection<HelpRequest> Requests => _requests;
    public IReadOnlyCollection<Offer> Offers => _offers;
    public IReadOnlyCollection<LedgerEntry> LedgerEntries => _ledgerEntries;

    public static Member Create(
        string fullName,
        string email,
        string passwordHash,
        string district,
        string? bio,
        string? photoPath,
        MemberRole role,
        DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new BusinessRuleException("Full name is required.");

        if (fullName.Trim().Length > 120)
            throw new BusinessRuleException("Full name cannot exceed 120 characters.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new BusinessRuleException("A valid email address is required.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new BusinessRuleException("Password hash is required.");

        if (string.IsNullOrWhiteSpace(district))
            throw new BusinessRuleException("District is required.");

        if (district.Trim().Length > 100)
            throw new BusinessRuleException("District cannot exceed 100 characters.");

        if (bio is { Length: > 1000 })
            throw new BusinessRuleException("Bio cannot exceed 1000 characters.");

        return new Member
        {
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            District = district.Trim(),
            Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim(),
            PhotoPath = string.IsNullOrWhiteSpace(photoPath) ? null : photoPath.Trim(),
            Role = role,
            CreatedAt = createdAt
        };
    }

    public void UpdateProfile(string fullName, string district, string? bio, string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new BusinessRuleException("Full name is required.");

        if (string.IsNullOrWhiteSpace(district))
            throw new BusinessRuleException("District is required.");

        FullName = fullName.Trim();
        District = district.Trim();
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        if (!string.IsNullOrWhiteSpace(photoPath))
            PhotoPath = photoPath.Trim();
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new BusinessRuleException("Password hash is required.");

        PasswordHash = passwordHash;
    }

    /// <summary>BR-14: refuse actions for suspended members using the current database state.</summary>
    public void EnsureActive()
    {
        if (IsSuspended)
            throw new ForbiddenException("This member is suspended and cannot perform this action.");
    }

    public void Suspend()
    {
        if (IsSuspended)
            throw new ConflictException("This member is already suspended.");

        IsSuspended = true;
    }

    public void Reinstate()
    {
        if (!IsSuspended)
            throw new ConflictException("This member is not suspended.");

        IsSuspended = false;
    }

    public bool IsModerator => Role == MemberRole.Moderator;
}

using TimeBank.Domain.Common;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Entities;

/// <summary>A medal a member earns automatically (BR-12).</summary>
public class Badge : BaseEntity
{
    private readonly List<MemberBadge> _memberBadges = [];

    private Badge()
    {
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Icon { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    public IReadOnlyCollection<MemberBadge> MemberBadges => _memberBadges;

    public static Badge Create(string code, string name, string icon, string description)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new BusinessRuleException("Badge code is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Badge name is required.");

        return new Badge
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Icon = icon.Trim(),
            Description = description.Trim()
        };
    }
}

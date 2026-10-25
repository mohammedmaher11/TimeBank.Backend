using TimeBank.Domain.Common;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Entities;

public class Category : BaseEntity
{
    private readonly List<Skill> _skills = [];
    private readonly List<HelpRequest> _requests = [];

    private Category()
    {
    }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<Skill> Skills => _skills;
    public IReadOnlyCollection<HelpRequest> Requests => _requests;

    public static Category Create(string name, string? description, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Category name is required.");

        if (name.Trim().Length > 100)
            throw new BusinessRuleException("Category name cannot exceed 100 characters.");

        if (description is { Length: > 500 })
            throw new BusinessRuleException("Category description cannot exceed 500 characters.");

        return new Category
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = createdAt
        };
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Category name is required.");

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}

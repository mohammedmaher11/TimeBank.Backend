using TimeBank.Domain.Common;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Entities;

/// <summary>
/// The skills list is fixed and comes from seed data. Members pick existing skills only.
/// </summary>
public class Skill : BaseEntity
{
    private Skill()
    {
    }

    public string Name { get; private set; } = null!;
    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public static Skill Create(string name, int categoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Skill name is required.");

        if (categoryId <= 0)
            throw new BusinessRuleException("A skill must belong to a category.");

        return new Skill { Name = name.Trim(), CategoryId = categoryId };
    }
}

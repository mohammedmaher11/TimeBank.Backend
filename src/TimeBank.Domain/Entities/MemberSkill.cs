using TimeBank.Domain.Common;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Entities;

/// <summary>Many-to-many with payload: a member has an existing skill at a level.</summary>
public class MemberSkill
{
    private MemberSkill()
    {
    }

    public int MemberId { get; private set; }
    public Member Member { get; private set; } = null!;
    public int SkillId { get; private set; }
    public Skill Skill { get; private set; } = null!;
    public SkillLevel Level { get; private set; }

    public static MemberSkill Create(int memberId, int skillId, SkillLevel level)
    {
        if (memberId <= 0)
            throw new BusinessRuleException("A member is required for a member skill.");

        if (skillId <= 0)
            throw new BusinessRuleException("A skill is required for a member skill.");

        if (!Enum.IsDefined(level))
            throw new BusinessRuleException("Invalid skill level.");

        return new MemberSkill { MemberId = memberId, SkillId = skillId, Level = level };
    }

    public void ChangeLevel(SkillLevel level)
    {
        if (!Enum.IsDefined(level))
            throw new BusinessRuleException("Invalid skill level.");

        Level = level;
    }
}

using TimeBank.Domain.Common;

namespace TimeBank.Domain.Entities;

/// <summary>Join entity: each badge is awarded once per member.</summary>
public class MemberBadge
{
    private MemberBadge()
    {
    }

    public int MemberId { get; private set; }
    public Member Member { get; private set; } = null!;
    public int BadgeId { get; private set; }
    public Badge Badge { get; private set; } = null!;
    public DateTime EarnedAt { get; private set; }

    public static MemberBadge Create(int memberId, int badgeId, DateTime earnedAt)
    {
        return new MemberBadge { MemberId = memberId, BadgeId = badgeId, EarnedAt = earnedAt };
    }
}

using TimeBank.Domain.Common;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Domain.Entities;

/// <summary>BR-11: one review per reviewer per request, rating 1-5, comment up to 300 characters.</summary>
public class Review : BaseEntity
{
    private Review()
    {
    }

    public int HelpRequestId { get; private set; }
    public HelpRequest HelpRequest { get; private set; } = null!;
    public int ReviewerId { get; private set; }
    public Member Reviewer { get; private set; } = null!;
    public int RevieweeId { get; private set; }
    public Member Reviewee { get; private set; } = null!;
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Review Create(
        int helpRequestId,
        int reviewerId,
        int revieweeId,
        int rating,
        string? comment,
        DateTime createdAt)
    {
        if (helpRequestId <= 0)
            throw new BusinessRuleException("A review must belong to a request.");

        if (reviewerId == revieweeId)
            throw new BusinessRuleException("A member cannot review themselves.");

        if (rating is < 1 or > 5)
            throw new BusinessRuleException("Rating must be between 1 and 5.");

        if (comment is { Length: > 300 })
            throw new BusinessRuleException("Review comment cannot exceed 300 characters.");

        return new Review
        {
            HelpRequestId = helpRequestId,
            ReviewerId = reviewerId,
            RevieweeId = revieweeId,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = createdAt
        };
    }
}

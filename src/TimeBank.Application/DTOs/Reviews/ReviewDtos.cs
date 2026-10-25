using System.ComponentModel.DataAnnotations;

namespace TimeBank.Application.DTOs.Reviews;

public sealed class CreateReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(300)]
    public string? Comment { get; set; }
}

public sealed record ReviewDto(
    int Id,
    int RequestId,
    int ReviewerId,
    string ReviewerName,
    int RevieweeId,
    string RevieweeName,
    int Rating,
    string? Comment,
    DateTime CreatedAt);

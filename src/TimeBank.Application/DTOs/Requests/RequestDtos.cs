using System.ComponentModel.DataAnnotations;
using TimeBank.Domain.Enums;

namespace TimeBank.Application.DTOs.Requests;

public sealed class CreateHelpRequestRequest
{
    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    [Required, MaxLength(100)]
    public string District { get; set; } = string.Empty;

    [Range(0.5, 8.0)]
    public decimal EstimatedHours { get; set; }

    public DateOnly NeededBy { get; set; }
}

public sealed class SendOfferRequest
{
    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Range(0.5, 8.0)]
    public decimal Hours { get; set; }
}

public sealed class CompleteHelpRequestRequest
{
    [Range(0.5, 8.0)]
    public decimal ActualHours { get; set; }
}

public sealed record RequestFilter(int? CategoryId, string? District, RequestStatus? Status);

public sealed record RequestSummaryDto(
    int Id,
    int RequesterId,
    string RequesterName,
    string Title,
    string Description,
    int CategoryId,
    string CategoryName,
    string District,
    decimal EstimatedHours,
    DateOnly NeededBy,
    string Status,
    DateTime CreatedAt,
    bool NeedsLove,
    int OfferCount,
    decimal? AcceptedHours,
    decimal? ActualHours);

public sealed record OfferDto(
    int Id,
    int RequestId,
    int HelperId,
    string HelperName,
    string Message,
    decimal Hours,
    string Status,
    DateTime CreatedAt);

public sealed record RequestStatusDto(
    int Id,
    string Status,
    int? AcceptedOfferId,
    decimal? AcceptedHours,
    decimal? ActualHours);

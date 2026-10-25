using TimeBank.Domain.Enums;

namespace TimeBank.Application.DTOs.Members;

public sealed record MemberSummaryDto(
    int Id,
    string FullName,
    string Email,
    string District,
    string? PhotoPath,
    string Role,
    bool IsSuspended,
    DateTime CreatedAt);

public sealed record MemberDirectoryDto(
    int Id,
    string FullName,
    string District,
    string? PhotoPath,
    IReadOnlyList<MemberSkillDto> Skills,
    IReadOnlyList<BadgeDto> Badges,
    double? AverageRating);

public sealed record MemberSkillDto(int SkillId, string SkillName, int CategoryId, string CategoryName, string Level);

public sealed record MemberProfileDto(
    int Id,
    string FullName,
    string District,
    string? Bio,
    string? PhotoPath,
    string Role,
    bool IsSuspended,
    DateTime CreatedAt,
    IReadOnlyList<MemberSkillDto> Skills,
    IReadOnlyList<BadgeDto> Badges,
    double? AverageRating,
    int ReviewCount,
    decimal HoursGiven,
    decimal HoursReceived);

public sealed record BadgeDto(string Code, string Name, string Icon, string Description, DateTime EarnedAt);

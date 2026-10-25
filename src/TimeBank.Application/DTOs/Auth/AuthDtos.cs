using System.ComponentModel.DataAnnotations;
using TimeBank.Application.DTOs.Members;
using TimeBank.Domain.Enums;

namespace TimeBank.Application.DTOs.Auth;

public sealed class SkillSelectionRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid skill id is required.")]
    public int SkillId { get; set; }

    [Required]
    public SkillLevel Level { get; set; }
}

public sealed class RegisterRequest
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string District { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Bio { get; set; }

    [MaxLength(500)]
    public string? PhotoPath { get; set; }

    [Required]
    public List<SkillSelectionRequest> Skills { get; set; } = [];
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed record AuthResponse(string Token, DateTime ExpiresAt, MemberSummaryDto Member);

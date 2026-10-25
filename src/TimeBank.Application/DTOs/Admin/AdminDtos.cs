using System.ComponentModel.DataAnnotations;

namespace TimeBank.Application.DTOs.Admin;

public sealed class SuspendMemberRequest
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public sealed record AuditLogDto(
    int Id,
    string Actor,
    string Source,
    string Action,
    string Target,
    string? Details,
    DateTime CreatedAt);

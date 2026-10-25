using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeBank.API.Filters;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Admin;
using TimeBank.Application.DTOs.Members;

namespace TimeBank.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Moderator")]
public sealed class AdminController : ControllerBase
{
    private readonly IModerationService _moderationService;

    public AdminController(IModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [HttpGet("members")]
    [ProducesResponseType(typeof(List<MemberSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MemberSummaryDto>>> GetMembers(CancellationToken cancellationToken) =>
        Ok(await _moderationService.ListMembersAsync(cancellationToken));

    [HttpPut("members/{id:int}/suspend")]
    [Audit("SuspendMember", "Member")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(
        int id, [FromBody] SuspendMemberRequest request, CancellationToken cancellationToken)
    {
        await _moderationService.SuspendAsync(id, request.Reason, cancellationToken);
        return NoContent();
    }

    [HttpPut("members/{id:int}/reinstate")]
    [Audit("ReinstateMember", "Member")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reinstate(int id, CancellationToken cancellationToken)
    {
        await _moderationService.ReinstateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(List<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuditLogDto>>> GetAuditLogs(
        [FromQuery] int take, CancellationToken cancellationToken) =>
        Ok(await _moderationService.GetAuditLogsAsync(take <= 0 ? 50 : take, cancellationToken));
}

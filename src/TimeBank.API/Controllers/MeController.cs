using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeBank.API.Services;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Auth;
using TimeBank.Application.DTOs.Balances;
using TimeBank.Application.DTOs.Members;

namespace TimeBank.API.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController : ControllerBase
{
    private readonly IBalanceService _balanceService;
    private readonly IMemberSkillService _memberSkillService;
    private readonly IMemberQueryService _memberQueryService;
    private readonly IDateTimeProvider _clock;

    public MeController(
        IBalanceService balanceService,
        IMemberSkillService memberSkillService,
        IMemberQueryService memberQueryService,
        IDateTimeProvider clock)
    {
        _balanceService = balanceService;
        _memberSkillService = memberSkillService;
        _memberQueryService = memberQueryService;
        _clock = clock;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MemberSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MemberSummaryDto>> GetMe(CancellationToken cancellationToken) =>
        Ok(await _memberQueryService.GetMeAsync(User.GetMemberId(), cancellationToken));

    [HttpGet("balance")]
    [ProducesResponseType(typeof(BalanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BalanceDto>> GetBalance(CancellationToken cancellationToken) =>
        Ok(await _balanceService.GetBalanceAsync(User.GetMemberId(), cancellationToken));

    [HttpGet("statement")]
    [ProducesResponseType(typeof(StatementDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatementDto>> GetStatement(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var start = from ?? new DateOnly(2000, 1, 1);
        var end = to ?? DateOnly.FromDateTime(_clock.UtcNow);
        return Ok(await _balanceService.GetStatementAsync(User.GetMemberId(), start, end, cancellationToken));
    }

    [HttpPut("skills")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetSkills(
        [FromBody] List<SkillSelectionRequest> skills, CancellationToken cancellationToken)
    {
        await _memberSkillService.SetSkillsAsync(User.GetMemberId(), skills, cancellationToken);
        return NoContent();
    }
}

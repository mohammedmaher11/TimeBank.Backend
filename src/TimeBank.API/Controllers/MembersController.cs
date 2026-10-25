using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Members;

namespace TimeBank.API.Controllers;

[ApiController]
[Route("api/members")]
public sealed class MembersController : ControllerBase
{
    private readonly IMemberQueryService _memberQueryService;

    public MembersController(IMemberQueryService memberQueryService)
    {
        _memberQueryService = memberQueryService;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<MemberDirectoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MemberDirectoryDto>>> GetDirectory(CancellationToken cancellationToken) =>
        Ok(await _memberQueryService.GetDirectoryAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MemberProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MemberProfileDto>> GetProfile(int id, CancellationToken cancellationToken) =>
        Ok(await _memberQueryService.GetProfileAsync(id, cancellationToken));
}

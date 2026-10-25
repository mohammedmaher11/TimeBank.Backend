using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeBank.API.Services;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Requests;

namespace TimeBank.API.Controllers;

[ApiController]
[Route("api/offers")]
[Authorize]
public sealed class OffersController : ControllerBase
{
    private readonly IExchangeService _exchangeService;

    public OffersController(IExchangeService exchangeService)
    {
        _exchangeService = exchangeService;
    }

    [HttpPost("{id:int}/accept")]
    [ProducesResponseType(typeof(RequestStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestStatusDto>> Accept(int id, CancellationToken cancellationToken) =>
        Ok(await _exchangeService.AcceptOfferAsync(User.GetMemberId(), id, cancellationToken));
}

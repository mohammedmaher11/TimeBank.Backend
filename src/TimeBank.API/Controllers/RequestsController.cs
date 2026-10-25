using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeBank.API.Services;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Requests;
using TimeBank.Application.DTOs.Reviews;
using TimeBank.Domain.Enums;

namespace TimeBank.API.Controllers;

[ApiController]
[Route("api/requests")]
public sealed class RequestsController : ControllerBase
{
    private readonly IExchangeService _exchangeService;
    private readonly IRequestQueryService _queryService;

    public RequestsController(IExchangeService exchangeService, IRequestQueryService queryService)
    {
        _exchangeService = exchangeService;
        _queryService = queryService;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RequestSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RequestSummaryDto>>> GetAll(
        [FromQuery] int? categoryId,
        [FromQuery] string? district,
        [FromQuery] RequestStatus? status,
        CancellationToken cancellationToken) =>
        Ok(await _queryService.ListAsync(new RequestFilter(categoryId, district, status), cancellationToken));

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RequestSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestSummaryDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _queryService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(RequestSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RequestSummaryDto>> Create(
        [FromBody] CreateHelpRequestRequest request, CancellationToken cancellationToken)
    {
        var id = await _exchangeService.CreateRequestAsync(User.GetMemberId(), request, cancellationToken);
        var created = await _queryService.GetByIdAsync(id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, created);
    }

    [HttpPost("{id:int}/offers")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SendOffer(
        int id, [FromBody] SendOfferRequest request, CancellationToken cancellationToken)
    {
        var offerId = await _exchangeService.SendOfferAsync(User.GetMemberId(), id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { id = offerId });
    }

    [HttpGet("{id:int}/offers")]
    [Authorize]
    [ProducesResponseType(typeof(List<OfferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<OfferDto>>> GetOffers(int id, CancellationToken cancellationToken) =>
        Ok(await _queryService.GetOffersAsync(User.GetMemberId(), id, cancellationToken));

    [HttpPost("{id:int}/complete")]
    [Authorize]
    [ProducesResponseType(typeof(RequestStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestStatusDto>> Complete(
        int id, [FromBody] CompleteHelpRequestRequest request, CancellationToken cancellationToken) =>
        Ok(await _exchangeService.CompleteAsync(User.GetMemberId(), id, request.ActualHours, cancellationToken));

    [HttpPost("{id:int}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(RequestStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestStatusDto>> Cancel(int id, CancellationToken cancellationToken) =>
        Ok(await _exchangeService.CancelAsync(User.GetMemberId(), id, cancellationToken));

    [HttpPost("{id:int}/reviews")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddReview(
        int id, [FromBody] CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var reviewId = await _exchangeService.AddReviewAsync(User.GetMemberId(), id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { id = reviewId });
    }
}

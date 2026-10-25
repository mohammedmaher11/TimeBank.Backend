using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Requests;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Application.Services;

public sealed class RequestQueryService : IRequestQueryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public RequestQueryService(IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<List<RequestSummaryDto>> ListAsync(RequestFilter filter, CancellationToken cancellationToken = default) =>
        _unitOfWork.HelpRequests.ListAsync(filter, _clock.UtcNow, cancellationToken);

    public async Task<RequestSummaryDto> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _unitOfWork.HelpRequests.GetDetailsAsync(id, _clock.UtcNow, cancellationToken)
        ?? throw new NotFoundException("Request was not found.");

    public async Task<List<OfferDto>> GetOffersAsync(
        int requesterId, int requestId, CancellationToken cancellationToken = default)
    {
        var helpRequest = await _unitOfWork.HelpRequests.GetByIdAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Request was not found.");

        if (helpRequest.RequesterId != requesterId)
            throw new ForbiddenException("Only the request owner can see its offers.");

        return await _unitOfWork.HelpRequests.GetOffersAsync(requestId, cancellationToken);
    }
}

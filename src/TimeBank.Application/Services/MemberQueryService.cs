using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Members;
using TimeBank.Application.Mapping;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Application.Services;

public sealed class MemberQueryService : IMemberQueryService
{
    private readonly IUnitOfWork _unitOfWork;

    public MemberQueryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<MemberProfileDto> GetProfileAsync(int id, CancellationToken cancellationToken = default) =>
        await _unitOfWork.Members.GetProfileAsync(id, cancellationToken)
        ?? throw new NotFoundException("Member was not found.");

    public async Task<List<MemberDirectoryDto>> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await _unitOfWork.Members.ListProfilesAsync(cancellationToken);
        return profiles.Select(p => p.ToDirectory()).ToList();
    }

    public async Task<MemberSummaryDto> GetMeAsync(int id, CancellationToken cancellationToken = default)
    {
        var member = await _unitOfWork.Members.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Member was not found.");

        return member.ToSummary();
    }
}

using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Admin;
using TimeBank.Application.DTOs.Members;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Application.Services;

public sealed class ModerationService : IModerationService
{
    private readonly IUnitOfWork _unitOfWork;

    public ModerationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>BR-14: suspension takes effect immediately because every action re-checks the database.</summary>
    public async Task SuspendAsync(int memberId, string? reason, CancellationToken cancellationToken = default)
    {
        var member = await _unitOfWork.Members.GetByIdAsync(memberId, cancellationToken)
            ?? throw new NotFoundException("Member was not found.");

        member.Suspend();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ReinstateAsync(int memberId, CancellationToken cancellationToken = default)
    {
        var member = await _unitOfWork.Members.GetByIdAsync(memberId, cancellationToken)
            ?? throw new NotFoundException("Member was not found.");

        member.Reinstate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<List<AuditLogDto>> GetAuditLogsAsync(int take, CancellationToken cancellationToken = default) =>
        _unitOfWork.AuditLogs.ListAsync(Math.Clamp(take, 1, 200), cancellationToken);

    public Task<List<MemberSummaryDto>> ListMembersAsync(CancellationToken cancellationToken = default) =>
        _unitOfWork.Members.ListSummariesAsync(cancellationToken);
}

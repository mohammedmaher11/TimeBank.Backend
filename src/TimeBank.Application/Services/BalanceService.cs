using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Balances;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Application.Services;

public sealed class BalanceService : IBalanceService
{
    private readonly IUnitOfWork _unitOfWork;

    public BalanceService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>BR-04/BR-05: balance is the ledger sum; locked is the accepted hours of InProgress requests.</summary>
    public async Task<BalanceDto> GetBalanceAsync(int memberId, CancellationToken cancellationToken = default)
    {
        var balance = await _unitOfWork.LedgerEntries.GetBalanceAsync(memberId, cancellationToken);
        var locked = await _unitOfWork.HelpRequests.GetLockedHoursAsync(memberId, cancellationToken);
        return new BalanceDto(balance, locked, balance - locked);
    }

    public async Task<StatementDto> GetStatementAsync(
        int memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (from > to)
            throw new BusinessRuleException("The statement start date cannot be after the end date.");

        return await _unitOfWork.LedgerEntries.GetStatementAsync(memberId, from, to, cancellationToken);
    }
}

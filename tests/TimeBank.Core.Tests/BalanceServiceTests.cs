using Moq;
using TimeBank.Application.DTOs.Balances;
using TimeBank.Application.Services;
using TimeBank.Core.Tests.TestSupport;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Core.Tests;

public sealed class BalanceServiceTests
{
    private readonly UnitOfWorkMock _uow = new();

    private BalanceService CreateService() => new(_uow.UnitOfWork.Object);

    [Fact]
    public async Task GetBalance_ReturnsAvailableAsBalanceMinusLocked()
    {
        _uow.LedgerEntries
            .Setup(r => r.GetBalanceAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5.5m);
        _uow.HelpRequests
            .Setup(r => r.GetLockedHoursAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2m);

        var result = await CreateService().GetBalanceAsync(7);

        Assert.Equal(5.5m, result.Balance);
        Assert.Equal(2m, result.Locked);
        Assert.Equal(3.5m, result.Available);
    }

    [Fact]
    public async Task GetBalance_WithNoLedgerEntries_ReturnsZero()
    {
        _uow.LedgerEntries
            .Setup(r => r.GetBalanceAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _uow.HelpRequests
            .Setup(r => r.GetLockedHoursAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var result = await CreateService().GetBalanceAsync(8);

        Assert.Equal(0m, result.Balance);
        Assert.Equal(0m, result.Locked);
        Assert.Equal(0m, result.Available);
    }

    [Fact]
    public async Task GetStatement_WhenFromIsAfterTo_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().GetStatementAsync(
                1,
                new DateOnly(2026, 12, 31),
                new DateOnly(2026, 1, 1)));

        _uow.LedgerEntries.Verify(
            r => r.GetStatementAsync(
                It.IsAny<int>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetStatement_DelegatesToRepository()
    {
        var expected = new StatementDto(
            1,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            2m,
            [new StatementEntryDto(1, TestFactory.Now, "Welcome", "WelcomeBonus", 2m, 2m)],
            2m);

        _uow.LedgerEntries
            .Setup(r => r.GetStatementAsync(
                1,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateService().GetStatementAsync(
            1,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        Assert.Same(expected, result);
    }
}

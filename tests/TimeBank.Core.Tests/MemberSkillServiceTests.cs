using Moq;
using TimeBank.Application.DTOs.Auth;
using TimeBank.Application.Services;
using TimeBank.Core.Tests.TestSupport;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Core.Tests;

public sealed class MemberSkillServiceTests
{
    private readonly UnitOfWorkMock _uow = new();

    private MemberSkillService CreateService() => new(_uow.UnitOfWork.Object);

    private static List<SkillSelectionRequest> Selection(params (int SkillId, SkillLevel Level)[] items) =>
        items.Select(i => new SkillSelectionRequest { SkillId = i.SkillId, Level = i.Level }).ToList();

    [Fact]
    public async Task SetSkills_WithUnknownSkillId_ThrowsBusinessRuleException()
    {
        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1));
        _uow.Skills
            .Setup(r => r.GetExistingIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1]);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().SetSkillsAsync(
                1,
                Selection((1, SkillLevel.Beginner), (99, SkillLevel.Expert))));

        Assert.Contains("99", exception.Message);
        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetSkills_WithDuplicateSkillIds_ThrowsBusinessRuleException()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().SetSkillsAsync(
                1,
                Selection((3, SkillLevel.Beginner), (3, SkillLevel.Expert))));

        Assert.Contains("only be selected once", exception.Message);
        _uow.Members.Verify(
            r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetSkills_WithEmptySelection_ThrowsBusinessRuleException()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().SetSkillsAsync(1, []));
    }

    [Fact]
    public async Task SetSkills_WhenMemberIsSuspended_ThrowsForbiddenException()
    {
        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1, suspended: true));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => CreateService().SetSkillsAsync(1, Selection((1, SkillLevel.Beginner))));
    }

    [Fact]
    public async Task SetSkills_ReplacesLinksAddsNewOnesAndChangesLevels()
    {
        var existingSkillOne = MemberSkill.Create(1, 1, SkillLevel.Beginner);
        var existingSkillTwo = MemberSkill.Create(1, 2, SkillLevel.Beginner);

        _uow.Members.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestFactory.CreateMember(1));
        _uow.Skills
            .Setup(r => r.GetExistingIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([2, 3]);
        _uow.MemberSkills
            .Setup(r => r.GetByMemberAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingSkillOne, existingSkillTwo]);

        List<MemberSkill>? removed = null;
        List<MemberSkill>? added = null;

        _uow.MemberSkills
            .Setup(r => r.RemoveRange(It.IsAny<IEnumerable<MemberSkill>>()))
            .Callback<IEnumerable<MemberSkill>>(links => removed = links.ToList());
        _uow.MemberSkills
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<MemberSkill>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<MemberSkill>, CancellationToken>((links, _) => added = links.ToList())
            .Returns(Task.CompletedTask);

        await CreateService().SetSkillsAsync(
            1,
            Selection((2, SkillLevel.Expert), (3, SkillLevel.Intermediate)));

        Assert.NotNull(removed);
        Assert.Single(removed!);
        Assert.Equal(1, removed[0].SkillId);

        Assert.NotNull(added);
        Assert.Single(added!);
        Assert.Equal(3, added[0].SkillId);
        Assert.Equal(SkillLevel.Intermediate, added[0].Level);

        Assert.Equal(SkillLevel.Expert, existingSkillTwo.Level);
        _uow.UnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

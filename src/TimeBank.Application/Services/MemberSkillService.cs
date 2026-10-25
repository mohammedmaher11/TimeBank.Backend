using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Auth;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Exceptions;

namespace TimeBank.Application.Services;

public sealed class MemberSkillService : IMemberSkillService
{
    private readonly IUnitOfWork _unitOfWork;

    public MemberSkillService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Replaces the member's links to existing skills (add/remove/change level).
    /// Unknown skill ids are rejected; members can never create skills (BR-06 support).
    /// </summary>
    public async Task SetSkillsAsync(
        int memberId,
        IReadOnlyCollection<SkillSelectionRequest> skills,
        CancellationToken cancellationToken = default)
    {
        if (skills is null || skills.Count == 0)
            throw new BusinessRuleException("Choose at least one skill.");

        if (skills.GroupBy(s => s.SkillId).Any(g => g.Count() > 1))
            throw new BusinessRuleException("A skill can only be selected once.");

        var member = await _unitOfWork.Members.GetByIdAsync(memberId, cancellationToken)
            ?? throw new NotFoundException("Member was not found.");

        member.EnsureActive();

        var selectedIds = skills.Select(s => s.SkillId).Distinct().ToList();
        var existingSkillIds = await _unitOfWork.Skills.GetExistingIdsAsync(selectedIds, cancellationToken);
        var missing = selectedIds.Except(existingSkillIds).ToList();

        if (missing.Count > 0)
            throw new BusinessRuleException($"Skill id(s) {string.Join(", ", missing)} do not exist.");

        var current = await _unitOfWork.MemberSkills.GetByMemberAsync(memberId, cancellationToken);
        var selectedSet = selectedIds.ToHashSet();

        var toRemove = current.Where(link => !selectedSet.Contains(link.SkillId)).ToList();
        if (toRemove.Count > 0)
            _unitOfWork.MemberSkills.RemoveRange(toRemove);

        var newLinks = new List<MemberSkill>();
        foreach (var selection in skills)
        {
            var existing = current.FirstOrDefault(link => link.SkillId == selection.SkillId);
            if (existing is not null)
            {
                existing.ChangeLevel(selection.Level);
            }
            else
            {
                newLinks.Add(MemberSkill.Create(memberId, selection.SkillId, selection.Level));
            }
        }

        if (newLinks.Count > 0)
            await _unitOfWork.MemberSkills.AddRangeAsync(newLinks, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

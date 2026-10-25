using TimeBank.Application.DTOs.Members;
using TimeBank.Application.DTOs.Requests;
using TimeBank.Domain.Entities;

namespace TimeBank.Application.Mapping;

public static class DtoMapper
{
    public static MemberSummaryDto ToSummary(this Member member) => new(
        member.Id,
        member.FullName,
        member.Email,
        member.District,
        member.PhotoPath,
        member.Role.ToString(),
        member.IsSuspended,
        member.CreatedAt);

    public static RequestStatusDto ToStatus(this HelpRequest request) => new(
        request.Id,
        request.Status.ToString(),
        request.AcceptedOfferId,
        request.AcceptedHours,
        request.ActualHours);

    public static MemberDirectoryDto ToDirectory(this MemberProfileDto profile) => new(
        profile.Id,
        profile.FullName,
        profile.District,
        profile.PhotoPath,
        profile.Skills,
        profile.Badges,
        profile.AverageRating);
}

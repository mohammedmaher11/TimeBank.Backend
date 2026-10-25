using System.Security.Claims;
using TimeBank.Domain.Exceptions;

namespace TimeBank.API.Services;

public static class ClaimsPrincipalExtensions
{
    public static int GetMemberId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(value, out var id))
            throw new UnauthorizedException("The authenticated token does not contain a member id.");

        return id;
    }
}

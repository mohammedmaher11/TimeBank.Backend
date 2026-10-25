namespace TimeBank.API.Services;

public interface ICurrentUserService
{
    int? MemberId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? MemberId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Role => _httpContextAccessor.HttpContext?.User.FindFirst(
        System.Security.Claims.ClaimTypes.Role)?.Value;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}

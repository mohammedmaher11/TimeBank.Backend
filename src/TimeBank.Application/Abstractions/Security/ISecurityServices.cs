using TimeBank.Domain.Entities;

namespace TimeBank.Application.Abstractions.Security;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public sealed record JwtTokenResult(string Token, DateTime ExpiresAt);

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(Member member);
}

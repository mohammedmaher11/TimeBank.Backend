namespace TimeBank.Domain.Exceptions;

/// <summary>Invalid credentials or missing authentication (HTTP 401).</summary>
public sealed class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}

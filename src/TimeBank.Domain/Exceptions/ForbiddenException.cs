namespace TimeBank.Domain.Exceptions;

/// <summary>The authenticated member is not allowed to perform the operation (HTTP 403).</summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message)
    {
    }
}

namespace TimeBank.Domain.Exceptions;

/// <summary>The operation conflicts with the current persisted state (HTTP 409).</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}

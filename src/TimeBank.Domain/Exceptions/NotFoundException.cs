namespace TimeBank.Domain.Exceptions;

/// <summary>A requested resource does not exist (HTTP 404).</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

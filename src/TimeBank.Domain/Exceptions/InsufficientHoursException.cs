namespace TimeBank.Domain.Exceptions;

/// <summary>The member does not have enough available hours for the operation (HTTP 409).</summary>
public sealed class InsufficientHoursException : DomainException
{
    public InsufficientHoursException(string message) : base(message)
    {
    }
}

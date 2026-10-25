namespace TimeBank.Domain.Exceptions;

/// <summary>Base class for expected, business-level failures. The API maps these to ProblemDetails.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

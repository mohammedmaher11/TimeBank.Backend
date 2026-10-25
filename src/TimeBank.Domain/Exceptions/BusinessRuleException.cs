namespace TimeBank.Domain.Exceptions;

/// <summary>A business rule was violated by otherwise valid input (HTTP 400).</summary>
public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

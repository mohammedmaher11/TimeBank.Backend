namespace TimeBank.Application.Abstractions.Services;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly Today => DateOnly.FromDateTime(UtcNow);
}

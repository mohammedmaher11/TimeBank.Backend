namespace TimeBank.Domain.Common;

/// <summary>
/// Base type for persisted entities. Ids are assigned by the database.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; protected set; }
}

namespace TimeBank.API.Filters;

/// <summary>
/// Marks a controller action for BR-15 auditing. AuditFilter records the actor
/// from the JWT and Source = Api after the action succeeds.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditAttribute : Attribute
{
    public AuditAttribute(string action, string targetType)
    {
        Action = action;
        TargetType = targetType;
    }

    public string Action { get; }
    public string TargetType { get; }
}

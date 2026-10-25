using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using TimeBank.API.Services;
using TimeBank.Application.Abstractions.Persistence;
using TimeBank.Application.Abstractions.Services;
using TimeBank.Application.DTOs.Admin;
using TimeBank.Application.DTOs.Categories;
using TimeBank.Domain.Entities;
using TimeBank.Domain.Enums;

namespace TimeBank.API.Filters;

/// <summary>
/// BR-15: records every administrative action. Actor is the logged-in moderator id
/// from the JWT; Source is Api. The same filter shape can be used by an MVC back-office.
/// </summary>
public sealed class AuditFilter : IAsyncActionFilter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<AuditFilter> _logger;

    public AuditFilter(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        ILogger<AuditFilter> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return;

        var attribute = descriptor.MethodInfo.GetCustomAttribute<AuditAttribute>();
        if (attribute is null || executed.Exception is not null)
            return;

        try
        {
            var actor = _currentUser.MemberId?.ToString() ?? "Unknown";
            var targetId = context.RouteData.Values.TryGetValue("id", out var routeId)
                ? routeId?.ToString()
                : null;
            var target = $"{attribute.TargetType}:{targetId ?? "new"}";
            var details = ResolveDetails(attribute.Action, context.ActionArguments);

            var audit = AuditLog.Create(
                actor,
                AuditSource.Api,
                attribute.Action,
                target,
                details,
                _clock.UtcNow);

            await _unitOfWork.AuditLogs.AddAsync(audit);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            // The business action already succeeded; do not turn it into a 500.
            _logger.LogError(exception, "Failed to write audit log for {Action}.", attribute.Action);
        }
    }

    private static string? ResolveDetails(string action, IDictionary<string, object?> arguments)
    {
        if (action == "SuspendMember")
        {
            var request = arguments.Values.OfType<SuspendMemberRequest>().FirstOrDefault();
            return request?.Reason ?? "No reason supplied.";
        }

        if (action is "CreateCategory" or "UpdateCategory")
        {
            var request = arguments.Values
                .FirstOrDefault(v => v is CategoryCreateRequest or CategoryUpdateRequest);

            return request switch
            {
                CategoryCreateRequest create => $"Name: {create.Name}",
                CategoryUpdateRequest update => $"Name: {update.Name}",
                _ => null
            };
        }

        return null;
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace TimeBank.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
                throw;

            var (status, title, code) = Map(exception);

            if (status >= 500)
                _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                _logger.LogWarning("Business error {Code} for {Method} {Path}: {Message}", code, context.Request.Method, context.Request.Path, exception.Message);

            context.Response.Clear();
            context.Response.StatusCode = status;

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message,
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = code;
            problem.Extensions["traceId"] = context.TraceIdentifier;

            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body, problem, cancellationToken: context.RequestAborted);
        }
    }

    private static (int Status, string Title, string Code) Map(Exception exception) => exception switch
    {
        TimeBank.Domain.Exceptions.InsufficientHoursException => (409, "Insufficient hours", "INSUFFICIENT_HOURS"),
        TimeBank.Domain.Exceptions.ConflictException => (409, "Conflict", "CONFLICT"),
        TimeBank.Domain.Exceptions.NotFoundException => (404, "Not found", "NOT_FOUND"),
        TimeBank.Domain.Exceptions.ForbiddenException => (403, "Forbidden", "FORBIDDEN"),
        TimeBank.Domain.Exceptions.UnauthorizedException => (401, "Unauthorized", "UNAUTHORIZED"),
        TimeBank.Domain.Exceptions.BusinessRuleException => (400, "Business rule violation", "BUSINESS_RULE"),
        Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (409, "Concurrent update", "CONCURRENT_UPDATE"),
        Microsoft.EntityFrameworkCore.DbUpdateException dbUpdate => MapDbUpdate(dbUpdate),
        BadHttpRequestException => (400, "Bad request", "BAD_REQUEST"),
        _ => (500, "Unexpected error", "INTERNAL_ERROR")
    };

    private static (int Status, string Title, string Code) MapDbUpdate(
        Microsoft.EntityFrameworkCore.DbUpdateException exception)
    {
        if (exception.InnerException is Microsoft.Data.SqlClient.SqlException sql)
        {
            if (sql.Number is 2601 or 2627)
                return (409, "Duplicate data", "DUPLICATE_DATA");

            if (sql.Number == 547)
            {
                return sql.Message.Contains("CHECK constraint", StringComparison.OrdinalIgnoreCase)
                    ? (409, "Constraint violation", "CONSTRAINT_VIOLATION")
                    : (409, "Relationship conflict", "RELATIONSHIP_CONFLICT");
            }
        }

        return (500, "Unexpected error", "INTERNAL_ERROR");
    }
}

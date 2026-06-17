using Ecommerce.Application.Common.Exceptions;
using FluentValidation;

namespace Ecommerce.API.Middleware;

public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failure for {Path}", context.Request.Path);
            await WriteValidationErrorAsync(context, ex);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning("Bad request for {Path}: {Message}", context.Request.Path, ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("Not found for {Path}: {Message}", context.Request.Path, ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message);
        }
        catch (ConflictException ex)
        {
            _logger.LogWarning("Conflict for {Path}: {Message}", context.Request.Path, ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message);
        }
        catch (ForbiddenException ex)
        {
            _logger.LogWarning("Forbidden for {Path}: {Message}", context.Request.Path, ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Forbidden", ex.Message);
        }
        catch (UnprocessableEntityException ex)
        {
            _logger.LogWarning("Unprocessable entity for {Path}: {Message}", context.Request.Path, ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", ex.Message, ex.Errors);
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogWarning("Authentication failed for {Path}", context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message);
        }
        catch (AccountLockedException ex)
        {
            _logger.LogWarning("Account locked for {Path}", context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status423Locked, "Locked", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
            await WriteInternalErrorAsync(context);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://tools.ietf.org/html/rfc7231#section-6.5.{statusCode % 100}",
            title,
            status = statusCode,
            detail,
            errors,
            traceId = context.TraceIdentifier
        });
    }

    private static async Task WriteValidationErrorAsync(HttpContext context, ValidationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            title = "Validation Failed",
            status = 400,
            errors,
            traceId = context.TraceIdentifier
        });
    }

    private static async Task WriteInternalErrorAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title = "An error occurred while processing your request.",
            status = 500,
            traceId = context.TraceIdentifier
        });
    }
}

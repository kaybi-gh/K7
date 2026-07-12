using Ardalis.GuardClauses;
using K7.Server.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Infrastructure;

public class CustomExceptionHandler : IExceptionHandler
{
    private readonly Dictionary<Type, Func<HttpContext, Exception, CancellationToken, Task>> _exceptionHandlers;

    public CustomExceptionHandler()
    {
        _exceptionHandlers = new()
        {
            { typeof(ValidationException), HandleValidationException },
            { typeof(NotFoundException), HandleNotFoundException },
            { typeof(UnauthorizedAccessException), HandleUnauthorizedAccessException },
            { typeof(ForbiddenAccessException), HandleForbiddenAccessException },
            { typeof(BadHttpRequestException), HandleBadRequestException },
            { typeof(HttpRequestException), HandleBadGatewayException },
            { typeof(DbUpdateConcurrencyException), HandleConcurrencyException },
        };
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var current = exception;
        while (current is not null)
        {
            var exceptionType = current.GetType();
            foreach (var (registeredType, handler) in _exceptionHandlers)
            {
                if (!registeredType.IsAssignableFrom(exceptionType))
                    continue;

                await handler.Invoke(httpContext, current, cancellationToken);
                return true;
            }

            current = current.InnerException;
        }

        var path = httpContext.Request.Path.Value;
        if (path is not null && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            }, cancellationToken);
            return true;
        }

        return false;
    }

    private static Task HandleValidationException(HttpContext httpContext, Exception ex, CancellationToken cancellationToken)
    {
        var exception = (ValidationException)ex;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return httpContext.Response.WriteAsJsonAsync(new ValidationProblemDetails(exception.Errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        }, cancellationToken);
    }

    private static Task HandleNotFoundException(HttpContext httpContext, Exception ex, CancellationToken cancellationToken)
    {
        var exception = (NotFoundException)ex;

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        return httpContext.Response.WriteAsJsonAsync(new ProblemDetails()
        {
            Status = StatusCodes.Status404NotFound,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "The specified resource was not found.",
            Detail = exception.Message
        }, cancellationToken);
    }

    private static Task HandleUnauthorizedAccessException(HttpContext httpContext, Exception ex, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;

        return httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
        }, cancellationToken);
    }

    private static Task HandleForbiddenAccessException(HttpContext httpContext, Exception ex, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;

        return httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3"
        }, cancellationToken);
    }

    private static Task HandleBadRequestException(HttpContext httpContext, Exception ex, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            Title = "Bad request",
            Detail = ex.Message
        }, cancellationToken);
    }

    private static Task HandleBadGatewayException(HttpContext httpContext, Exception ex, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;

        return httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status502BadGateway,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.3",
            Title = "Bad Gateway",
            Detail = "The remote server is unreachable."
        }, cancellationToken);
    }

    private static Task HandleConcurrencyException(HttpContext httpContext, Exception ex, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            Title = "Conflict",
            Detail = "The resource was modified by another request. Please retry."
        }, cancellationToken);
    }
}

using DesignApp.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace DesignApp.Api;

/// <summary>
/// Turns exceptions caused by the request (a broken domain rule, a malformed or oversized body) into
/// 4xx problem details with a message the frontend can show. Anything else stays a 500 without
/// internal details. Expected refusals such as a bad photo never get here: they come back as results.
/// </summary>
internal sealed class UserErrorExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "Invalid request"),
            BadHttpRequestException e => (e.StatusCode, "Invalid request"),
            _ => (0, ""),
        };
        if (status == 0)
            return false;

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new()
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = exception.Message },
        });
    }
}

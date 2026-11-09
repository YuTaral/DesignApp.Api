using DesignApp.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace DesignApp.Api.Middleware;

/// <summary>
/// Turns exceptions caused by the request (a broken domain rule, a malformed or oversized body) into
/// 4xx problem details with a message the frontend can show. Anything else stays a 500 without
/// internal details. Expected refusals such as a bad photo never get here: they come back as results.
/// </summary>
/// <param name="problemDetails">Writes the RFC 7807 problem response.</param>
internal sealed class UserErrorExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    /// <summary>Writes a 4xx problem response when the exception was caused by the request.</summary>
    /// <param name="httpContext">The failed request.</param>
    /// <param name="exception">What was thrown.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns><c>true</c> when handled; <c>false</c> lets the default 500 handling run.</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, Constants.Titles.InvalidRequest),
            BadHttpRequestException e => (e.StatusCode, Constants.Titles.InvalidRequest),
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

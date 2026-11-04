using DesignApp.Application.Photos;
using DesignApp.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace DesignApp.Api;

/// <summary>
/// Turns errors caused by the request (a bad photo, a broken domain rule) into 4xx problem details
/// with a message the frontend can show. Anything else stays a 500 without internal details.
/// </summary>
internal sealed class UserErrorExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            InvalidPhotoException => (StatusCodes.Status400BadRequest, "Invalid photo"),
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

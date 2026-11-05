namespace DesignApp.Api;

/// <summary>
/// Ends a request quietly when the browser has gone away: a page closed or left mid-request, or an
/// image replaced before it finished loading. The cancellation then surfaces as an
/// <see cref="OperationCanceledException"/> from the database or storage call, which isn't an
/// error. Caught here, in the app's own code, it never reaches the framework as unhandled, so it
/// doesn't fill the logs, and a debugger doesn't stop the whole app on it.
/// </summary>
internal sealed class ClientDisconnectedFilter : IEndpointFilter
{
    /// <summary>Status code for a request the client abandoned (nginx's convention); no one receives it.</summary>
    internal const int ClientClosedRequest = 499;

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Results.StatusCode(ClientClosedRequest);
        }
    }
}

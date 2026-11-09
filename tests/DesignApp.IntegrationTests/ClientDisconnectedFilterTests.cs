using DesignApp.Api;
using DesignApp.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DesignApp.IntegrationTests;

/// <summary>
/// A request the browser abandoned (its RequestAborted token is cancelled) ends quietly; any other
/// cancellation is still an error. No containers needed.
/// </summary>
public class ClientDisconnectedFilterTests
{
    [Fact]
    public async Task A_cancellation_after_the_browser_left_ends_the_request_quietly()
    {
        using var aborted = new CancellationTokenSource();
        var http = new DefaultHttpContext { RequestAborted = aborted.Token };
        await aborted.CancelAsync();

        var result = await new ClientDisconnectedFilter().InvokeAsync(
            new DefaultEndpointFilterInvocationContext(http), _ => throw new OperationCanceledException(aborted.Token));

        Assert.Equal(Constants.Http.ClientClosedRequest, Assert.IsType<StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task A_cancellation_while_the_browser_is_still_there_is_still_an_error()
    {
        var http = new DefaultHttpContext(); // RequestAborted not cancelled

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await new ClientDisconnectedFilter().InvokeAsync(
            new DefaultEndpointFilterInvocationContext(http), _ => throw new OperationCanceledException()));
    }

    [Fact]
    public async Task Answers_pass_through_unchanged()
    {
        var answer = TypedResults.Ok("fine");

        var result = await new ClientDisconnectedFilter().InvokeAsync(
            new DefaultEndpointFilterInvocationContext(new DefaultHttpContext()), _ => ValueTask.FromResult<object?>(answer));

        Assert.Same(answer, result);
    }
}

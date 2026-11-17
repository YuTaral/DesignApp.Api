using Microsoft.AspNetCore.Http.HttpResults;

namespace DesignApp.Api.Helpers;

/// <summary>The problem answers the endpoints share.</summary>
internal static class Problems
{
    /// <summary>A 400 Bad Request problem the user can read.</summary>
    /// <param name="detail">What is wrong, shown to the user.</param>
    /// <param name="title">The problem's title.</param>
    public static ProblemHttpResult BadRequest(string detail, string title)
    {
        return TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: title);
    }
}

using DesignApp.Application.Objects;

using static DesignApp.Application.Constants;

namespace DesignApp.Application.Helpers;

/// <summary>The point counts each stroke shape needs: a box has two corners, a lasso at least three points.</summary>
public static class StrokeShapes
{
    /// <summary>Why a stroke's points don't fit its shape, or <c>null</c> when they do.</summary>
    /// <param name="shape">The stroke's shape.</param>
    /// <param name="points">Its points (their count and range are checked separately).</param>
    /// <returns>A message the user can read, or <c>null</c>.</returns>
    public static string? Check(StrokeShape shape, IReadOnlyList<StrokePoint> points)
    {
        if (!Enum.IsDefined(shape))
        {
            return Constants.Messages.UnknownStrokeShape;
        }

        if (shape is StrokeShape.Box && points.Count != 2)
        {
            return Constants.Messages.BoxPoints;
        }

        if (shape is StrokeShape.Lasso && points.Count < 3)
        {
            return Constants.Messages.LassoPoints;
        }

        return null;
    }
}

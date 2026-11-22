using DesignApp.Application.Objects;

using C = DesignApp.Application.Constants.Builder.Camera;
using P = DesignApp.Application.Constants.Builder.Prompt;

namespace DesignApp.Application.Helpers;

/// <summary>
/// The 3D view's camera as the photo sees the room: across the room from the middle of the far wall (the wall in view),
/// a little in front of the near wall, at eye height, looking level at the far wall through a wide lens that shows the
/// room's whole width from halfway there, its picture cut from the lower part of a taller frame. It tells which points the picture shows, so the prompt describes only what is in the render (an object
/// beside or behind the camera that the prompt still named was drawn anyway, as something else). Where it stands, how
/// it is turned and its lens come from its place (<see cref="CameraPlace"/>), such as a near corner or a photo's spot.
/// </summary>
/// <remarks>The same camera as <c>scene-3d.ts</c> in DesignApp.Web; lengths in centimetres.</remarks>
public sealed class PlanCamera
{
    private readonly double _middle;
    private readonly double _stand;
    private readonly double _eye;
    private readonly double _across;
    private readonly double _below;
    private readonly double _above;
    private readonly double _sin;
    private readonly double _cos;

    /// <summary>The camera for a room seen this way.</summary>
    /// <param name="across">From the photo's left wall to its right wall.</param>
    /// <param name="height">From the floor to the ceiling.</param>
    /// <param name="pose">Where it stands, how it is turned and its lens.</param>
    public PlanCamera(double across, double height, CameraPose pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        // The lens fitted so the room's whole width shows from part of the way to the far wall, kept in a wide lens's range.
        var fit = 2 * Math.Atan(across / 2 / Math.Max(C.MinFitDistance, pose.Stand * C.FitDepthShare)) * 180 / Math.PI;
        var horizontal = (pose.Fov ?? Math.Clamp(fit, C.MinFov, C.MaxFov)) * Math.PI / 180;

        (_middle, _stand) = (pose.FromLeft, pose.Stand);
        _eye = Math.Min(C.EyeHeight, height - C.CeilingGap);
        (_sin, _cos) = (Math.Sin(pose.Turn), Math.Cos(pose.Turn));
        _across = Math.Tan(horizontal / 2);
        // The taller frame's half height (as a slope), of which the picture shows the lower part.
        var tall = _across * (1 + C.LensShift) / C.Aspect;

        _below = tall;
        _above = tall * (2 / (1 + C.LensShift) - 1);
    }

    /// <summary>
    /// How much of a thing the picture shows, from points on it: null when none of them is in the picture, else the
    /// picture's edges that cut it (none when it is all in view).
    /// </summary>
    /// <param name="points">Points on it: across from the left wall, from the far wall, and above the floor.</param>
    /// <returns>The edges cutting it ("left", "right", "bottom", "top"), or null when it is out of view.</returns>
    public IReadOnlyList<string>? Sees(IEnumerable<(double FromLeft, double FromFar, double Height)> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        string[] names = [P.LeftEdge, P.RightEdge, P.BottomEdge, P.TopEdge];
        var seen = false;
        var edges = new SortedSet<int>();

        foreach (var (fromLeft, fromFar, height) in points)
        {
            // To the right of the camera and ahead of it, before turning; then ahead and sideways as it looks.
            var (right, ahead) = (fromLeft - _middle, _stand - fromFar);
            var distance = right * _sin + ahead * _cos;

            if (distance <= 0)
            {
                edges.Add(2);
                continue;
            }

            var sideways = (right * _cos - ahead * _sin) / distance;
            var up = (height - _eye) / distance;
            var outside = false;

            if (sideways < -_across)
            {
                edges.Add(0);
                outside = true;
            }

            if (sideways > _across)
            {
                edges.Add(1);
                outside = true;
            }

            if (up < -_below)
            {
                edges.Add(2);
                outside = true;
            }

            if (up > _above)
            {
                edges.Add(3);
                outside = true;
            }

            seen |= !outside;
        }

        return seen ? edges.Select(e => names[e]).ToList() : null;
    }

    /// <summary>Evenly spread steps from one end to the other, both included.</summary>
    /// <param name="from">The first.</param>
    /// <param name="to">The last.</param>
    public static IEnumerable<double> Steps(double from, double to)
    {
        for (var i = 0; i < C.Samples; i++)
        {
            yield return from + (to - from) * i / (C.Samples - 1);
        }
    }
}

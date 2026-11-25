using DesignApp.Application.Objects;

using C = DesignApp.Application.Constants.Panoramas;

namespace DesignApp.UnitTests.Builder;

/// <summary>
/// The views that may be saved from a 360-degree photo keep clear of its seam behind at any lens and tilt: a tilted
/// view's corners reach further around than a level one's edges.
/// </summary>
public class PanoramaViewTests
{
    /// <summary>
    /// How close a view's edge comes to the seam behind, in degrees: each pixel on its border is a ray from the camera,
    /// tilted then turned as a saved view is cut, and the seam is the photo's turn of 180 degrees either way.
    /// </summary>
    /// <param name="view">The view.</param>
    private static double SeamGap(PanoramaView view)
    {
        var (width, height) = (C.ViewWidth, C.ViewHeight);
        var focal = height / 2.0 / Math.Tan(view.Fov * Math.PI / 360);
        var (pitch, yaw) = (view.Pitch * Math.PI / 180, view.Yaw * Math.PI / 180);
        var gap = double.MaxValue;
        var border = Enumerable.Range(0, width).SelectMany(x => new[] { (x, 0), (x, height - 1) })
            .Concat(Enumerable.Range(0, height).SelectMany(y => new[] { (0, y), (width - 1, y) }));

        foreach (var (x, y) in border)
        {
            var (dx, dy, dz) = (x - width / 2.0 + 0.5, -(y - height / 2.0 + 0.5), focal);
            var (up, ahead) = (dy * Math.Cos(pitch) + dz * Math.Sin(pitch), -dy * Math.Sin(pitch) + dz * Math.Cos(pitch));
            var (across, forward) = (dx * Math.Cos(yaw) + ahead * Math.Sin(yaw), -dx * Math.Sin(yaw) + ahead * Math.Cos(yaw));
            var turn = Math.Atan2(across, forward) * 180 / Math.PI;

            gap = Math.Min(gap, 180 - Math.Abs(turn));
        }

        return gap;
    }

    /// <summary>The furthest turn to the right a view with this lens and tilt may be saved at, found by halving.</summary>
    /// <param name="fov">The lens.</param>
    /// <param name="pitch">The tilt.</param>
    private static double FurthestYaw(double fov, double pitch)
    {
        var (low, high) = (0.0, 180.0);

        for (var step = 0; step < 40; step++)
        {
            var middle = (low + high) / 2;

            (low, high) = new PanoramaView(middle, pitch, fov).IsAllowed ? (middle, high) : (low, middle);
        }

        return low;
    }

    [Theory]
    [InlineData(35)]
    [InlineData(60)]
    [InlineData(75)]
    [InlineData(90)]
    public void At_any_tilt_the_furthest_view_allowed_keeps_its_margin_from_the_seam(double fov)
    {
        var tilts = new[] { 0, 10, 25, 40, PanoramaView.MaxTilt(fov) }.SelectMany(t => new[] { t, -t });

        foreach (var pitch in tilts)
        {
            var furthest = new PanoramaView(FurthestYaw(fov, pitch), pitch, fov);
            var gap = SeamGap(furthest);

            // Right at the margin, never inside it (the border pixels' rays fall just inside the view's edge).
            Assert.InRange(gap, C.SeamMargin - 0.01, C.SeamMargin + 0.5);
        }
    }

    [Fact]
    public void A_tilted_view_turns_less_and_no_view_reaches_straight_up_or_down()
    {
        var level = FurthestYaw(75, 0);
        var tilted = FurthestYaw(75, -40);

        // Level, a 75-degree lens turns to about 130 degrees; tilted 40 degrees, to about 101.
        Assert.InRange(level, 130, 131);
        Assert.InRange(tilted, 100, 102);
        // The viewer's start (a little down) and the first saved view are allowed.
        Assert.True(new PanoramaView(120, -6, 75).IsAllowed);
        Assert.True(new PanoramaView(120, C.FrontPitch, C.FrontFov).IsAllowed);
        // Straight up or down stays out of view: a 75-degree lens tilts at most 48.5 degrees, a 35-degree one 68.5.
        Assert.Equal(48.5, PanoramaView.MaxTilt(75), 3);
        Assert.Equal(68.5, PanoramaView.MaxTilt(35), 3);
        Assert.False(new PanoramaView(0, 60, 75).IsAllowed);
        Assert.False(new PanoramaView(0, -80, 35).IsAllowed);
    }
}

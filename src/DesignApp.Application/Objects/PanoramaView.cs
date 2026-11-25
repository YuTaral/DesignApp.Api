namespace DesignApp.Application.Objects;

/// <summary>
/// A view cut from a 360-degree photo: which way it looks from the photo's middle, and its lens.
/// </summary>
/// <param name="Yaw">The turn, in degrees: 0 faces the photo's middle, right is positive.</param>
/// <param name="Pitch">The tilt, in degrees: up is positive.</param>
/// <param name="Fov">The lens across the view's height, in degrees.</param>
public sealed record PanoramaView(double Yaw, double Pitch, double Fov)
{
    /// <summary>
    /// Whether a saved view may look this way (<see cref="Constants.Panoramas"/>): its lens in range, tilted no further
    /// than <see cref="MaxTilt"/>, and turned no further than keeps its edge (a 4:3 view, at its tilt) clear of the seam
    /// behind, as the viewer stops.
    /// </summary>
    public bool IsAllowed =>
        double.IsFinite(Yaw) && double.IsFinite(Pitch) && double.IsFinite(Fov)
        && Fov >= Constants.Panoramas.MinFov && Fov <= Constants.Panoramas.MaxFov && Math.Abs(Pitch) <= MaxTilt(Fov) + Constants.Panoramas.Rounding
        && Math.Abs(Yaw) <= Constants.Panoramas.MaxYaw - Reach(Fov, Pitch) - Constants.Panoramas.SeamMargin + Constants.Panoramas.Rounding;

    /// <summary>
    /// The furthest a view with this lens may tilt up or down: straight up and down, where every point of the seam meets,
    /// stay out of it by the seam's margin.
    /// </summary>
    /// <param name="fov">The lens across the view's height, in degrees.</param>
    /// <returns>The tilt, in degrees.</returns>
    public static double MaxTilt(double fov)
    {
        return Math.Min(Constants.Panoramas.MaxPitch, Constants.Panoramas.StraightUp - fov / 2 - Constants.Panoramas.SeamMargin);
    }

    /// <summary>
    /// How far a saved view's edge reaches to either side of where it faces, in degrees. Level, that is half its width;
    /// tilted, its top or bottom corner reaches further, nearer straight up or down, where the turns crowd together.
    /// </summary>
    /// <param name="fov">The lens across its height, in degrees.</param>
    /// <param name="pitch">Its tilt, in degrees.</param>
    private static double Reach(double fov, double pitch)
    {
        var aspect = (double)Constants.Panoramas.ViewWidth / Constants.Panoramas.ViewHeight;
        var half = fov * Math.PI / 360;
        var tilt = Math.Abs(pitch) * Math.PI / 180;

        return Math.Atan2(Math.Sin(half) * aspect, Math.Cos(tilt + half)) * 180 / Math.PI;
    }
}

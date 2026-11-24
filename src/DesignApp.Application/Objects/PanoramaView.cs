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
    /// Whether a saved view may look this way (<see cref="Constants.Panoramas"/>): its lens in range, and turned no
    /// further than keeps its edge (a 4:3 view) clear of the seam behind, as the viewer stops.
    /// </summary>
    public bool IsAllowed =>
        double.IsFinite(Yaw) && double.IsFinite(Pitch) && double.IsFinite(Fov)
        && Math.Abs(Pitch) <= Constants.Panoramas.MaxPitch && Fov >= Constants.Panoramas.MinFov && Fov <= Constants.Panoramas.MaxFov
        && Math.Abs(Yaw) <= Constants.Panoramas.MaxYaw - HalfWidth(Fov) - Constants.Panoramas.SeamMargin;

    /// <summary>Half a saved view's width, in degrees, for its lens across its height.</summary>
    /// <param name="fov">The lens across its height, in degrees.</param>
    private static double HalfWidth(double fov)
    {
        var aspect = (double)Constants.Panoramas.ViewWidth / Constants.Panoramas.ViewHeight;

        return Math.Atan(Math.Tan(fov * Math.PI / 360) * aspect) * 180 / Math.PI;
    }
}

using DesignApp.Application.Objects;

namespace DesignApp.Api.Requests;

/// <summary>A view of a room's 360-degree photo to save as one of its photos (mirrors the viewer's camera).</summary>
/// <param name="Yaw">The turn, in degrees: 0 faces the photo's middle, right is positive.</param>
/// <param name="Pitch">The tilt, in degrees: up is positive.</param>
/// <param name="Fov">The lens across the view's height, in degrees.</param>
public sealed record PanoramaViewRequest(double Yaw, double Pitch, double Fov)
{
    /// <summary>The request as the use case takes it.</summary>
    public PanoramaView ToView()
    {
        return new PanoramaView(Yaw, Pitch, Fov);
    }
}

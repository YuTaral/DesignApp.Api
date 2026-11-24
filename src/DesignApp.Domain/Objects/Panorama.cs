using DesignApp.Domain.Exceptions;

namespace DesignApp.Domain.Objects;

/// <summary>
/// A room's 360-degree photo: one equirectangular picture, twice as wide as it is tall, taken from one spot of its plan
/// at eye height. Its middle faces the plan's back wall; its two ends meet behind, where the picture has a seam.
/// </summary>
public sealed record Panorama
{
    /// <summary>Creates a panorama.</summary>
    /// <param name="key">Where the picture is stored.</param>
    /// <param name="width">Its width in pixels: twice its height.</param>
    /// <param name="height">Its height in pixels.</param>
    /// <param name="spot">Where it was taken, in cm on the plan.</param>
    /// <param name="startYaw">
    /// Which way its views start, in degrees from its middle (right is positive): towards most of the room. 0 for the
    /// panoramas made before it was kept.
    /// </param>
    /// <exception cref="DomainException">
    /// The key is missing, the picture isn't twice as wide as it is tall, or the start faces the seam behind.
    /// </exception>
    public Panorama(string key, int width, int height, OutlinePoint spot, double startYaw = 0)
    {
        ArgumentNullException.ThrowIfNull(spot);

        if (string.IsNullOrWhiteSpace(key) || height <= 0 || width != height * 2 || !double.IsFinite(startYaw) || Math.Abs(startYaw) > Constants.Rooms.MaxPanoramaStartYaw)
        {
            throw new DomainException(Constants.Messages.PanoramaFields);
        }

        (Key, Width, Height, Spot, StartYaw) = (key, width, height, spot, startYaw);
    }

    /// <summary>Where the picture is stored.</summary>
    public string Key { get; }

    /// <summary>Its width in pixels.</summary>
    public int Width { get; }

    /// <summary>Its height in pixels.</summary>
    public int Height { get; }

    /// <summary>Where it was taken, in cm on the plan.</summary>
    public OutlinePoint Spot { get; }

    /// <summary>Which way its views start, in degrees from its middle: right is positive.</summary>
    public double StartYaw { get; }
}

using DesignApp.Application.Objects;

namespace DesignApp.Application.Interfaces;

/// <summary>Measures how far each part of a room photo is from the camera, for the 3D view.</summary>
public interface IDepthProvider
{
    /// <summary>Measures a photo's depth.</summary>
    /// <param name="input">The photo and its analysis.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The depth map, or a failure with a message the user can read.</returns>
    Task<Result<DepthMeasurement>> MeasureAsync(DepthInput input, CancellationToken cancellationToken);
}

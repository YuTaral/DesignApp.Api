using System.Globalization;
using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;
using B = DesignApp.Application.Constants.Builder;
using C = DesignApp.Application.Constants.Builder.Camera;

namespace DesignApp.Application.Helpers;

/// <summary>
/// At the plan's camera spot (where the photo it was read from was taken), turned towards the far wall's middle, its
/// lens fitted to the room's width as from where it stands.
/// </summary>
public sealed class SpotCamera : CameraPlace
{
    /// <inheritdoc/>
    public override ViewCorner Corner => ViewCorner.Spot;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The room has no spot (<see cref="CameraPlace.For"/> picks the middle then).</exception>
    public override CameraPose Pose(ViewGeometry view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var (fromLeft, fromFar) = view.Spot ?? throw new InvalidOperationException("The room has no camera spot.");

        return new CameraPose(fromLeft, fromFar, Math.Atan2(view.Middle - fromLeft, Math.Max(C.MinFitDistance, fromFar)), null);
    }

    /// <inheritdoc/>
    public override string Sentence(CameraPose pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        var culture = CultureInfo.InvariantCulture;

        return B.Prompt.SpotCamera(Math.Round(pose.FromLeft).ToString("0", culture), Math.Round(pose.Stand).ToString("0", culture));
    }
}

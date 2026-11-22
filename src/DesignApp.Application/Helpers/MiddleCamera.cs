using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;
using B = DesignApp.Application.Constants.Builder;
using C = DesignApp.Application.Constants.Builder.Camera;

namespace DesignApp.Application.Helpers;

/// <summary>Straight across the room from the far wall's middle, a little in front of the near wall, looking straight at it.</summary>
public sealed class MiddleCamera : CameraPlace
{
    /// <inheritdoc/>
    public override ViewCorner Corner => ViewCorner.None;

    /// <inheritdoc/>
    public override CameraPose Pose(ViewGeometry view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return new CameraPose(view.Middle, view.Deep - C.Inset, 0, null);
    }

    /// <inheritdoc/>
    public override string Sentence(CameraPose pose)
    {
        return B.Prompt.Camera;
    }
}

using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;
using B = DesignApp.Application.Constants.Builder;
using C = DesignApp.Application.Constants.Builder.Camera;

namespace DesignApp.Application.Helpers;

/// <summary>In a near corner, a little in from both walls, turned towards the room to look diagonally across it through a fixed wide lens.</summary>
/// <param name="left">Whether it stands in the near left corner (else the near right one).</param>
public sealed class CornerCamera(bool left) : CameraPlace
{
    /// <inheritdoc/>
    public override ViewCorner Corner => left ? ViewCorner.Left : ViewCorner.Right;

    /// <inheritdoc/>
    public override bool Diagonal => true;

    /// <inheritdoc/>
    public override CameraPose Pose(ViewGeometry view)
    {
        ArgumentNullException.ThrowIfNull(view);
        // Turned to the right (towards the room) from the left corner, to the left from the right one.
        var turn = (left ? C.CornerTurn : -C.CornerTurn) * Math.PI / 180;

        return new CameraPose(left ? view.NearLeft + C.Inset : view.NearRight - C.Inset, view.Deep - C.Inset, turn, C.CornerFov);
    }

    /// <inheritdoc/>
    public override string Sentence(CameraPose pose)
    {
        return B.Prompt.CornerCamera(left);
    }
}

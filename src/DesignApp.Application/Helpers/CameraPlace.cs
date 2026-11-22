using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;
using C = DesignApp.Application.Constants.Builder.Camera;

namespace DesignApp.Application.Helpers;

/// <summary>
/// A place the camera can stand to look at the wall in view: where it stands, how it is turned and its lens
/// (<see cref="CameraPose"/>), and how the photo prompt says so. A new place is a new subclass and a line in
/// <see cref="For"/>; the camera (<see cref="PlanCamera"/>) and the prompt only use the pose and the sentence.
/// </summary>
/// <remarks>The same places as <c>CAMERA_PLACES</c> in DesignApp.Web's <c>room-outline.ts</c>.</remarks>
public abstract class CameraPlace
{
    /// <summary>Which place this is.</summary>
    public abstract ViewCorner Corner { get; }

    /// <summary>
    /// Whether it looks diagonally across the room: the walls keep the names they have from the middle of the near
    /// wall, and things are said to face walls rather than the camera.
    /// </summary>
    public virtual bool Diagonal => false;

    /// <summary>The place for a choice; the middle when the choice is the spot and the plan has none in front of the far wall.</summary>
    /// <param name="corner">The place chosen.</param>
    /// <param name="view">The room as seen.</param>
    public static CameraPlace For(ViewCorner corner, ViewGeometry view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return corner switch
        {
            ViewCorner.Left => new CornerCamera(left: true),
            ViewCorner.Right => new CornerCamera(left: false),
            ViewCorner.Spot when view.Spot is { FromFar: > C.Inset } => new SpotCamera(),
            _ => new MiddleCamera(),
        };
    }

    /// <summary>Where it stands and how it looks in a room.</summary>
    /// <param name="view">The room as seen.</param>
    public abstract CameraPose Pose(ViewGeometry view);

    /// <summary>Where it stands, in the photo prompt's words.</summary>
    /// <param name="pose">Its pose.</param>
    public abstract string Sentence(CameraPose pose);
}

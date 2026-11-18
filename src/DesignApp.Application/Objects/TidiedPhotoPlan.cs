using DesignApp.Domain.Objects;

namespace DesignApp.Application.Objects;

/// <summary>A photo plan as the AI read it, made ready for a room plan: its sizes kept in range, its objects checked and tidied.</summary>
/// <param name="Width">From the left wall to the right wall, in metres.</param>
/// <param name="Length">From the back wall to the front wall, in metres.</param>
/// <param name="Height">The ceiling height, in metres.</param>
/// <param name="WallColor">The walls' color.</param>
/// <param name="FloorColor">The floor's color.</param>
/// <param name="Finish">The walls' finish and the ceiling light, the rest as usual.</param>
/// <param name="Camera">Where the photo was taken from, kept inside the room.</param>
/// <param name="Items">The objects kept, tidied.</param>
/// <param name="LeftOut">The names of the objects left out (not valid, or fit nowhere).</param>
public sealed record TidiedPhotoPlan(double Width, double Length, double Height, HexColor WallColor, HexColor FloorColor, RoomFinish Finish,
    OutlinePoint Camera, IReadOnlyList<PlanItem> Items, IReadOnlyList<string> LeftOut);

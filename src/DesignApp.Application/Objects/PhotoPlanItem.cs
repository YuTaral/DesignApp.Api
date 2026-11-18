using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Objects;

/// <summary>
/// One object of a room photo as the AI placed it on the plan: a catalog object and its variation, its size, color and
/// material, and its place, on the floor or on a wall.
/// </summary>
/// <param name="Key">The catalog object's key.</param>
/// <param name="Variant">The variation's key (the first variation when unknown).</param>
/// <param name="X">On the floor: its middle across the plan from the left wall, in cm.</param>
/// <param name="Y">On the floor: its middle down the plan from the back wall, in cm.</param>
/// <param name="Rotation">Its turn, clockwise from above: 0 faces the front wall, 90 the left, 180 the back, 270 the right.</param>
/// <param name="Width">Its width, in cm (before turning).</param>
/// <param name="Depth">Its depth, in cm (before turning).</param>
/// <param name="Height">Its height, in cm.</param>
/// <param name="Color">Its color, #RRGGBB (may be invalid).</param>
/// <param name="Material">What it is made of; empty for none.</param>
/// <param name="Wall">The wall a window, door or hung thing is on; <c>null</c> on the floor.</param>
/// <param name="Along">On a wall: its middle from the wall's left end as seen from inside, in cm.</param>
/// <param name="Elevation">On a wall: its bottom above the floor, in cm.</param>
public sealed record PhotoPlanItem(string Key, string Variant, double X, double Y, int Rotation, double Width, double Depth, double Height, string Color,
    string Material, PlanWall? Wall, double Along, double Elevation);

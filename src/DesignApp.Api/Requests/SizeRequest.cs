namespace DesignApp.Api.Requests;

/// <summary>The size of a wall, floor or ceiling, as sent and as returned.</summary>
/// <param name="ObjectIndex">The surface, as in the room's objects and label map.</param>
/// <param name="Width">Its width, in metres (0.1 to 50).</param>
/// <param name="Height">Its height, or a floor's or ceiling's length, in metres (0.1 to 50).</param>
internal sealed record SizeRequest(int ObjectIndex, double Width, double Height);

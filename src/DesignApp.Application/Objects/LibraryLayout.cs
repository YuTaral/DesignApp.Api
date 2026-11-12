using DesignApp.Domain.Objects;

namespace DesignApp.Application.Objects;

/// <summary>One layout of a library room: a photo of it, analyzed ahead of time.</summary>
/// <param name="Id">The layout's id inside its room, such as <c>2</c>.</param>
/// <param name="Width">Width of the photo, in pixels.</param>
/// <param name="Height">Height of the photo, in pixels.</param>
/// <param name="Objects">The objects in its label map, ordered by index.</param>
public sealed record LibraryLayout(string Id, int Width, int Height, IReadOnlyList<DetectedObject> Objects);

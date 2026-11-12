namespace DesignApp.Infrastructure.Library;

/// <summary>A library layout's <c>.objects.json</c>: the photo's size and the objects of its label map.</summary>
/// <param name="Width">Width of the photo, in pixels.</param>
/// <param name="Height">Height of the photo, in pixels.</param>
/// <param name="Objects">The objects, ordered by index.</param>
internal sealed record LibraryRoomFile(int Width, int Height, IReadOnlyList<LibraryObjectFile> Objects);

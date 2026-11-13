namespace DesignApp.Infrastructure.Library;

/// <summary>A library layout's <c>.objects.json</c>: the photo's size and the objects of its label map.</summary>
/// <param name="Width">Width of the photo, in pixels.</param>
/// <param name="Height">Height of the photo, in pixels.</param>
/// <param name="Objects">The objects, ordered by index.</param>
/// <param name="Depth">Its depth map's camera, or <c>null</c> when it has no depth map.</param>
/// <param name="Backdrop">Whether it has an empty-room backdrop (its three <c>.backdrop.*</c> files).</param>
internal sealed record LibraryRoomFile(int Width, int Height, IReadOnlyList<LibraryObjectFile> Objects, LibraryDepthFile? Depth = null, bool Backdrop = false);

using DesignApp.Application.Objects;

namespace DesignApp.Application.Interfaces;

/// <summary>
/// The built-in library: ready-made designs whose room photos were analyzed ahead of time, so opening one costs
/// no AI. Its files never change while the API runs.
/// </summary>
public interface ILibrary
{
    /// <summary>Every library design, in the order they are shown.</summary>
    IReadOnlyList<LibraryDesign> Designs { get; }

    /// <summary>The design with this id, or <c>null</c>.</summary>
    /// <param name="designId">The design's id, such as <c>minimalist</c>.</param>
    LibraryDesign? FindDesign(string designId);

    /// <summary>Opens the photo of a library room's layout (a JPEG).</summary>
    /// <param name="designId">The design's id.</param>
    /// <param name="roomId">The room's id inside the design.</param>
    /// <param name="layoutId">The layout's id inside the room.</param>
    /// <exception cref="KeyNotFoundException">The library has no such layout.</exception>
    Stream OpenPhoto(string designId, string roomId, string layoutId);

    /// <summary>Opens the label map of a library room's layout (a PNG).</summary>
    /// <param name="designId">The design's id.</param>
    /// <param name="roomId">The room's id inside the design.</param>
    /// <param name="layoutId">The layout's id inside the room.</param>
    /// <exception cref="KeyNotFoundException">The library has no such layout.</exception>
    Stream OpenLabelMap(string designId, string roomId, string layoutId);

    /// <summary>Packs a room as the files of one library layout, in a zip (development only).</summary>
    /// <param name="room">The room's photo, label map and objects.</param>
    /// <returns>The zip's bytes.</returns>
    byte[] Pack(LibraryExport room);
}

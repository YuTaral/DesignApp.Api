using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case: open a layout of a built-in library room. The visitor gets their own copy, Ready at once with the library's
/// objects, so Claude and SAM 3 never run for it. Like an upload, it is deleted unless saved to a design.
/// </summary>
/// <param name="library">The library.</param>
/// <param name="copier">Copies the room's files.</param>
/// <param name="rooms">Saves the copy.</param>
public sealed class CopyLibraryRoom(ILibrary library, LibraryCopier copier, IRoomRepository rooms)
{
    /// <summary>Copies one layout of a library room for the current user.</summary>
    /// <param name="designId">The library design's id.</param>
    /// <param name="roomId">The room's id inside it.</param>
    /// <param name="layoutId">The layout's id inside the room.</param>
    /// <param name="aiMode">The AI the copy's later steps use (the caller checks that it's allowed).</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>The new room, or <c>null</c> when the library has no such layout.</returns>
    public async Task<Room?> HandleAsync(string designId, string roomId, string layoutId, AiMode aiMode, CancellationToken cancellationToken)
    {
        var design = library.FindDesign(designId);
        var source = design?.FindRoom(roomId);
        var layout = source?.FindLayout(layoutId);
        if (design is null || source is null || layout is null)
            return null;

        try
        {
            var room = await copier.CopyAsync(design, source, layout, aiMode, cancellationToken);
            await rooms.SaveChangesAsync(cancellationToken);
            return room;
        }
        catch
        {
            await copier.DeleteFilesAsync();
            throw;
        }
    }
}

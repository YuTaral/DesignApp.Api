using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Domain.Entities;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case: save a whole built-in library house as one of the account's designs, with a copy of every room in its
/// first layout (Ready at once, no AI). Needs an account, like every design.
/// </summary>
/// <param name="library">The library.</param>
/// <param name="copier">Copies the rooms' files.</param>
/// <param name="designs">Saves the design and its rooms.</param>
/// <param name="currentUser">The account.</param>
/// <param name="clock">Supplies the creation time.</param>
public sealed class CopyLibraryDesign(ILibrary library, LibraryCopier copier, IDesignRepository designs, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>Copies every room of a library design into a new design of the account.</summary>
    /// <param name="designId">The library design's id.</param>
    /// <param name="aiMode">The AI the copies' later steps use (the caller checks that it's allowed).</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>The new design, or <c>null</c> when the library has no such design.</returns>
    public async Task<Design?> HandleAsync(string designId, AiMode aiMode, CancellationToken cancellationToken)
    {
        var source = library.FindDesign(designId);
        if (source is null)
            return null;

        try
        {
            var copies = new List<Room>();
            foreach (var room in source.Rooms)
                copies.Add(await copier.CopyAsync(source, room, room.Layouts[0], aiMode, cancellationToken));

            var now = clock.GetUtcNow();
            var design = Design.Create(copies[0], currentUser.Id, source.Name, source.Description, [], now);
            foreach (var room in copies.Skip(1))
                design.AddRoom(room, now);
            designs.Add(design);
            await designs.SaveChangesAsync(cancellationToken); // the rooms are saved with it
            return design;
        }
        catch
        {
            await copier.DeleteFilesAsync();
            throw;
        }
    }
}

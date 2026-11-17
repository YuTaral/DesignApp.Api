using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case (development only): pack one of the user's rooms, as it is now, as the files of a library room. Outline
/// fixes, hand-marked objects and removed objects are kept, so a room can be cleaned up in the editor first.
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="storage">Where the room's files are.</param>
/// <param name="library">Packs the files.</param>
/// <param name="currentUser">Whose room it must be.</param>
public sealed class ExportLibraryRoom(IRoomRepository rooms, IFileStorage storage, ILibrary library, ICurrentUser currentUser)
{
    /// <summary>Packs the room.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="libraryRoomId">The library room id the files are named after, such as <c>kitchen</c>.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The zip's bytes, or <c>null</c> when the user has no such room or it isn't Ready.</returns>
    public async Task<byte[]?> HandleAsync(Guid roomId, string libraryRoomId, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);

        if (room is null || room.Status is not RoomStatus.Ready || room.LabelMapKey is null)
        {
            return null;
        }

        var photo = await ReadAsync(Constants.Rooms.PhotoKey(room.Id, room.PhotoVersion), cancellationToken);
        var labelMap = await ReadAsync(room.LabelMapKey, cancellationToken);

        if (photo is null || labelMap is null)
        {
            return null;
        }

        return library.Pack(new LibraryExport(libraryRoomId, photo, labelMap, room.Width, room.Height, room.Objects));
    }

    /// <summary>Reads a stored file into memory.</summary>
    /// <param name="key">The storage key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or <c>null</c> when the file is missing.</returns>
    private async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        await using var file = await storage.OpenReadAsync(key, cancellationToken);

        if (file is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();

        await file.Content.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }
}

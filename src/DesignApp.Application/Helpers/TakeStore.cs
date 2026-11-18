using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

namespace DesignApp.Application.Helpers;

/// <summary>Stores the photos of a room to choose from, and copies the chosen one to the room's own photo.</summary>
/// <param name="processor">Turns the image model's picture into the stored JPEG.</param>
/// <param name="storage">Where the files are.</param>
/// <param name="clock">When each photo is made.</param>
public sealed class TakeStore(IPhotoProcessor processor, IFileStorage storage, TimeProvider clock)
{
    /// <summary>Stores a picture as a new photo of a room (not yet added to the room's list).</summary>
    /// <param name="roomId">The room, which may not be saved yet.</param>
    /// <param name="image">The picture, as the image model made it.</param>
    /// <param name="note">Its view's name, or the change asked for.</param>
    /// <param name="changedFrom">The photo it was changed from, if any.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The photo, or why the picture can't be used.</returns>
    public async Task<Result<PhotoTake>> StoreAsync(Guid roomId, byte[] image, string note, Guid? changedFrom, CancellationToken cancellationToken)
    {
        using var upload = new MemoryStream(image, writable: false);
        var processed = processor.Process(upload);

        if (!processed.Succeeded)
        {
            return Result.Failure<PhotoTake>(processed.Error);
        }

        var now = clock.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        var key = Constants.Rooms.TakeKey(roomId, id);

        using (var content = new MemoryStream(processed.Value.Content, writable: false))
        {
            await storage.SaveAsync(key, content, Constants.ContentTypes.Jpeg, cancellationToken);
        }

        return PhotoTake.Create(id, roomId, key, processed.Value.Width, processed.Value.Height, note, changedFrom, now);
    }

    /// <summary>The picture of a photo, to change it.</summary>
    /// <param name="take">The photo.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public Task<byte[]> ReadAsync(PhotoTake take, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(take);

        return storage.ReadAllBytesAsync(take.Key, cancellationToken);
    }

    /// <summary>Copies a photo to its room's own photo (version 0), which the analysis and the editor use.</summary>
    /// <param name="take">The photo.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    public async Task UseAsRoomPhotoAsync(PhotoTake take, CancellationToken cancellationToken)
    {
        var picture = await ReadAsync(take, cancellationToken);

        using var content = new MemoryStream(picture, writable: false);

        await storage.SaveAsync(Constants.Rooms.PhotoKey(take.RoomId), content, Constants.ContentTypes.Jpeg, cancellationToken);
    }
}

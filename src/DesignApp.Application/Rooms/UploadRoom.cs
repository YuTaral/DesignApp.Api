using DesignApp.Application.Abstractions;
using DesignApp.Application.Photos;
using DesignApp.Domain.Rooms;

namespace DesignApp.Application.Rooms;

public sealed class UploadRoom(
    IPhotoProcessor photoProcessor,
    IFileStorage storage,
    IRoomRepository rooms,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<Room> HandleAsync(Stream upload, CancellationToken cancellationToken)
    {
        var photo = photoProcessor.Process(upload);

        var now = clock.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        var photoKey = StorageKeys.RoomPhoto(id);
        var room = Room.Upload(id, currentUser.Id, photoKey, photo.Width, photo.Height, now);

        using (var content = new MemoryStream(photo.Content, writable: false))
            await storage.SaveAsync(photoKey, content, ProcessedPhoto.ContentType, cancellationToken);

        rooms.Add(room);
        try
        {
            await rooms.SaveChangesAsync(cancellationToken);
        }
        catch (Exception saveError)
        {
            // Don't leave an orphaned photo behind when the room can't be saved,
            // but report the save failure even if the cleanup fails too.
            try
            {
                await storage.DeleteAsync(photoKey, CancellationToken.None);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(saveError, cleanupError);
            }
            throw;
        }

        return room;
    }
}

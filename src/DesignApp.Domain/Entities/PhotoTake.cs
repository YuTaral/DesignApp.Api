using DesignApp.Domain.Exceptions;

namespace DesignApp.Domain.Entities;

/// <summary>
/// One photo of a room to choose from: a photo Generate made of the room's plan from one view, or a version the AI made
/// of another one with the user's change. The room's own photo is the chosen one (<see cref="Room.ChosenTakeId"/>).
/// </summary>
public sealed class PhotoTake
{
    /// <summary>The photo's id (a time-ordered UUID v7, so photos sort in the order they were made).</summary>
    public Guid Id { get; private set; }
    /// <summary>The room it is a photo of.</summary>
    public Guid RoomId { get; private set; }
    /// <summary>Storage key of the photo (a JPEG).</summary>
    public string Key { get; private set; } = "";
    /// <summary>Width of the photo, in pixels.</summary>
    public int Width { get; private set; }
    /// <summary>Height of the photo, in pixels.</summary>
    public int Height { get; private set; }
    /// <summary>What it is: its view's name, or the change the user asked the AI for.</summary>
    public string Note { get; private set; } = "";
    /// <summary>The photo it was made from with a change, or <c>null</c> for one made from the plan.</summary>
    public Guid? ChangedFrom { get; private set; }
    /// <summary>When it was made.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    private PhotoTake() { } // for EF Core

    /// <summary>A new photo of a room.</summary>
    /// <param name="id">Chosen by the caller, so the file can be stored under it first.</param>
    /// <param name="roomId">The room.</param>
    /// <param name="key">Storage key of the photo.</param>
    /// <param name="width">Its width in pixels.</param>
    /// <param name="height">Its height in pixels.</param>
    /// <param name="note">Its view's name, or the change asked for.</param>
    /// <param name="changedFrom">The photo it was changed from, if any.</param>
    /// <param name="now">When it was made.</param>
    /// <exception cref="DomainException">An id, the key, the size or the note is missing or out of its limits.</exception>
    public static PhotoTake Create(Guid id, Guid roomId, string key, int width, int height, string note, Guid? changedFrom, DateTimeOffset now)
    {
        var trimmed = (note ?? "").Trim();

        if (id == Guid.Empty || roomId == Guid.Empty || string.IsNullOrWhiteSpace(key) || width <= 0 || height <= 0
            || trimmed.Length > Constants.Rooms.MaxTakeNoteLength)
        {
            throw new DomainException(Constants.Messages.TakeFields);
        }

        return new PhotoTake { Id = id, RoomId = roomId, Key = key, Width = width, Height = height, Note = trimmed, ChangedFrom = changedFrom, CreatedAt = now };
    }
}

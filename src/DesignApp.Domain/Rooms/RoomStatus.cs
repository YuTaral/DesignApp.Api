namespace DesignApp.Domain.Rooms;

/// <summary>The steps a room goes through, from upload to editable.</summary>
public enum RoomStatus
{
    /// <summary>The photo is stored; analysis hasn't started.</summary>
    Uploaded,
    /// <summary>The AI is listing the objects in the photo.</summary>
    Analyzing,
    /// <summary>The objects are being outlined into masks.</summary>
    Segmenting,
    /// <summary>Analysis finished; the room can be edited.</summary>
    Ready,
    /// <summary>Analysis stopped; see <see cref="Room.FailureReason"/>.</summary>
    Failed,
}

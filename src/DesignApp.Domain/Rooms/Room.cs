using DesignApp.Domain.Common;

namespace DesignApp.Domain.Rooms;

/// <summary>An uploaded room photo and the objects the AI pipeline found in it.</summary>
public sealed class Room
{
    private IReadOnlyList<DetectedObject> _objects = [];

    /// <summary>The room's id (a time-ordered UUID v7).</summary>
    public Guid Id { get; private set; }
    /// <summary>The user who uploaded the room.</summary>
    public string OwnerId { get; private set; } = "";
    /// <summary>Storage key of the processed photo.</summary>
    public string PhotoKey { get; private set; } = "";
    /// <summary>Width of the stored photo, in pixels.</summary>
    public int Width { get; private set; }
    /// <summary>Height of the stored photo, in pixels.</summary>
    public int Height { get; private set; }
    /// <summary>Where the room is in the AI analysis.</summary>
    public RoomStatus Status { get; private set; }
    /// <summary>Why the analysis failed; <c>null</c> unless <see cref="Status"/> is Failed.</summary>
    public string? FailureReason { get; private set; }
    /// <summary>When the photo was uploaded.</summary>
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>The objects found in the photo, ordered by index; empty until ready.</summary>
    public IReadOnlyList<DetectedObject> Objects => _objects;

    private Room() { } // for EF Core

    /// <summary>Creates a room for a newly uploaded photo, waiting for analysis.</summary>
    /// <param name="id">Chosen by the caller so the photo can be stored under the room's id before the room is saved.</param>
    /// <param name="ownerId">The uploading user.</param>
    /// <param name="photoKey">Storage key of the processed photo.</param>
    /// <param name="width">Photo width in pixels.</param>
    /// <param name="height">Photo height in pixels.</param>
    /// <param name="now">The upload time.</param>
    /// <exception cref="DomainException">The id is empty or the size isn't positive.</exception>
    public static Room Upload(Guid id, string ownerId, string photoKey, int width, int height, DateTimeOffset now)
    {
        if (id == Guid.Empty)
            throw new DomainException("A room needs an id.");
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(photoKey);
        if (width <= 0 || height <= 0)
            throw new DomainException("Photo dimensions must be positive.");

        return new Room
        {
            Id = id,
            OwnerId = ownerId,
            PhotoKey = photoKey,
            Width = width,
            Height = height,
            Status = RoomStatus.Uploaded,
            CreatedAt = now,
        };
    }

    /// <summary>Uploaded → Analyzing: the AI starts listing the objects.</summary>
    public void StartAnalysis() => MoveTo(RoomStatus.Analyzing, from: RoomStatus.Uploaded);

    /// <summary>Analyzing → Segmenting: the objects are being outlined.</summary>
    public void StartSegmentation() => MoveTo(RoomStatus.Segmenting, from: RoomStatus.Analyzing);

    /// <summary>Segmenting → Ready: stores the detected objects.</summary>
    /// <param name="objects">At least one object, with unique indexes.</param>
    /// <exception cref="DomainException">The list is empty, an index repeats, or the room isn't segmenting.</exception>
    public void MarkReady(IEnumerable<DetectedObject> objects)
    {
        var list = objects.ToList();
        if (list.Count == 0)
            throw new DomainException("A ready room needs at least one detected object.");
        if (list.Select(o => o.Index).Distinct().Count() != list.Count)
            throw new DomainException("Detected object indexes must be unique.");

        MoveTo(RoomStatus.Ready, from: RoomStatus.Segmenting);
        _objects = list.OrderBy(o => o.Index).ToList().AsReadOnly();
    }

    /// <summary>Stops the analysis with a reason the user can see.</summary>
    /// <param name="reason">Why the analysis failed.</param>
    /// <exception cref="DomainException">The room is already Ready or Failed.</exception>
    public void MarkFailed(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status is RoomStatus.Ready or RoomStatus.Failed)
            throw new DomainException($"Cannot fail a room that is already {Status}.");
        Status = RoomStatus.Failed;
        FailureReason = reason;
    }

    /// <summary>Changes the status, allowing only the one expected step.</summary>
    /// <param name="next">The new status.</param>
    /// <param name="from">The status the room must currently have.</param>
    private void MoveTo(RoomStatus next, RoomStatus from)
    {
        if (Status != from)
            throw new DomainException($"Cannot move a room from {Status} to {next}.");
        Status = next;
    }
}

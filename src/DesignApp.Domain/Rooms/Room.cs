using DesignApp.Domain.Common;

namespace DesignApp.Domain.Rooms;

/// <summary>An uploaded room photo and the objects the AI pipeline found in it.</summary>
public sealed class Room
{
    private readonly List<DetectedObject> _objects = [];

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = "";
    public string PhotoKey { get; private set; } = "";
    public int Width { get; private set; }
    public int Height { get; private set; }
    public RoomStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<DetectedObject> Objects => _objects;

    private Room() { } // for EF Core

    public static Room Upload(string ownerId, string photoKey, int width, int height, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(photoKey);
        if (width <= 0 || height <= 0)
            throw new DomainException("Photo dimensions must be positive.");

        return new Room
        {
            Id = Guid.CreateVersion7(now),
            OwnerId = ownerId,
            PhotoKey = photoKey,
            Width = width,
            Height = height,
            Status = RoomStatus.Uploaded,
            CreatedAt = now,
        };
    }

    public void StartAnalysis() => MoveTo(RoomStatus.Analyzing, from: RoomStatus.Uploaded);

    public void StartSegmentation() => MoveTo(RoomStatus.Segmenting, from: RoomStatus.Analyzing);

    public void MarkReady(IEnumerable<DetectedObject> objects)
    {
        var list = objects.ToList();
        if (list.Count == 0)
            throw new DomainException("A ready room needs at least one detected object.");
        if (list.Select(o => o.Index).Distinct().Count() != list.Count)
            throw new DomainException("Detected object indexes must be unique.");

        MoveTo(RoomStatus.Ready, from: RoomStatus.Segmenting);
        _objects.Clear();
        _objects.AddRange(list.OrderBy(o => o.Index));
    }

    public void MarkFailed(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status is RoomStatus.Ready or RoomStatus.Failed)
            throw new DomainException($"Cannot fail a room that is already {Status}.");
        Status = RoomStatus.Failed;
        FailureReason = reason;
    }

    private void MoveTo(RoomStatus next, RoomStatus from)
    {
        if (Status != from)
            throw new DomainException($"Cannot move a room from {Status} to {next}.");
        Status = next;
    }
}

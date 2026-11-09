using DesignApp.Domain.Common;
using static DesignApp.Domain.Constants;

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
    /// <summary>Storage key of the label map (a PNG whose pixel values are object indexes); <c>null</c> until ready.</summary>
    public string? LabelMapKey { get; private set; }
    /// <summary>How many times analysis has started, counting restarts after an interruption.</summary>
    public int AnalysisAttempts { get; private set; }
    /// <summary>Which objects the analysis looks for.</summary>
    public AnalysisScope Scope { get; private set; }
    /// <summary>Which AI analyzes the photo.</summary>
    public AiMode AiMode { get; private set; }
    /// <summary>
    /// Which version of the label map is current: 0 is the one the analysis made, each outline fix
    /// adds one. Every version stays stored, so undo can go back to it.
    /// </summary>
    public int LabelMapRevision { get; private set; }
    /// <summary>The newest label map version stored; fixes made after an undo get numbers above it.</summary>
    public int LatestLabelMapRevision { get; private set; }

    private Room() { } // for EF Core

    /// <summary>Creates a room for a newly uploaded photo, waiting for analysis.</summary>
    /// <param name="id">Chosen by the caller so the photo can be stored under the room's id before the room is saved.</param>
    /// <param name="ownerId">The uploading user.</param>
    /// <param name="photoKey">Storage key of the processed photo.</param>
    /// <param name="width">Photo width in pixels.</param>
    /// <param name="height">Photo height in pixels.</param>
    /// <param name="now">The upload time.</param>
    /// <param name="scope">Which objects the analysis looks for.</param>
    /// <param name="aiMode">Which AI analyzes the photo.</param>
    /// <exception cref="DomainException">The id is empty or the size isn't positive.</exception>
    public static Room Upload(Guid id, string ownerId, string photoKey, int width, int height, DateTimeOffset now,
        AnalysisScope scope = AnalysisScope.WholeRoom, AiMode aiMode = AiMode.Fake)
    {
        if (id == Guid.Empty)
            throw new DomainException(Constants.Messages.RoomNeedsId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(photoKey);
        if (width <= 0 || height <= 0)
            throw new DomainException(Constants.Messages.PhotoSize);

        return new Room
        {
            Id = id,
            OwnerId = ownerId,
            PhotoKey = photoKey,
            Width = width,
            Height = height,
            Status = RoomStatus.Uploaded,
            CreatedAt = now,
            Scope = Enum.IsDefined(scope) ? scope : throw new DomainException(Constants.Messages.UnknownScope(scope)),
            AiMode = Enum.IsDefined(aiMode) ? aiMode : throw new DomainException(Constants.Messages.UnknownAiMode(aiMode)),
        };
    }

    /// <summary>
    /// Gives the room to another user: a guest who signs in keeps the rooms made before, under the account.
    /// </summary>
    /// <param name="newOwnerId">The account's user id.</param>
    public void TransferTo(string newOwnerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newOwnerId);
        OwnerId = newOwnerId;
    }

    /// <summary>Uploaded → Analyzing: the AI starts listing the objects.</summary>
    /// <exception cref="DomainException">The room isn't waiting for analysis, or it has used up its attempts.</exception>
    public void StartAnalysis()
    {
        if (AnalysisAttempts >= Constants.Rooms.MaxAnalysisAttempts)
            throw new DomainException(Constants.Messages.TooManyAttempts);
        MoveTo(RoomStatus.Analyzing, from: RoomStatus.Uploaded);
        AnalysisAttempts++;
    }

    /// <summary>
    /// Analyzing or Segmenting → Uploaded: puts back a room whose analysis was cut off (the API
    /// stopped mid-way), so it can be analyzed again.
    /// </summary>
    /// <exception cref="DomainException">The room isn't in the middle of an analysis.</exception>
    public void RestartAnalysis()
    {
        if (Status is not (RoomStatus.Analyzing or RoomStatus.Segmenting))
            throw new DomainException(Constants.Messages.CannotRestart(Status));
        Status = RoomStatus.Uploaded;
    }

    /// <summary>Analyzing → Segmenting: the objects are being outlined.</summary>
    public void StartSegmentation() => MoveTo(RoomStatus.Segmenting, from: RoomStatus.Analyzing);

    /// <summary>Segmenting → Ready: stores the detected objects and where their label map is.</summary>
    /// <param name="objects">At least one object, with unique indexes from 1 to <see cref="Constants.Rooms.MaxObjectIndex"/>.</param>
    /// <param name="labelMapKey">Storage key of the label map.</param>
    /// <exception cref="DomainException">The list is empty, an index repeats or is out of range, or the room isn't segmenting.</exception>
    public void MarkReady(IEnumerable<DetectedObject> objects, string labelMapKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labelMapKey);
        var list = objects.ToList();
        if (list.Count == 0)
            throw new DomainException(Constants.Messages.ReadyNeedsObjects);
        if (list.Select(o => o.Index).Distinct().Count() != list.Count)
            throw new DomainException(Constants.Messages.DuplicateObjectIndexes);
        if (list.Any(o => o.Index is < 1 or > Constants.Rooms.MaxObjectIndex))
            throw new DomainException(Constants.Messages.ObjectIndexRange);

        MoveTo(RoomStatus.Ready, from: RoomStatus.Segmenting);
        _objects = list.OrderBy(o => o.Index).ToList().AsReadOnly();
        LabelMapKey = labelMapKey;
    }

    /// <summary>
    /// Stores a fixed label map as a new version and makes it current. The caller saves the file
    /// under <paramref name="keyFor"/> of the returned revision first.
    /// </summary>
    /// <param name="keyFor">The storage key of a revision.</param>
    /// <returns>The new revision's number.</returns>
    /// <exception cref="DomainException">The room isn't ready.</exception>
    public int AddLabelMapRevision(Func<int, string> keyFor)
    {
        ArgumentNullException.ThrowIfNull(keyFor);
        if (Status is not RoomStatus.Ready)
            throw new DomainException(Constants.Messages.CannotFixOutlines(Status));
        LatestLabelMapRevision++;
        LabelMapRevision = LatestLabelMapRevision;
        LabelMapKey = keyFor(LabelMapRevision);
        return LabelMapRevision;
    }

    /// <summary>Makes an earlier or later stored label map version current again (undo and redo of outline fixes).</summary>
    /// <param name="revision">A stored version, from 0 to <see cref="LatestLabelMapRevision"/>.</param>
    /// <param name="keyFor">The storage key of a revision.</param>
    /// <exception cref="DomainException">The room isn't ready or there is no such version.</exception>
    public void UseLabelMapRevision(int revision, Func<int, string> keyFor)
    {
        ArgumentNullException.ThrowIfNull(keyFor);
        if (Status is not RoomStatus.Ready)
            throw new DomainException(Constants.Messages.CannotChangeOutlines(Status));
        if (!HasLabelMapRevision(revision))
            throw new DomainException(Constants.Messages.NoLabelMapRevision(revision));
        LabelMapRevision = revision;
        LabelMapKey = keyFor(revision);
    }

    /// <summary>Whether a label map version is stored.</summary>
    /// <param name="revision">The version.</param>
    public bool HasLabelMapRevision(int revision) => Status is RoomStatus.Ready && revision >= 0 && revision <= LatestLabelMapRevision;

    /// <summary>Stops the analysis with a reason the user can see.</summary>
    /// <param name="reason">Why the analysis failed.</param>
    /// <exception cref="DomainException">The room is already Ready or Failed.</exception>
    public void MarkFailed(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status is RoomStatus.Ready or RoomStatus.Failed)
            throw new DomainException(Constants.Messages.CannotFail(Status));
        Status = RoomStatus.Failed;
        FailureReason = reason;
    }

    /// <summary>Changes the status, allowing only the one expected step.</summary>
    /// <param name="next">The new status.</param>
    /// <param name="from">The status the room must currently have.</param>
    private void MoveTo(RoomStatus next, RoomStatus from)
    {
        if (Status != from)
            throw new DomainException(Constants.Messages.CannotMove(Status, next));
        Status = next;
    }
}

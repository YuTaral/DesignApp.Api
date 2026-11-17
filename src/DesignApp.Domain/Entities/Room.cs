using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Entities;

/// <summary>An uploaded room photo and the objects the AI pipeline found in it.</summary>
public sealed class Room
{
    private IReadOnlyList<DetectedObject> _objects = [];
    private IReadOnlyList<int> _photoVersions = [];

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
    /// <summary>The index a new object gets: one past the highest so far, so an index is never reused.</summary>
    public int NextObjectIndex => _objects.Count == 0 ? 1 : _objects.Max(o => o.Index) + 1;
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
    /// <summary>
    /// For each label map version (by its number), which photo version goes with it: 0 is the uploaded photo, each
    /// object removed from the photo adds one. Versions not listed (rooms from before removal) use photo 0.
    /// </summary>
    public IReadOnlyList<int> PhotoVersions => _photoVersions;
    /// <summary>
    /// The built-in library layout this room was copied from (such as <c>minimalist/kitchen/2</c>), or <c>null</c> for
    /// an uploaded photo.
    /// </summary>
    public string? LibraryRoom { get; private set; }
    /// <summary>The photo version that goes with the current label map version.</summary>
    public int PhotoVersion => PhotoVersionOf(LabelMapRevision);
    /// <summary>The newest photo version stored.</summary>
    public int LatestPhotoVersion => _photoVersions.Count == 0 ? 0 : _photoVersions.Max();

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
    /// <param name="waitForUser">Leave the room <see cref="RoomStatus.Waiting"/> until the user starts its analysis.</param>
    /// <exception cref="DomainException">The id is empty or the size isn't positive.</exception>
    public static Room Upload(Guid id, string ownerId, string photoKey, int width, int height, DateTimeOffset now,
        AnalysisScope scope = AnalysisScope.WholeRoom, AiMode aiMode = AiMode.Fake, bool waitForUser = false)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException(Constants.Messages.RoomNeedsId);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(photoKey);

        if (width <= 0 || height <= 0)
        {
            throw new DomainException(Constants.Messages.PhotoSize);
        }

        return new Room
        {
            Id = id,
            OwnerId = ownerId,
            PhotoKey = photoKey,
            Width = width,
            Height = height,
            Status = waitForUser ? RoomStatus.Waiting : RoomStatus.Uploaded,
            CreatedAt = now,
            Scope = Enum.IsDefined(scope) ? scope : throw new DomainException(Constants.Messages.UnknownScope(scope)),
            AiMode = Enum.IsDefined(aiMode) ? aiMode : throw new DomainException(Constants.Messages.UnknownAiMode(aiMode)),
        };
    }

    /// <summary>
    /// Creates a room from a built-in library room: the photo was analyzed ahead of time, so the room starts Ready
    /// with the library's objects and label map, and no AI runs for it.
    /// </summary>
    /// <param name="id">The new room's id.</param>
    /// <param name="ownerId">The user who opened the library room.</param>
    /// <param name="photoKey">Storage key of the copied photo.</param>
    /// <param name="width">Photo width in pixels.</param>
    /// <param name="height">Photo height in pixels.</param>
    /// <param name="now">The copy time.</param>
    /// <param name="objects">The library room's objects, with the rules of <see cref="MarkReady"/>.</param>
    /// <param name="labelMapKey">Storage key of the copied label map.</param>
    /// <param name="libraryRoom">Which library layout it is, such as <c>minimalist/kitchen/2</c>.</param>
    /// <param name="aiMode">The AI later steps use (removing an object), chosen like an upload's.</param>
    /// <exception cref="DomainException">The id, size or objects are invalid.</exception>
    public static Room FromLibrary(Guid id, string ownerId, string photoKey, int width, int height, DateTimeOffset now,
        IEnumerable<DetectedObject> objects, string labelMapKey, string libraryRoom, AiMode aiMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoom);
        var room = Upload(id, ownerId, photoKey, width, height, now, AnalysisScope.WholeRoom, aiMode);

        room.Status = RoomStatus.Segmenting; // the analysis was done ahead of time
        room.MarkReady(objects, labelMapKey);
        room.LibraryRoom = libraryRoom;

        return room;
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

    /// <summary>Waiting → Uploaded: the user asked for the analysis, so it is queued.</summary>
    /// <exception cref="DomainException">The room isn't waiting for the user.</exception>
    public void QueueAnalysis()
    {
        MoveTo(RoomStatus.Uploaded, from: RoomStatus.Waiting);
    }

    /// <summary>Uploaded → Analyzing: the AI starts listing the objects.</summary>
    /// <exception cref="DomainException">The room isn't waiting for analysis, or it has used up its attempts.</exception>
    public void StartAnalysis()
    {
        if (AnalysisAttempts >= Constants.Rooms.MaxAnalysisAttempts)
        {
            throw new DomainException(Constants.Messages.TooManyAttempts);
        }

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
        {
            throw new DomainException(Constants.Messages.CannotRestart(Status));
        }

        Status = RoomStatus.Uploaded;
    }

    /// <summary>Analyzing → Segmenting: the objects are being outlined.</summary>
    public void StartSegmentation()
    {
        MoveTo(RoomStatus.Segmenting, from: RoomStatus.Analyzing);
    }

    /// <summary>Segmenting → Ready: stores the detected objects and where their label map is.</summary>
    /// <param name="objects">At least one object, with unique indexes from 1 to <see cref="Constants.Rooms.MaxObjectIndex"/>.</param>
    /// <param name="labelMapKey">Storage key of the label map.</param>
    /// <exception cref="DomainException">The list is empty, an index repeats or is out of range, or the room isn't segmenting.</exception>
    public void MarkReady(IEnumerable<DetectedObject> objects, string labelMapKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labelMapKey);
        var list = objects.ToList();

        if (list.Count == 0)
        {
            throw new DomainException(Constants.Messages.ReadyNeedsObjects);
        }

        if (list.Select(o => o.Index).Distinct().Count() != list.Count)
        {
            throw new DomainException(Constants.Messages.DuplicateObjectIndexes);
        }

        if (list.Any(o => o.Index is < 1 or > Constants.Rooms.MaxObjectIndex))
        {
            throw new DomainException(Constants.Messages.ObjectIndexRange);
        }

        if (list.FirstOrDefault(o => o.ParentIndex is int parent && !IsWhole(list, parent)) is { } orphan)
        {
            throw new DomainException(Constants.Messages.BadParent(orphan.Index));
        }

        MoveTo(RoomStatus.Ready, from: RoomStatus.Segmenting);
        _objects = list.OrderBy(o => o.Index).ToList().AsReadOnly();
        LabelMapKey = labelMapKey;
    }

    /// <summary>
    /// Adds an object the user marked by hand, with the label map version that holds its pixels, and makes that
    /// version current. The caller saves the file under <paramref name="keyFor"/> of the next revision first.
    /// </summary>
    /// <param name="detected">The new object; its index must be <see cref="NextObjectIndex"/>.</param>
    /// <param name="baseRevision">The version the object was marked on; the new one keeps its photo.</param>
    /// <param name="keyFor">The storage key of a revision.</param>
    /// <returns>The new revision's number.</returns>
    /// <exception cref="DomainException">The room isn't ready, or the index is taken or out of range.</exception>
    public int AddObject(DetectedObject detected, int baseRevision, Func<int, string> keyFor)
    {
        ArgumentNullException.ThrowIfNull(detected);

        if (Status is not RoomStatus.Ready)
        {
            throw new DomainException(Constants.Messages.CannotFixOutlines(Status));
        }

        if (detected.Index is < 1 or > Constants.Rooms.MaxObjectIndex)
        {
            throw new DomainException(Constants.Messages.ObjectIndexRange);
        }

        if (detected.Index != NextObjectIndex)
        {
            throw new DomainException(Constants.Messages.NotNextObjectIndex(detected.Index, NextObjectIndex));
        }

        if (detected.ParentIndex is int parent && !IsWhole(_objects, parent))
        {
            throw new DomainException(Constants.Messages.BadParent(detected.Index));
        }

        var revision = AddLabelMapRevision(baseRevision, keyFor);

        _objects = _objects.Append(detected).ToArray();

        return revision;
    }

    /// <summary>
    /// Records that objects were removed from the photo together: a new photo version with a new label map version
    /// (where they have no pixels), both current. The objects stay listed, so undo brings them back. The caller saves
    /// both files first, under <paramref name="keyFor"/> of the next revision and the next photo version.
    /// </summary>
    /// <param name="indexes">The removed objects, at least one.</param>
    /// <param name="baseRevision">The version they were removed from.</param>
    /// <param name="keyFor">The storage key of a label map revision.</param>
    /// <returns>The new label map revision and photo version.</returns>
    /// <exception cref="DomainException">The room isn't ready, no object is given, or the room has no such object.</exception>
    public (int Revision, int PhotoVersion) RemoveObjects(IReadOnlyCollection<int> indexes, int baseRevision, Func<int, string> keyFor)
    {
        ArgumentNullException.ThrowIfNull(indexes);

        if (Status is not RoomStatus.Ready)
        {
            throw new DomainException(Constants.Messages.CannotFixOutlines(Status));
        }

        if (indexes.Count == 0)
        {
            throw new DomainException(Constants.Messages.NothingToRemove);
        }

        if (indexes.FirstOrDefault(i => _objects.All(o => o.Index != i), -1) is var unknown and >= 0)
        {
            throw new DomainException(Constants.Messages.UnknownObject(unknown));
        }

        var photo = LatestPhotoVersion + 1;
        var revision = AddLabelMapRevision(baseRevision, keyFor, photo);

        return (revision, photo);
    }

    /// <summary>The photo version that goes with a label map version (0 for one stored before photo versions).</summary>
    /// <param name="revision">The label map version.</param>
    public int PhotoVersionOf(int revision)
    {
        return revision >= 0 && revision < _photoVersions.Count ? _photoVersions[revision] : 0;
    }

    /// <summary>
    /// Stores a fixed label map as a new version and makes it current. The caller saves the file
    /// under <paramref name="keyFor"/> of the returned revision first.
    /// </summary>
    /// <param name="baseRevision">The version the fix was made on; the new one keeps its photo.</param>
    /// <param name="keyFor">The storage key of a revision.</param>
    /// <param name="photoVersion">A new photo for this version (an object removed), or <c>null</c> to keep the base's.</param>
    /// <returns>The new revision's number.</returns>
    /// <exception cref="DomainException">The room isn't ready.</exception>
    public int AddLabelMapRevision(int baseRevision, Func<int, string> keyFor, int? photoVersion = null)
    {
        ArgumentNullException.ThrowIfNull(keyFor);

        if (Status is not RoomStatus.Ready)
        {
            throw new DomainException(Constants.Messages.CannotFixOutlines(Status));
        }

        var photo = photoVersion ?? PhotoVersionOf(baseRevision);

        LatestLabelMapRevision++;
        // One entry per revision; revisions from before photo versions are filled in as photo 0.
        _photoVersions = [.. Enumerable.Range(0, LatestLabelMapRevision).Select(PhotoVersionOf), photo];
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
        {
            throw new DomainException(Constants.Messages.CannotChangeOutlines(Status));
        }

        if (!HasLabelMapRevision(revision))
        {
            throw new DomainException(Constants.Messages.NoLabelMapRevision(revision));
        }

        LabelMapRevision = revision;
        LabelMapKey = keyFor(revision);
    }

    /// <summary>Whether a label map version is stored.</summary>
    /// <param name="revision">The version.</param>
    public bool HasLabelMapRevision(int revision)
    {
        return Status is RoomStatus.Ready && revision >= 0 && revision <= LatestLabelMapRevision;
    }

    /// <summary>Stops the analysis with a reason the user can see.</summary>
    /// <param name="reason">Why the analysis failed.</param>
    /// <exception cref="DomainException">The room is already Ready or Failed.</exception>
    public void MarkFailed(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status is RoomStatus.Ready or RoomStatus.Failed)
        {
            throw new DomainException(Constants.Messages.CannotFail(Status));
        }

        Status = RoomStatus.Failed;
        FailureReason = reason;
    }

    /// <summary>
    /// Adds the parts found on an object (a chair's seat, backrest and legs) in one new label map version, which becomes
    /// current.
    /// </summary>
    /// <param name="parts">The parts, with the room's next indexes in order, each a part of a whole object of the room.</param>
    /// <param name="baseRevision">The label map version the parts were found on.</param>
    /// <param name="keyFor">The storage key of a label map version.</param>
    /// <returns>The new version.</returns>
    /// <exception cref="DomainException">The room isn't ready, or a part's index or object is wrong.</exception>
    public int AddParts(IReadOnlyList<DetectedObject> parts, int baseRevision, Func<int, string> keyFor)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (Status is not RoomStatus.Ready)
        {
            throw new DomainException(Constants.Messages.CannotFixOutlines(Status));
        }

        var next = NextObjectIndex;

        foreach (var part in parts)
        {
            if (part.Index != next || part.Index > Constants.Rooms.MaxObjectIndex)
            {
                throw new DomainException(Constants.Messages.NotNextObjectIndex(part.Index, next));
            }

            if (part.ParentIndex is not int parent || !IsWhole(_objects, parent))
            {
                throw new DomainException(Constants.Messages.BadParent(part.Index));
            }

            next++;
        }

        var revision = AddLabelMapRevision(baseRevision, keyFor);

        _objects = [.. _objects, .. parts];

        return revision;
    }

    /// <summary>
    /// Records that a part was merged back into its object: its pixels are the parent's again in a new label map
    /// version, which becomes current. The part stays listed, so undo (an older version) brings it back.
    /// </summary>
    /// <param name="partIndex">The part.</param>
    /// <param name="baseRevision">The label map version the merge was made on.</param>
    /// <param name="keyFor">The storage key of a label map version.</param>
    /// <returns>The new version.</returns>
    /// <exception cref="DomainException">The room isn't ready, or the object isn't a part.</exception>
    public int MergePart(int partIndex, int baseRevision, Func<int, string> keyFor)
    {
        if (_objects.FirstOrDefault(o => o.Index == partIndex) is not { ParentIndex: not null })
        {
            throw new DomainException(Constants.Messages.NotAPart(partIndex));
        }

        return AddLabelMapRevision(baseRevision, keyFor);
    }

    /// <summary>Whether an object is in a list and isn't a part itself, so it can have parts.</summary>
    /// <param name="objects">The objects.</param>
    /// <param name="index">The object's index.</param>
    private static bool IsWhole(IEnumerable<DetectedObject> objects, int index)
    {
        return objects.Any(o => o.Index == index && o.ParentIndex is null);
    }

    /// <summary>Changes the status, allowing only the one expected step.</summary>
    /// <param name="next">The new status.</param>
    /// <param name="from">The status the room must currently have.</param>
    private void MoveTo(RoomStatus next, RoomStatus from)
    {
        if (Status != from)
        {
            throw new DomainException(Constants.Messages.CannotMove(Status, next));
        }

        Status = next;
    }
}

using DesignApp.Application.UseCases.Room;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Objects;
using Microsoft.Extensions.Logging.Abstractions;
using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Rooms;

/// <summary>Uploads nobody saved to a design are deleted after 24 hours, with all their files.</summary>
public class DeleteUnsavedRoomsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Only_unsaved_uploads_older_than_a_day_are_deleted_with_their_files()
    {
        var rooms = new FakeRooms();
        var storage = new FakeStorage();
        var old = await AddAsync(rooms, storage, Now.AddHours(-25), outlineFixes: 2);
        var recent = await AddAsync(rooms, storage, Now.AddHours(-23));
        var saved = await AddAsync(rooms, storage, Now.AddDays(-30));
        rooms.InDesigns.Add(saved.Id);

        var deleted = await UseCase(rooms, storage).HandleAsync(CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(new[] { recent.Id, saved.Id }.Order(), rooms.Saved.Select(r => r.Id).Order());
        // The photo and all three label map versions of the old upload are gone; the others keep theirs.
        Assert.DoesNotContain(storage.Files.Keys, key => key.Contains(old.Id.ToString()));
        Assert.Contains(DesignApp.Application.Constants.Rooms.PhotoKey(recent.Id), storage.Files.Keys);
        Assert.Contains(DesignApp.Application.Constants.Rooms.PhotoKey(saved.Id), storage.Files.Keys);
    }

    [Fact]
    public async Task Nothing_to_delete_saves_nothing()
    {
        var rooms = new FakeRooms();
        await AddAsync(rooms, new FakeStorage(), Now.AddHours(-1));
        var saves = rooms.SaveCount;

        Assert.Equal(0, await UseCase(rooms, new FakeStorage()).HandleAsync(CancellationToken.None));
        Assert.Equal(saves, rooms.SaveCount);
    }

    /// <summary>The use case at <see cref="Now"/>.</summary>
    private static DeleteUnsavedRooms UseCase(FakeRooms rooms, FakeStorage storage) =>
        new(rooms, new RoomRemover(rooms, storage, NullLogger<RoomRemover>.Instance), new FixedClock(Now));

    /// <summary>Saves a ready room uploaded at <paramref name="uploaded"/>, with its photo and label maps in storage.</summary>
    private static async Task<Room> AddAsync(FakeRooms rooms, FakeStorage storage, DateTimeOffset uploaded, int outlineFixes = 0)
    {
        var id = Guid.CreateVersion7(uploaded);
        var room = Room.Upload(id, "user-1", DesignApp.Application.Constants.Rooms.PhotoKey(id), 800, 600, uploaded);
        room.StartAnalysis();
        room.StartSegmentation();
        room.MarkReady([new DetectedObject(1, "wall", ObjectCategory.Wall, new BoundingBox(0, 0, 1, 1), DesignApp.Domain.Objects.HexColor.Parse("#FFFFFF"))],
            DesignApp.Application.Constants.Rooms.LabelMapKey(id));
        for (var i = 0; i < outlineFixes; i++)
            room.AddLabelMapRevision(revision => DesignApp.Application.Constants.Rooms.LabelMapKey(id, revision));
        rooms.Add(room);
        await rooms.SaveChangesAsync(CancellationToken.None);
        for (var revision = 0; revision <= room.LatestLabelMapRevision; revision++)
            await storage.SaveAsync(DesignApp.Application.Constants.Rooms.LabelMapKey(id, revision), new MemoryStream([1]), DesignApp.Application.Constants.ContentTypes.Png, CancellationToken.None);
        await storage.SaveAsync(room.PhotoKey, new MemoryStream([1]), DesignApp.Application.Constants.ContentTypes.Jpeg, CancellationToken.None);
        return room;
    }
}

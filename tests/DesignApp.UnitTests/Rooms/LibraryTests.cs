using System.IO.Compression;
using System.Text.Json;
using DesignApp.Application.Helpers;
using DesignApp.Application.Objects;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Library;
using DesignApp.Infrastructure.Photos;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Rooms;

/// <summary>
/// The built-in library: every embedded layout is complete and consistent, copying a layout makes a Ready room with
/// its own files (and cleans them up when the save fails), and a room packs back into a layout's files.
/// </summary>
public class LibraryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly EmbeddedLibrary Library = new();
    private static readonly string[] LayoutFiles = [".jpg", ".labels.png", ".objects.json"];

    private readonly FakeStorage _storage = new();
    private readonly FakeRooms _rooms = new();

    /// <summary>The copy use case wired to this test's fakes and the real embedded library.</summary>
    private CopyLibraryRoom CreateCopy()
    {
        var copier = new LibraryCopier(Library, _storage, _rooms, new FixedUser("user-1"), new FixedClock(Now), NullLogger<LibraryCopier>.Instance);
        return new CopyLibraryRoom(Library, copier, _rooms);
    }

    [Fact]
    public void The_library_has_at_least_one_design_with_rooms_and_layouts()
    {
        Assert.NotEmpty(Library.Designs);
        Assert.All(Library.Designs, d => Assert.All(d.Rooms, r => Assert.NotEmpty(r.Layouts)));
    }

    [Fact]
    public void Every_file_belongs_to_a_listed_layout()
    {
        var expected = Library.Designs
            .SelectMany(d => d.Rooms.SelectMany(r => r.Layouts.SelectMany(l => LayoutFiles.Select(s => $"{d.Id}/{r.Id}/{l.Id}{s}")))
                .Append($"{d.Id}/design.json"))
            .Order(StringComparer.Ordinal);
        Assert.Equal(expected, Library.Files.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_layout_has_a_matching_photo_label_map_and_objects()
    {
        foreach (var design in Library.Designs)
        {
            foreach (var room in design.Rooms)
            {
                foreach (var layout in room.Layouts)
                {
                    var name = $"{design.Id}/{room.Id}/{layout.Id}";
                    using (var photo = SKBitmap.Decode(Library.OpenPhoto(design.Id, room.Id, layout.Id)))
                    {
                        Assert.True(photo is not null, $"{name}: the photo can't be decoded.");
                        Assert.Equal((layout.Width, layout.Height), (photo.Width, photo.Height));
                    }

                    using var labelFile = new MemoryStream();
                    Library.OpenLabelMap(design.Id, room.Id, layout.Id).CopyTo(labelFile);
                    var (labels, width, height) = SkiaRoomImaging.DecodeGrayPng(labelFile.ToArray());
                    // The label map is the analysis size: the photo scaled down, same shape.
                    Assert.True(Math.Abs((double)width / height - (double)layout.Width / layout.Height) < 0.01, $"{name}: the label map's shape differs from the photo's.");

                    var present = labels.Where(l => l > 0).Select(l => (int)l).ToHashSet();
                    var listed = layout.Objects.Select(o => o.Index).ToHashSet();
                    Assert.True(present.IsSubsetOf(listed), $"{name}: pixels of unlisted objects {string.Join(", ", present.Except(listed))}.");
                    Assert.True(listed.IsSubsetOf(present), $"{name}: objects without pixels {string.Join(", ", listed.Except(present))}.");
                }
            }
        }
    }

    [Fact]
    public async Task Copying_a_layout_stores_its_files_and_saves_a_ready_room()
    {
        var design = Library.Designs[0];
        var source = design.Rooms[0];
        var layout = source.Layouts[^1];

        var room = await CreateCopy().HandleAsync(design.Id, source.Id, layout.Id, AiMode.Fake, CancellationToken.None);

        Assert.NotNull(room);
        Assert.Equal(RoomStatus.Ready, room.Status);
        Assert.Equal("user-1", room.OwnerId);
        Assert.Equal($"{design.Id}/{source.Id}/{layout.Id}", room.LibraryRoom);
        Assert.Equal(layout.Objects, room.Objects);
        Assert.Equal((layout.Width, layout.Height), (room.Width, room.Height));
        Assert.Equal(0, room.AnalysisAttempts);
        Assert.Same(room, Assert.Single(_rooms.Saved));
        Assert.Equal(
            [DesignApp.Application.Constants.Rooms.LabelMapKey(room.Id), DesignApp.Application.Constants.Rooms.PhotoKey(room.Id)],
            _storage.Files.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task An_unknown_layout_room_or_design_copies_nothing()
    {
        var design = Library.Designs[0];
        var room = design.Rooms[0];

        Assert.Null(await CreateCopy().HandleAsync(design.Id, room.Id, "no-such-layout", AiMode.Fake, CancellationToken.None));
        Assert.Null(await CreateCopy().HandleAsync(design.Id, "no-such-room", room.Layouts[0].Id, AiMode.Fake, CancellationToken.None));
        Assert.Null(await CreateCopy().HandleAsync("no-such-design", room.Id, room.Layouts[0].Id, AiMode.Fake, CancellationToken.None));
        Assert.Empty(_storage.Files);
        Assert.Empty(_rooms.Saved);
    }

    [Fact]
    public async Task A_copy_that_cannot_be_saved_leaves_no_files()
    {
        _rooms.FailOnSave = true;
        var design = Library.Designs[0];

        await Assert.ThrowsAnyAsync<Exception>(() => CreateCopy().HandleAsync(design.Id, design.Rooms[0].Id, design.Rooms[0].Layouts[0].Id, AiMode.Fake, CancellationToken.None));

        Assert.Empty(_storage.Files);
    }

    [Fact]
    public void A_library_room_is_ready_and_remembers_where_it_came_from()
    {
        var objects = new[] { new DetectedObject(1, "sofa", ObjectCategory.Furniture, new BoundingBox(0.1, 0.1, 0.5, 0.5), HexColor.Parse("#808080")) };

        var room = Room.FromLibrary(Guid.CreateVersion7(), "user-1", "photo.jpg", 800, 600, Now, objects, "labels.png", "house/living/2", AiMode.Real);

        Assert.Equal(RoomStatus.Ready, room.Status);
        Assert.Equal("house/living/2", room.LibraryRoom);
        Assert.Equal(AiMode.Real, room.AiMode);
        Assert.Equal(0, room.LabelMapRevision);
        Assert.Throws<DomainException>(() => Room.FromLibrary(Guid.CreateVersion7(), "user-1", "photo.jpg", 800, 600, Now, [], "labels.png", "house/living", AiMode.Real));
    }

    [Fact]
    public void A_room_packs_into_the_library_files_without_objects_that_have_no_pixels()
    {
        var design = Library.Designs[0];
        var room = design.Rooms[0];
        var source = room.Layouts[0];
        using var photo = new MemoryStream();
        Library.OpenPhoto(design.Id, room.Id, source.Id).CopyTo(photo);
        using var labels = new MemoryStream();
        Library.OpenLabelMap(design.Id, room.Id, source.Id).CopyTo(labels);
        var removed = new DetectedObject(250, "removed lamp", ObjectCategory.Lighting, new BoundingBox(0, 0, 0.1, 0.1), HexColor.Parse("#FFFFFF"));

        var zip = Library.Pack(new LibraryExport("den", photo.ToArray(), labels.ToArray(), source.Width, source.Height, [.. source.Objects, removed]));

        using var archive = new ZipArchive(new MemoryStream(zip));
        Assert.Equal(["den.jpg", "den.labels.png", "den.objects.json"], archive.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        using var json = archive.GetEntry("den.objects.json")!.Open();
        var file = JsonDocument.Parse(json).RootElement;
        Assert.Equal(source.Width, file.GetProperty("width").GetInt32());
        var indexes = file.GetProperty("objects").EnumerateArray().Select(o => o.GetProperty("index").GetInt32()).ToList();
        Assert.Equal(source.Objects.Select(o => o.Index), indexes);
        Assert.Equal(source.Objects[0].Category.ToString(), file.GetProperty("objects")[0].GetProperty("category").GetString());
    }
}

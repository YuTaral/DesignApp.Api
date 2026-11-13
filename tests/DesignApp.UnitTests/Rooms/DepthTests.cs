using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Fakes;
using DesignApp.Infrastructure.Ai.Fal;
using DesignApp.Infrastructure.Photos;
using DesignApp.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Rooms;

/// <summary>
/// Depth for the 3D view: reading MoGe-2's point cloud, putting it into the grid and back out of the PNG, the fake's
/// box room, and measuring a room once and reusing it.
/// </summary>
public class DepthTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeStorage _storage = new();
    private static readonly RoomLocks Locks = new();

    private readonly FakeRooms _rooms = new();

    [Fact]
    public void A_point_cloud_is_read_camera_style()
    {
        // MoGe-2 writes OpenGL style: y up, the camera looking down -z.
        var ply = Ply([new Vector3(1, 2, -3), new Vector3(-0.5f, -1, -4)]);

        var points = PlyCloud.Read(ply);

        Assert.Equal([new Vector3(1, -2, 3), new Vector3(-0.5f, 1, 4)], points);
        Assert.Throws<FormatException>(() => PlyCloud.Read("not a ply"u8));
    }

    [Fact]
    public void Points_land_in_their_cells_nearest_first_and_holes_are_filled()
    {
        const int width = 8, height = 6;
        const double fx = 0.6, fy = 0.8, cx = 0.5, cy = 0.5;
        // One point at the center of every cell at depth 2, except the middle row at depth 3 and one cell left empty;
        // and a farther point in the first cell that must lose to the nearer one.
        var points = new List<Vector3>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if ((x, y) == (4, 1))
                    continue;
                var z = y == 3 ? 3f : 2f;
                var (u, v) = ((x + 0.5) / width, (y + 0.5) / height);
                points.Add(new Vector3((float)((u - cx) / fx * z), (float)((v - cy) / fy * z), z));
            }
        }
        points.Add(new Vector3((float)((0.5 / width - cx) / fx * 9), (float)((0.5 / height - cy) / fy * 9), 9));

        var grid = DepthGrid.Rasterize([.. points], fx, fy, cx, cy, width, height);

        Assert.Equal(2f, grid[0], 3);
        Assert.Equal(3f, grid[3 * width + 2], 3);
        Assert.Equal(0f, grid[1 * width + 4]);
        Assert.True(DepthGrid.FillHoles(grid, width, height));
        Assert.Equal(2f, grid[1 * width + 4], 3);
    }

    [Fact]
    public void A_depth_map_keeps_each_cell_relative_to_the_farthest()
    {
        float[] grid = [1, 2, 3, 4];

        var (depth, width, height) = DepthGrid.FromPng(DepthGrid.ToPng(grid, 2, 2));

        Assert.Equal((2, 2), (width, height));
        Assert.Equal([0.25f, 0.5f, 0.75f, 1f], depth.Select(d => MathF.Round(d, 3)));
    }

    [Fact]
    public async Task The_fake_puts_furniture_in_front_of_the_wall_behind_it()
    {
        // A wall everywhere, with a chair (index 2) in the lower middle.
        var labels = new SKBitmap(new SKImageInfo(400, 300, SKColorType.Gray8, SKAlphaType.Opaque));
        labels.Erase(new SKColor(1, 1, 1));
        using (var canvas = new SKCanvas(labels))
            canvas.DrawRect(150, 150, 100, 120, new SKPaint { Color = new SKColor(2, 2, 2) });
        using var png = SKImage.FromBitmap(labels).Encode(SKEncodedImageFormat.Png, 100);
        DetectedObject[] objects =
        [
            new(1, "back wall", ObjectCategory.Wall, new BoundingBox(0, 0, 1, 1), HexColor.Parse("#EEEEEE")),
            new(2, "chair", ObjectCategory.Furniture, new BoundingBox(0.375, 0.5, 0.25, 0.4), HexColor.Parse("#884422")),
        ];
        var fake = new FakeDepthProvider(Options.Create(new AiOptions { UseFakes = true }));

        var result = await fake.MeasureAsync(new DepthInput(TestImages.Create(400, 300), 400, 300, png.ToArray(), objects), CancellationToken.None);

        var (depth, width, height) = DepthGrid.FromPng(result.Value.Png);
        Assert.Equal((512, 384), (width, height));
        var at = (double u, double v) => depth[(int)(v * height) * width + (int)(u * width)];
        Assert.True(at(0.5, 0.6) < at(0.5, 0.3), "The chair is nearer than the back wall above it.");
        Assert.True(at(0.02, 0.5) < at(0.5, 0.3), "The side walls are nearer at the edges than the back wall.");
        Assert.InRange(result.Value.FovY, 40, 50);
    }

    [Fact]
    public async Task A_room_is_measured_once_and_then_reused()
    {
        var provider = new CountingDepth();
        var room = await ReadyRoomAsync();

        var first = await CreateHandler(provider).HandleAsync(room.Id, CancellationToken.None);
        var second = await CreateHandler(provider).HandleAsync(room.Id, CancellationToken.None);

        Assert.True(first!.Succeeded);
        Assert.Equal(DesignApp.Application.Constants.Rooms.DepthKey(room.Id), first.Value.DepthKey);
        Assert.Equal((70.0, 55.0), (first.Value.DepthFovX!.Value, first.Value.DepthFovY!.Value));
        Assert.True(_storage.Files.ContainsKey(first.Value.DepthKey!));
        Assert.True(second!.Succeeded);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task Two_requests_at_once_measure_the_room_only_once()
    {
        var provider = new CountingDepth { Delay = TimeSpan.FromMilliseconds(100) };
        var room = await ReadyRoomAsync();

        var both = await Task.WhenAll(
            CreateHandler(provider).HandleAsync(room.Id, CancellationToken.None),
            CreateHandler(provider).HandleAsync(room.Id, CancellationToken.None));

        Assert.All(both, result => Assert.True(result!.Succeeded));
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task A_failed_measurement_or_save_leaves_no_depth_map()
    {
        var room = await ReadyRoomAsync();

        var failed = await CreateHandler(new CountingDepth { Fail = true }).HandleAsync(room.Id, CancellationToken.None);
        Assert.False(failed!.Succeeded);
        Assert.Null(room.DepthKey);

        _rooms.FailOnSave = true;
        await Assert.ThrowsAnyAsync<Exception>(() => CreateHandler(new CountingDepth()).HandleAsync(room.Id, CancellationToken.None));
        Assert.False(_storage.Files.ContainsKey(DesignApp.Application.Constants.Rooms.DepthKey(room.Id)));
    }

    [Fact]
    public async Task Someone_elses_room_or_one_not_analyzed_is_refused()
    {
        var room = await ReadyRoomAsync();
        var upload = Room.Upload(Guid.CreateVersion7(), "user-1", "photo.jpg", 400, 300, Now);
        _rooms.Add(upload);
        await _rooms.SaveChangesAsync(CancellationToken.None);

        Assert.Null(await new MeasureRoomDepth(_rooms, _storage, Providers(new CountingDepth()), new FixedUser("someone-else"), Locks, NullLogger<MeasureRoomDepth>.Instance)
            .HandleAsync(room.Id, CancellationToken.None));
        var notReady = await CreateHandler(new CountingDepth()).HandleAsync(upload.Id, CancellationToken.None);
        Assert.False(notReady!.Succeeded);
        Assert.Throws<DomainException>(() => room.SetDepth("depth.png", 0, 50));
    }

    /// <summary>The use case wired to this test's fakes.</summary>
    /// <param name="provider">The depth provider.</param>
    private MeasureRoomDepth CreateHandler(IDepthProvider provider)
    {
        return new MeasureRoomDepth(_rooms, _storage, Providers(provider), new FixedUser("user-1"), Locks, NullLogger<MeasureRoomDepth>.Instance);
    }

    /// <summary>AI providers whose depth step is the given one.</summary>
    /// <param name="provider">The depth provider.</param>
    private static FixedAiProviders Providers(IDepthProvider provider)
    {
        return new FixedAiProviders(null!, null!, depth: provider);
    }

    /// <summary>A saved, analyzed room of user-1 with its photo and label map stored.</summary>
    private async Task<Room> ReadyRoomAsync()
    {
        var id = Guid.CreateVersion7();
        var objects = new[] { new DetectedObject(1, "wall", ObjectCategory.Wall, new BoundingBox(0, 0, 1, 1), HexColor.Parse("#EEEEEE")) };
        var room = Room.FromLibrary(id, "user-1", DesignApp.Application.Constants.Rooms.PhotoKey(id), 400, 300, Now, objects,
            DesignApp.Application.Constants.Rooms.LabelMapKey(id), "house/room/1", AiMode.Fake);
        await _storage.SaveAsync(room.PhotoKey, new MemoryStream(TestImages.Create(400, 300)), "image/jpeg", CancellationToken.None);
        await _storage.SaveAsync(room.LabelMapKey!, new MemoryStream(TestImages.Create(400, 300, SKEncodedImageFormat.Png)), "image/png", CancellationToken.None);
        _rooms.Add(room);
        await _rooms.SaveChangesAsync(CancellationToken.None);
        return room;
    }

    /// <summary>A binary little-endian PLY of points, as MoGe-2 writes it (with colors after x, y, z).</summary>
    /// <param name="points">The points, OpenGL style.</param>
    private static byte[] Ply(Vector3[] points)
    {
        var header = $"ply\nformat binary_little_endian 1.0\nelement vertex {points.Length}\nproperty float x\nproperty float y\nproperty float z\n" +
            "property uchar red\nproperty uchar green\nproperty uchar blue\nproperty uchar alpha\nelement face 0\nproperty list uchar int vertex_indices\nend_header\n";
        var bytes = new List<byte>(Encoding.ASCII.GetBytes(header));
        var vertex = new byte[16];
        foreach (var p in points)
        {
            BinaryPrimitives.WriteSingleLittleEndian(vertex, p.X);
            BinaryPrimitives.WriteSingleLittleEndian(vertex.AsSpan(4), p.Y);
            BinaryPrimitives.WriteSingleLittleEndian(vertex.AsSpan(8), p.Z);
            vertex[12] = vertex[13] = vertex[14] = vertex[15] = 200;
            bytes.AddRange(vertex);
        }
        return [.. bytes];
    }

    /// <summary>A depth provider that counts its calls, and can fail.</summary>
    private sealed class CountingDepth : IDepthProvider
    {
        /// <summary>Whether it fails.</summary>
        public bool Fail { get; init; }

        /// <summary>How long it takes.</summary>
        public TimeSpan Delay { get; init; }

        /// <summary>How often it was asked.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public async Task<Result<DepthMeasurement>> MeasureAsync(DepthInput input, CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Delay(Delay, cancellationToken);
            return Fail
                ? Result.Failure<DepthMeasurement>("The depth model is down.")
                : new DepthMeasurement(DepthGrid.ToPng([1, 2, 3, 4], 2, 2), 70, 55);
        }
    }
}

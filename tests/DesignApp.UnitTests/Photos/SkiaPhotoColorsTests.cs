using DesignApp.Application.Helpers;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Photos;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

using static DesignApp.Domain.Constants;
using Ids = DesignApp.Application.Constants.Builder.MaskIds;

namespace DesignApp.UnitTests.Photos;

/// <summary>
/// The color correction of photos made from a built room: each region moves to its color in the 3D view, keeping its
/// texture; what the AI added keeps its own color; a wall's accent is told apart by hue.
/// </summary>
public class SkiaPhotoColorsTests
{
    private const int Width = 120;
    private const int Height = 90;
    private static readonly SKColor Mustard = new(214, 166, 64);
    private static readonly SKColor PaleWall = new(222, 216, 206);
    private readonly SkiaPhotoColors _colors = new(NullLogger<SkiaPhotoColors>.Instance);

    /// <summary>A picture: an object (mask id 1) on the left half, a wall on the right.</summary>
    /// <param name="left">The object's color.</param>
    /// <param name="right">The wall's color.</param>
    /// <param name="texture">How far each pixel strays from its color, in steps of a checker pattern.</param>
    /// <param name="patch">A square the AI added on the object, in its own color; <c>null</c> for none.</param>
    private static byte[] Picture(SKColor left, SKColor right, int texture = 0, SKColor? patch = null)
    {
        using var bitmap = new SKBitmap(Width, Height);

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var color = x < Width / 2 ? left : right;
                var grain = ((x / 3 + y / 3) % 3 - 1) * texture;
                var added = patch is not null && x is >= 20 and < 34 && y is >= 30 and < 44;

                bitmap.SetPixel(x, y, added ? patch!.Value : new SKColor(Shift(color.Red, grain), Shift(color.Green, grain), Shift(color.Blue, grain)));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    /// <summary>The masks: object 1 on the left half, the given id on the right.</summary>
    /// <param name="right">The right half's id.</param>
    private static byte[] Masks(int right = Ids.Wall)
    {
        return Picture(new SKColor(1, 1, 1), new SKColor((byte)right, (byte)right, (byte)right));
    }

    /// <summary>A channel moved by some steps, kept in range.</summary>
    /// <param name="value">The channel.</param>
    /// <param name="by">The steps.</param>
    private static byte Shift(byte value, int by)
    {
        return (byte)Math.Clamp(value + by, 0, 255);
    }

    /// <summary>The Lab color of a pixel of a picture.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="x">Across.</param>
    /// <param name="y">Down.</param>
    private static (float L, float A, float B) At(byte[] picture, int x, int y)
    {
        using var bitmap = SKBitmap.Decode(picture);
        var color = bitmap.GetPixel(x, y);

        return LabColor.FromRgb(color.Red, color.Green, color.Blue);
    }

    /// <summary>The average Lab color of a square of a picture, and how much its lightness varies.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="x0">The square's left edge.</param>
    /// <param name="y0">Its top edge.</param>
    /// <param name="size">Its size.</param>
    private static ((double L, double A, double B) Mean, double Spread) Area(byte[] picture, int x0, int y0, int size)
    {
        var colors = new List<(float L, float A, float B)>();

        using var bitmap = SKBitmap.Decode(picture);

        for (var y = y0; y < y0 + size; y++)
        {
            for (var x = x0; x < x0 + size; x++)
            {
                var color = bitmap.GetPixel(x, y);

                colors.Add(LabColor.FromRgb(color.Red, color.Green, color.Blue));
            }
        }

        var mean = (colors.Average(c => c.L), colors.Average(c => c.A), colors.Average(c => c.B));
        var spread = Math.Sqrt(colors.Average(c => (c.L - mean.Item1) * (c.L - mean.Item1)));

        return (mean, spread);
    }

    /// <summary>The distance between two Lab colors.</summary>
    /// <param name="a">One.</param>
    /// <param name="b">The other.</param>
    private static double Distance((double L, double A, double B) a, (double L, double A, double B) b)
    {
        return Math.Sqrt((a.L - b.L) * (a.L - b.L) + (a.A - b.A) * (a.A - b.A) + (a.B - b.B) * (a.B - b.B));
    }

    /// <summary>The regions: object 1, and the wall in the given planned colors.</summary>
    /// <param name="walls">The wall's planned colors.</param>
    private static PhotoRegions Regions(params string[] walls)
    {
        return new PhotoRegions(new HashSet<int> { 1 }, new Dictionary<int, IReadOnlyList<HexColor>> { [Ids.Wall] = [.. walls.Select(HexColor.Parse)] });
    }

    [Fact]
    public void Each_region_takes_its_color_in_the_3D_view_and_keeps_its_texture()
    {
        var guide = Picture(Mustard, PaleWall);
        // The AI's photo: a darker, browner object (like plain wood for a mustard table) and a greyer wall, textured.
        var photo = Picture(new SKColor(150, 118, 88), new SKColor(184, 176, 166), texture: 12);

        var fixedPhoto = _colors.Correct(photo, guide, Masks(), Regions("#DED8CE"));
        var (table, tableGrain) = Area(fixedPhoto, 12, 12, 30);
        var (wall, wallGrain) = Area(fixedPhoto, 78, 12, 30);
        var (_, photoGrain) = Area(photo, 12, 12, 30);

        Assert.InRange(Distance(table, At(guide, 30, 30)), 0, 3);
        Assert.InRange(Distance(wall, At(guide, 90, 30)), 0, 3);
        // The texture stays: the grain varies about as much as before.
        Assert.InRange(tableGrain, photoGrain * 0.6, photoGrain * 1.6);
        Assert.True(wallGrain > 1, $"the wall's grain is gone ({wallGrain:0.0})");
    }

    [Fact]
    public void Something_the_AI_added_keeps_its_own_color()
    {
        var blue = new SKColor(40, 70, 200);
        var guide = Picture(Mustard, PaleWall);
        var photo = Picture(new SKColor(150, 118, 88), new SKColor(184, 176, 166), patch: blue);

        var fixedPhoto = _colors.Correct(photo, guide, Masks(), Regions("#DED8CE"));
        var before = At(photo, 27, 37);
        var after = At(fixedPhoto, 27, 37);
        var tableMove = Distance(At(fixedPhoto, 45, 70), At(photo, 45, 70));

        // The table moves a long way; the blue throw on it barely.
        Assert.True(tableMove > 20, $"the table moved only {tableMove:0.0}");
        Assert.InRange(Distance(before, after), 0, tableMove / 4);
    }

    [Fact]
    public void A_wall_and_its_accent_are_told_apart_by_hue_though_the_3D_view_darkens_them()
    {
        // A pale blue wall and a green accent, both darkened by the 3D view's shading; the photo greys them both.
        var guide = Picture(new SKColor(110, 128, 104), new SKColor(150, 160, 172));
        var photo = Picture(new SKColor(118, 118, 116), new SKColor(176, 174, 170));
        var masks = Picture(new SKColor(Ids.Wall, Ids.Wall, Ids.Wall), new SKColor(Ids.Wall, Ids.Wall, Ids.Wall));

        var fixedPhoto = _colors.Correct(photo, guide, masks, new PhotoRegions(new HashSet<int>(), new Dictionary<int, IReadOnlyList<HexColor>>
        {
            [Ids.Wall] = [HexColor.Parse("#C9D8E4"), HexColor.Parse("#8A9A84")],
        }));

        Assert.InRange(Distance(Area(fixedPhoto, 12, 12, 30).Mean, At(guide, 30, 30)), 0, 3);
        Assert.InRange(Distance(Area(fixedPhoto, 78, 12, 30).Mean, At(guide, 90, 30)), 0, 3);
    }

    [Fact]
    public void An_object_of_two_parts_is_matched_by_the_part_the_plan_describes()
    {
        // A white cooker in the 3D view; the photo's body is a greyer white, with a black oven window across its middle.
        using var guideBitmap = new SKBitmap(Width, Height);
        using var photoBitmap = new SKBitmap(Width, Height);

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var window = x is >= 10 and < 50 && y is >= 30 and < 60;

                guideBitmap.SetPixel(x, y, x < Width / 2 ? new SKColor(236, 232, 224) : PaleWall);
                photoBitmap.SetPixel(x, y, x < Width / 2 ? (window ? new SKColor(20, 20, 22) : new SKColor(190, 186, 178)) : PaleWall);
            }
        }

        var (guide, photo) = (Png(guideBitmap), Png(photoBitmap));
        var fixedPhoto = _colors.Correct(photo, guide, Masks(), Regions("#DED8CE"));

        // The body comes back to the 3D view's white; the window stays black.
        Assert.InRange(Distance(At(fixedPhoto, 6, 10), At(guide, 6, 10)), 0, 3);
        Assert.InRange(At(fixedPhoto, 30, 45).L, 0, 15);
    }

    [Fact]
    public void A_golden_hour_photo_keeps_its_light_and_a_midday_one_takes_the_3D_views()
    {
        var guide = Picture(Mustard, PaleWall);
        // The photo's light warms and darkens its wall; the table is plain wood besides.
        var photo = Picture(new SKColor(150, 118, 88), new SKColor(214, 190, 160));

        var midday = _colors.Correct(photo, guide, Masks(), Regions("#DED8CE"));
        var golden = _colors.Correct(photo, guide, Masks(), Regions("#DED8CE") with { KeepsLight = true });
        var light = Subtract(At(photo, 90, 30), At(guide, 90, 30));

        Assert.InRange(Distance(At(midday, 90, 30), At(guide, 90, 30)), 0, 3);
        // In golden light the wall stays as the photo has it, and the table takes the 3D view's mustard in that light.
        Assert.InRange(Distance(At(golden, 90, 30), At(photo, 90, 30)), 0, 3);
        Assert.InRange(Distance(At(golden, 30, 30), Add(At(guide, 30, 30), light)), 0, 4);
    }

    /// <summary>A plan in the evening.</summary>
    /// <param name="plan">The plan, changed.</param>
    private static RoomPlan Evening(RoomPlan plan)
    {
        var f = plan.Finish;

        plan.Update(plan.Name, plan.Outline, plan.Height, plan.WallColor, plan.FloorColor, plan.FloorType, plan.View, plan.Items, DateTimeOffset.UnixEpoch,
            new RoomFinish(f.Walls, f.OwnWalls, f.CeilingColor, f.TrimColor, TimeOfDay.Evening));

        return plan;
    }

    /// <summary>A bitmap as a PNG.</summary>
    /// <param name="bitmap">The bitmap.</param>
    private static byte[] Png(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    /// <summary>One Lab color minus another.</summary>
    /// <param name="a">The one.</param>
    /// <param name="b">The other.</param>
    private static (double L, double A, double B) Subtract((float L, float A, float B) a, (float L, float A, float B) b)
    {
        return (a.L - b.L, a.A - b.A, a.B - b.B);
    }

    /// <summary>A Lab color plus a difference.</summary>
    /// <param name="a">The color.</param>
    /// <param name="b">The difference.</param>
    private static (double L, double A, double B) Add((float L, float A, float B) a, (double L, double A, double B) b)
    {
        return (a.L + b.L, a.A + b.A, a.B + b.B);
    }

    [Fact]
    public void A_photo_that_is_its_3D_view_or_unreadable_masks_leave_the_photo_as_it_is()
    {
        var guide = Picture(Mustard, PaleWall);
        var photo = Picture(new SKColor(150, 118, 88), PaleWall);

        // The fake AI's photo is its 3D view: the very same bytes come back, not a JPEG of them.
        Assert.Same(guide, _colors.Correct(guide, guide, Masks(), Regions("#DED8CE")));
        Assert.Same(photo, _colors.Correct(photo, guide, [1, 2, 3], Regions("#DED8CE")));
        Assert.Same(photo, _colors.Correct(photo, guide, Masks(), new PhotoRegions(new HashSet<int>(), new Dictionary<int, IReadOnlyList<HexColor>>())));
    }

    [Fact]
    public void Windows_and_doors_keep_their_colors_pictures_frames_dont_and_every_surface_lists_its_planned_colors()
    {
        var now = new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
        var wall = HexColor.Parse("#C9D8E4");
        var plan = RoomPlan.Create(Guid.CreateVersion7(now), "user-1", "", RoomType.Bedroom, 4, 4, 2.6, wall, HexColor.Parse("#C8C8C8"), FloorType.Vinyl, AiMode.Fake, now);
        var red = HexColor.Parse("#B23A3A");
        var tiles = HexColor.Parse("#FFFFFF");
        var brick = HexColor.Parse("#B5543C");

        plan.Update("", plan.Outline, 2.6, wall, plan.FloorColor, FloorType.Vinyl, new PlanView((int)PlanWall.Back, ViewCorner.None),
            [
                new PlanItem("bed", ItemShape.Bed, 200, 200, 160, 200, 100, 0, red),
                new PlanItem("window", ItemShape.Window, 200, 0, 100, 10, 120, 0, red, 0, 90),
                new PlanItem("painting", ItemShape.Picture, 100, 0, 60, 3, 40, 0, red, 0, 140),
                new PlanItem("floor area", ItemShape.FloorZone, 300, 300, 100, 100, 1, 0, tiles, material: nameof(FloorType.Tiles)),
            ], now, new RoomFinish(WallFinish.Paint, [new WallLook(3, WallFinish.Brick, brick)], RoomFinish.Default.CeilingColor, RoomFinish.Default.TrimColor,
                TimeOfDay.Midday));
        var regions = PhotoRegionsOf.Plan(plan);

        // The bed and the painting (whose masks show only its frame); not the window, nor the floor area (a surface).
        Assert.Equal([1, 3], regions.Objects.Order());
        Assert.False(regions.KeepsLight);
        Assert.True(PhotoRegionsOf.Plan(Evening(plan)).KeepsLight);
        Assert.Equal([plan.FloorColor, tiles], regions.Surfaces[Ids.Floor]);
        Assert.Equal([wall, brick], regions.Surfaces[Ids.Wall]);
        Assert.Equal([plan.Finish.CeilingColor], regions.Surfaces[Ids.Ceiling]);
    }
}

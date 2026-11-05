using DesignApp.Application.Analysis;
using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;
using DesignApp.Infrastructure.Photos;
using DesignApp.Tests;
using SkiaSharp;

namespace DesignApp.UnitTests.Analysis;

/// <summary>Scaling photos for analysis and building the label map.</summary>
public class SkiaRoomImagingTests
{
    private readonly SkiaRoomImaging _imaging = new();

    [Fact]
    public void Large_photos_are_scaled_down_for_analysis()
    {
        var image = _imaging.PrepareForAnalysis(new MemoryStream(TestImages.Create(4096, 2048)));

        Assert.Equal((AnalysisRules.MaxLongEdge, AnalysisRules.MaxLongEdge / 2), (image.Width, image.Height));
        using var decoded = TestImages.Decode(image.Jpeg);
        Assert.Equal((image.Width, image.Height), (decoded.Width, decoded.Height));
    }

    [Fact]
    public void Small_photos_are_used_as_they_are()
    {
        var jpeg = TestImages.Create(800, 600);

        var image = _imaging.PrepareForAnalysis(new MemoryStream(jpeg));

        Assert.Equal((800, 600), (image.Width, image.Height));
        Assert.Equal(jpeg, image.Jpeg);
    }

    [Fact]
    public void Smaller_objects_sit_on_top_and_keep_the_inventory_order()
    {
        var image = Image(200, 100);
        var wall = Item("wall", ObjectCategory.Wall);
        var sofa = Item("sofa", ObjectCategory.Furniture);

        // The wall is listed second but painted first, being larger, so the sofa stays on top of it.
        var map = _imaging.BuildLabelMap(image, [new(sofa, Rect(200, 100, 50, 50, 100, 40)), new(wall, Rect(200, 100, 0, 0, 200, 100))]);

        Assert.Equal(["sofa", "wall"], map.Objects.Select(o => o.Label));
        var labels = DecodeLabels(map.Png);
        Assert.Equal(1, labels[60 * 200 + 100]); // inside the sofa
        Assert.Equal(2, labels[10 * 200 + 10]);  // wall around it
    }

    [Fact]
    public void Boxes_are_measured_from_the_visible_pixels()
    {
        // TestImages: a red top-left quadrant on blue; the mask covers exactly the red part, so the
        // color edge stops the cleanup from moving its border.
        var image = new AnalysisImage(TestImages.Create(200, 100), 200, 100);

        var map = _imaging.BuildLabelMap(image, [new(Item("rug", ObjectCategory.Textile), Rect(200, 100, 0, 0, 100, 50))]);

        var box = Assert.Single(map.Objects).Box;
        Assert.Equal((0, 0, 0.5, 0.5), (Math.Round(box.X, 2), Math.Round(box.Y, 2), Math.Round(box.Width, 2), Math.Round(box.Height, 2)));
    }

    [Fact]
    public void Objects_left_with_almost_no_pixels_are_dropped()
    {
        var specks = Rect(200, 100, 0, 0, 2, 2); // 4 of 20 000 pixels, under the minimum
        var map = _imaging.BuildLabelMap(Image(200, 100), [new(Item("speck", ObjectCategory.Decor), specks)]);

        Assert.Empty(map.Objects);
    }

    [Fact]
    public void The_main_color_comes_from_the_photo()
    {
        // TestImages: red top-left quadrant on blue. The mask covers only the red part.
        var image = new AnalysisImage(TestImages.Create(200, 100), 200, 100);

        var map = _imaging.BuildLabelMap(image, [new(Item("cushion", ObjectCategory.Textile), Rect(200, 100, 0, 0, 100, 50))]);

        var color = SKColor.Parse(Assert.Single(map.Objects).DominantColor.Value);
        Assert.InRange(color.Red, TestImages.Red.Red - 30, 255);
        Assert.InRange(color.Blue, 0, TestImages.Red.Blue + 30);
    }

    [Fact]
    public void Masks_of_another_size_are_scaled_to_fit()
    {
        // A half-size mask covering the right half. A sofa (not a surface) grows a few pixels at most.
        var map = _imaging.BuildLabelMap(Image(200, 100), [new(Item("sofa", ObjectCategory.Furniture), Rect(100, 50, 50, 0, 50, 50))]);

        var labels = DecodeLabels(map.Png);
        Assert.Equal(0, labels[50 * 200 + 50]);
        Assert.Equal(1, labels[50 * 200 + 150]);
    }

    [Fact]
    public void A_wall_fills_the_gaps_its_mask_missed_up_to_a_color_edge()
    {
        // Red top-left quadrant on blue. The wall's mask covers only part of the blue, with a
        // ragged gap; the gap is blue too, so the wall takes it. The red quadrant has a real edge.
        var image = new AnalysisImage(TestImages.Create(200, 100), 200, 100);
        var ragged = Rect(200, 100, 100, 0, 100, 100);
        for (var y = 0; y < 100; y++)
            for (var x = 100; x < 130; x++)
                ragged.Coverage[y * 200 + x] = (byte)((x + y) % 3 == 0 ? 0 : 255);

        var map = _imaging.BuildLabelMap(image, [new(Item("wall", ObjectCategory.Wall), ragged)]);

        var labels = DecodeLabels(map.Png);
        Assert.All(Enumerable.Range(100, 30), x => Assert.Equal(1, labels[10 * 200 + x])); // the ragged strip, filled
        Assert.Equal(1, labels[80 * 200 + 95]);   // blue just past the mask: within reach
        Assert.Equal(0, labels[80 * 200 + 20]);   // blue far from the mask: too far to claim
        Assert.Equal(0, labels[25 * 200 + 50]);   // inside the red quadrant: behind a color edge
    }

    [Fact]
    public void Holes_inside_an_object_are_filled()
    {
        // A mirror whose mask has a hole where it reflects something of a very different color.
        var image = new AnalysisImage(TestImages.Create(200, 100), 200, 100);
        var mirror = Rect(200, 100, 110, 10, 80, 80);
        for (var y = 40; y < 60; y++)
            for (var x = 140; x < 160; x++)
                mirror.Coverage[y * 200 + x] = 0;

        var map = _imaging.BuildLabelMap(image, [new(Item("mirror", ObjectCategory.Decor), mirror)]);

        Assert.Equal(1, DecodeLabels(map.Png)[50 * 200 + 150]);
    }

    [Fact]
    public void Stray_speckles_of_an_object_are_removed()
    {
        // A blanket with a 3x3 speck far away from it, on a different color.
        var image = new AnalysisImage(TestImages.Create(200, 100), 200, 100);
        var blanket = Rect(200, 100, 0, 0, 100, 50);
        for (var y = 80; y < 83; y++)
            for (var x = 180; x < 183; x++)
                blanket.Coverage[y * 200 + x] = 255;

        var map = _imaging.BuildLabelMap(image, [new(Item("blanket", ObjectCategory.Textile), blanket)]);

        var labels = DecodeLabels(map.Png);
        Assert.Equal(0, labels[81 * 200 + 181]);
        Assert.Equal(1, labels[25 * 200 + 50]);
    }

    /// <summary>A plain gray analysis image.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    private static AnalysisImage Image(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Gray);
        using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Jpeg, 90);
        return new AnalysisImage(encoded.ToArray(), width, height);
    }

    /// <summary>An inventory item; its box doesn't matter to the label map.</summary>
    /// <param name="label">The item's label.</param>
    /// <param name="category">The item's category.</param>
    private static InventoryItem Item(string label, ObjectCategory category) =>
        new(label, category, new BoundingBox(0, 0, 1, 1), HexColor.Parse("#808080"));

    /// <summary>A mask with one filled rectangle.</summary>
    /// <param name="width">Mask width.</param>
    /// <param name="height">Mask height.</param>
    /// <param name="x">Rectangle left.</param>
    /// <param name="y">Rectangle top.</param>
    /// <param name="w">Rectangle width.</param>
    /// <param name="h">Rectangle height.</param>
    private static ObjectMask Rect(int width, int height, int x, int y, int w, int h)
    {
        var coverage = new byte[width * height];
        for (var row = y; row < y + h; row++)
            Array.Fill(coverage, (byte)255, row * width + x, w);
        return new ObjectMask(width, height, coverage);
    }

    /// <summary>Reads the label map's pixel values back.</summary>
    /// <param name="png">The label map PNG.</param>
    private static byte[] DecodeLabels(byte[] png)
    {
        using var codec = SKCodec.Create(new MemoryStream(png));
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        using var bitmap = SKBitmap.Decode(codec, info);
        return bitmap.GetPixelSpan().ToArray();
    }
}

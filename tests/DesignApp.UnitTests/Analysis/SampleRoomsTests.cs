using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Fakes;
using DesignApp.Tests;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace DesignApp.UnitTests.Analysis;

/// <summary>The traced sample rooms, and how the fake providers recognize a sample photo.</summary>
public class SampleRoomsTests
{
    private static readonly IOptions<AiOptions> NoDelay = Options.Create(new AiOptions { UseFakes = true });
    private static readonly System.Text.Json.JsonSerializerOptions SampleJson =
        new(System.Text.Json.JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public static TheoryData<string> SampleNames()
    {
        return [.. SampleRooms.All.Select(s => s.Name)];
    }

    [Fact]
    public void All_five_samples_are_embedded()
    {
        Assert.Equal(["bathroom", "bedroom", "kitchen", "kitchen_with_table", "minimalist_living_room"], SampleRooms.All.Select(s => s.Name));
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Each_sample_has_uniquely_labelled_objects_with_outlines(string name)
    {
        var sample = SampleRooms.All.Single(s => s.Name == name);

        Assert.InRange(sample.Objects.Count, 10, DesignApp.Application.Constants.Analysis.MaxObjects);
        Assert.Equal(sample.Objects.Count, sample.Objects.Select(o => o.Label).Distinct().Count());
        Assert.All(sample.Objects, o => Assert.NotEmpty(o.Shapes));
        Assert.Equal(256, sample.FingerprintBytes.Length);
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Every_traced_object_survives_into_the_label_map(string name)
    {
        var sample = SampleRooms.All.Single(s => s.Name == name);
        var image = LookAlike(sample);

        var outlined = sample.Inventory()
            .Select(item => new SegmentedObject(item, sample.Outline(item.Label, image.Width, image.Height)!))
            .ToList();
        var map = new Infrastructure.Photos.SkiaRoomImaging().BuildLabelMap(image, outlined);

        // No object is so small, or so covered by others, that it gets dropped.
        Assert.Equal(sample.Objects.Select(o => o.Label), map.Objects.Select(o => o.Label));
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public async Task A_sample_photo_gets_its_own_objects_and_outlines(string name)
    {
        var sample = SampleRooms.All.Single(s => s.Name == name);
        var image = LookAlike(sample);

        var items = (await new FakeInventoryProvider(NoDelay).ListObjectsAsync(image, CancellationToken.None)).Value;
        var masks = await new FakeSegmentationProvider(NoDelay).SegmentAsync(image, items, CancellationToken.None);

        Assert.Equal(sample.Objects.Select(o => o.Label), items.Select(i => i.Label));
        Assert.All(masks, m => Assert.Contains(m!.Coverage, c => c == 255));
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Outlines_are_drawn_at_the_analysis_size(string name)
    {
        var sample = SampleRooms.All.Single(s => s.Name == name);
        var label = sample.Objects[0].Label;

        var full = sample.Outline(label, sample.Width, sample.Height)!;
        var half = sample.Outline(label, sample.Width / 2, sample.Height / 2)!;

        Assert.Equal((sample.Width / 2, sample.Height / 2), (half.Width, half.Height));
        var ratio = full.Coverage.Count(c => c >= 128) / (double)half.Coverage.Count(c => c >= 128);

        Assert.InRange(ratio, 3.6, 4.4);
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Inventory_boxes_come_from_the_outlines(string name)
    {
        var sample = SampleRooms.All.Single(s => s.Name == name);

        foreach (var item in sample.Inventory())
        {
            var mask = sample.Outline(item.Label, sample.Width, sample.Height)!;
            var (minX, minY, maxX, maxY) = (int.MaxValue, int.MaxValue, -1, -1);

            for (var y = 0; y < mask.Height; y++)
            {
                for (var x = 0; x < mask.Width; x++)
                {
                    if (mask.Coverage[y * mask.Width + x] >= 128)
                    {
                        (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
                    }
                }
            }

            // Within 2 px: polygon edges and stroke ends round to whole pixels.
            Assert.InRange(item.Box.X * sample.Width, minX - 2, minX + 2);
            Assert.InRange(item.Box.Y * sample.Height, minY - 2, minY + 2);
            Assert.InRange((item.Box.X + item.Box.Width) * sample.Width, maxX - 1, maxX + 3);
            Assert.InRange((item.Box.Y + item.Box.Height) * sample.Height, maxY - 1, maxY + 3);
        }
    }

    [Fact]
    public void A_recorded_mask_plays_back_exactly()
    {
        // A 40x20 mask covering the left half, as the recorder stores it.
        using var bitmap = new SKBitmap(new SKImageInfo(40, 20, SKColorType.Gray8, SKAlphaType.Opaque));
        var coverage = new byte[40 * 20];

        for (var y = 0; y < 20; y++)
        {
            Array.Fill(coverage, (byte)255, y * 40, 20);
        }

        coverage.CopyTo(bitmap.GetPixelSpan());
        using var png = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        var json = $$"""
            {"name": "test", "width": 40, "height": 20, "fingerprint": "{{new string('0', 512)}}",
             "objects": [{"label": "sofa", "category": "Furniture", "shapes": [{"mask": "{{Convert.ToBase64String(png.ToArray())}}"}]}]}
            """;
        var sample = System.Text.Json.JsonSerializer.Deserialize<SampleRoom>(json, SampleJson)!;

        var mask = sample.Outline("sofa", 40, 20)!;
        var box = sample.Inventory().Single().Box;

        Assert.Equal(coverage, mask.Coverage);
        Assert.Equal((0, 0, 0.5, 1), (box.X, box.Y, box.Width, box.Height));
    }

    [Fact]
    public async Task Any_other_photo_gets_the_generic_layout()
    {
        var image = new AnalysisImage(TestImages.Create(975, 477), 975, 477); // bathroom's shape, different content

        var items = (await new FakeInventoryProvider(NoDelay).ListObjectsAsync(image, CancellationToken.None)).Value;

        Assert.Null(SampleRooms.Match(image));
        Assert.Equal(FakeInventoryProvider.Layout, items);
    }

    [Fact]
    public void Different_samples_are_far_apart()
    {
        var pairs = SampleRooms.All.SelectMany(a => SampleRooms.All.Where(b => string.CompareOrdinal(a.Name, b.Name) < 0), (a, b) => (a, b));

        Assert.All(pairs, p => Assert.True(
            SampleRooms.Difference(p.a.FingerprintBytes, p.b.FingerprintBytes) > DesignApp.Infrastructure.Constants.Samples.MaxDifference * 2,
            $"{p.a.Name} and {p.b.Name} look too alike to tell apart."));
    }

    /// <summary>
    /// A stand-in for the sample's photo with the same size and fingerprint: each grid cell filled
    /// with the cell's brightness, then JPEG-encoded like an upload.
    /// </summary>
    /// <param name="sample">The sample to imitate.</param>
    private static AnalysisImage LookAlike(SampleRoom sample)
    {
        using var bitmap = new SKBitmap(sample.Width, sample.Height);

        using (var canvas = new SKCanvas(bitmap))
        {
            for (var cy = 0; cy < 16; cy++)
            {
                for (var cx = 0; cx < 16; cx++)
                {
                    var v = sample.FingerprintBytes[cy * 16 + cx];
                    using var paint = new SKPaint { Color = new SKColor(v, v, v) };

                    canvas.DrawRect(SKRect.Create(cx * sample.Width / 16, cy * sample.Height / 16,
                        (cx + 1) * sample.Width / 16 - cx * sample.Width / 16, (cy + 1) * sample.Height / 16 - cy * sample.Height / 16), paint);
                }
            }
        }

        using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Jpeg, 90);

        return new AnalysisImage(encoded.ToArray(), sample.Width, sample.Height);
    }

    /// <summary>Compares pairs of numbers within a tolerance.</summary>
    /// <param name="tolerance">The allowed difference.</param>
    private sealed class ToleranceComparer(double tolerance) : IEqualityComparer<(double, double)>
    {
        public bool Equals((double, double) x, (double, double) y)
        {
            return Math.Abs(x.Item1 - y.Item1) <= tolerance && Math.Abs(x.Item2 - y.Item2) <= tolerance;
        }

        public int GetHashCode((double, double) obj)
        {
            return 0;
        }
    }
}

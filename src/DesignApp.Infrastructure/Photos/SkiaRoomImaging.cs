using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;
using SkiaSharp;

namespace DesignApp.Infrastructure.Photos;

/// <summary>SkiaSharp implementation of <see cref="IRoomImaging"/>.</summary>
internal sealed class SkiaRoomImaging : IRoomImaging
{
    /// <inheritdoc/>
    public AnalysisImage PrepareForAnalysis(Stream photo)
    {
        using var buffer = new MemoryStream();
        photo.CopyTo(buffer);
        var jpeg = buffer.ToArray();

        using var source = SKBitmap.Decode(jpeg) ?? throw new InvalidOperationException("The stored photo can't be decoded.");
        var scale = Math.Min(1f, (float)Application.Constants.Analysis.MaxLongEdge / Math.Max(source.Width, source.Height));
        if (scale >= 1f)
            return new AnalysisImage(jpeg, source.Width, source.Height);

        var info = new SKImageInfo(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale)),
            SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        using var scaled = source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var image = SKImage.FromBitmap(scaled);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, Application.Constants.Photos.JpegQuality);
        return new AnalysisImage(encoded.ToArray(), info.Width, info.Height);
    }

    /// <inheritdoc/>
    public LabelMap BuildLabelMap(AnalysisImage image, IReadOnlyList<SegmentedObject> objects)
    {
        if (objects.Count > Domain.Constants.Rooms.MaxObjectIndex)
            objects = [.. objects.Take(Domain.Constants.Rooms.MaxObjectIndex)];
        var (width, height) = (image.Width, image.Height);
        var labels = new byte[width * height];

        // Paint the largest outlines first, so smaller objects in front of them stay on top.
        // Paint ids are list positions + 1; objects are renumbered once we know which survive.
        var paintOrder = objects
            .Select((o, i) => (Object: o, PaintId: (byte)(i + 1), Area: CountCovered(o.Mask)))
            .Where(p => p.Area > 0)
            .OrderByDescending(p => p.Area / (double)(p.Object.Mask.Width * p.Object.Mask.Height))
            .ToList();
        foreach (var (o, paintId, _) in paintOrder)
            Paint(labels, width, height, o.Mask, paintId);

        // Objects too small to keep are cleared before the cleanup, so they can't grow.
        var minPixels = Application.Constants.Analysis.MinAreaFraction * width * height;
        var painted = new int[objects.Count + 1];
        foreach (var label in labels)
            painted[label]++;
        for (var p = 0; p < labels.Length; p++)
        {
            if (painted[labels[p]] < minPixels)
                labels[p] = 0;
        }

        using var photo = DecodeRgba(image);
        var pixels = photo.GetPixelSpan();
        var surfaces = objects.Select(o => o.Item.Category.IsSurface()).ToArray();
        var reach = Reach(objects, width, height);
        LabelMapRefiner.Refine(labels, width, height, pixels, label => label <= surfaces.Length && surfaces[label - 1], reach);

        var stats = Measure(labels, width, height, objects.Count, pixels);

        // Keep Claude's order (most important first) for the final indexes.
        var renumber = new byte[objects.Count + 1];
        var detected = new List<DetectedObject>();
        for (var i = 0; i < objects.Count; i++)
        {
            var s = stats[i + 1];
            if (s.Pixels < minPixels)
                continue;
            var index = detected.Count + 1;
            renumber[i + 1] = (byte)index;
            var box = new BoundingBox(
                s.MinX / (double)width, s.MinY / (double)height,
                (s.MaxX - s.MinX + 1) / (double)width, (s.MaxY - s.MinY + 1) / (double)height);
            var item = objects[i].Item;
            detected.Add(new DetectedObject(index, item.Label, item.Category, box, s.MedianColor()));
        }

        for (var p = 0; p < labels.Length; p++)
            labels[p] = renumber[labels[p]];

        return new LabelMap(EncodeGrayPng(labels, width, height), detected);
    }

    /// <summary>
    /// Per paint id, where the object may still grow in the cleanup's last pass: anywhere for a surface,
    /// otherwise Claude's box for it widened by <see cref="Constants.Refiner.BoxReachFraction"/> of the long side,
    /// or by <see cref="Constants.Refiner.BoxReachOwnFraction"/> of the box's own size when that is more.
    /// </summary>
    /// <param name="objects">The objects, in paint-id order (id = position + 1).</param>
    /// <param name="width">Label map width.</param>
    /// <param name="height">Label map height.</param>
    private static PixelRect?[] Reach(IReadOnlyList<SegmentedObject> objects, int width, int height)
    {
        var minMargin = Constants.Refiner.BoxReachFraction * Math.Max(width, height);
        var reach = new PixelRect?[objects.Count + 1];
        for (var i = 0; i < objects.Count; i++)
        {
            var item = objects[i].Item;
            if (item.Category.IsSurface())
                continue;
            var box = item.Box;
            var marginX = (int)Math.Ceiling(Math.Max(minMargin, Constants.Refiner.BoxReachOwnFraction * box.Width * width));
            var marginY = (int)Math.Ceiling(Math.Max(minMargin, Constants.Refiner.BoxReachOwnFraction * box.Height * height));
            reach[i + 1] = new PixelRect(
                Math.Max(0, (int)Math.Floor(box.X * width) - marginX),
                Math.Max(0, (int)Math.Floor(box.Y * height) - marginY),
                Math.Min(width, (int)Math.Ceiling((box.X + box.Width) * width) + marginX),
                Math.Min(height, (int)Math.Ceiling((box.Y + box.Height) * height) + marginY));
        }
        return reach;
    }

    /// <summary>Counts the mask pixels that belong to the object.</summary>
    /// <param name="mask">The mask.</param>
    private static int CountCovered(ObjectMask mask)
    {
        return mask.Coverage.Count(c => c >= Constants.Imaging.CoverageThreshold);
    }

    /// <summary>Writes <paramref name="paintId"/> into every label pixel the mask covers, scaling the mask to fit.</summary>
    /// <param name="labels">The label map being built.</param>
    /// <param name="width">Label map width.</param>
    /// <param name="height">Label map height.</param>
    /// <param name="mask">The mask to paint.</param>
    /// <param name="paintId">The value to write.</param>
    private static void Paint(byte[] labels, int width, int height, ObjectMask mask, byte paintId)
    {
        // Only visit the part of the label map under the mask's own bounding box.
        var (minX, minY, maxX, maxY) = (mask.Width, mask.Height, -1, -1);
        for (var y = 0; y < mask.Height; y++)
        {
            for (var x = 0; x < mask.Width; x++)
            {
                if (mask.Coverage[y * mask.Width + x] < Constants.Imaging.CoverageThreshold)
                    continue;
                (minX, minY) = (Math.Min(minX, x), Math.Min(minY, y));
                (maxX, maxY) = (Math.Max(maxX, x), Math.Max(maxY, y));
            }
        }
        if (maxX < 0)
            return;

        var (sx, sy) = (mask.Width / (double)width, mask.Height / (double)height);
        var (fromX, toX) = ((int)(minX / sx), Math.Min(width - 1, (int)Math.Ceiling((maxX + 1) / sx)));
        var (fromY, toY) = ((int)(minY / sy), Math.Min(height - 1, (int)Math.Ceiling((maxY + 1) / sy)));
        for (var y = fromY; y <= toY; y++)
        {
            var my = Math.Min(mask.Height - 1, (int)((y + 0.5) * sy));
            for (var x = fromX; x <= toX; x++)
            {
                var mx = Math.Min(mask.Width - 1, (int)((x + 0.5) * sx));
                if (mask.Coverage[my * mask.Width + mx] >= Constants.Imaging.CoverageThreshold)
                    labels[y * width + x] = paintId;
            }
        }
    }

    /// <summary>Measures each painted object's visible pixels: count, bounds and color histogram.</summary>
    /// <param name="labels">The painted label map.</param>
    /// <param name="width">Label map width.</param>
    /// <param name="height">Label map height.</param>
    /// <param name="count">How many objects were painted.</param>
    /// <param name="pixels">The analysis image, 4 bytes per pixel, for the colors.</param>
    /// <returns>Statistics by paint id (index 0 is unused).</returns>
    private static ObjectStats[] Measure(byte[] labels, int width, int height, int count, ReadOnlySpan<byte> pixels)
    {
        var stats = new ObjectStats[count + 1];
        for (var i = 0; i < stats.Length; i++)
            stats[i] = new ObjectStats();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = y * width + x;
                if (labels[p] != 0)
                    stats[labels[p]].Add(x, y, pixels[p * 4], pixels[p * 4 + 1], pixels[p * 4 + 2]);
            }
        }
        return stats;
    }

    /// <summary>Decodes the analysis image to sRGB RGBA, one 4-byte pixel per label-map pixel.</summary>
    /// <param name="image">The analysis image.</param>
    private static SKBitmap DecodeRgba(AnalysisImage image)
    {
        using var codec = SKCodec.Create(new MemoryStream(image.Jpeg));
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul, SKColorSpace.CreateSrgb());
        return SKBitmap.Decode(codec, info) ?? throw new InvalidOperationException("The analysis image can't be decoded.");
    }

    /// <inheritdoc/>
    public Result<byte[]> PaintOutline(byte[] labelMapPng, OutlineStroke stroke)
    {
        var (labels, width, height) = DecodeGrayPng(labelMapPng);
        var changed = OutlineBrush.Apply(labels, width, height, stroke);
        return changed.Succeeded ? EncodeGrayPng(labels, width, height) : Result.Failure<byte[]>(changed.Error);
    }

    /// <summary>Reads a label map PNG back into one byte per pixel.</summary>
    /// <param name="png">The label map file.</param>
    /// <returns>The labels and the map's size.</returns>
    /// <exception cref="InvalidOperationException">The file can't be read.</exception>
    private static (byte[] Labels, int Width, int Height) DecodeGrayPng(byte[] png)
    {
        using var codec = SKCodec.Create(new MemoryStream(png)) ?? throw new InvalidOperationException("The label map can't be decoded.");
        var (width, height) = (codec.Info.Width, codec.Info.Height);
        using var bitmap = SKBitmap.Decode(codec, new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque))
            ?? throw new InvalidOperationException("The label map can't be decoded.");
        return (bitmap.GetPixelSpan().ToArray(), width, height);
    }

    /// <summary>Encodes the label map as an 8-bit grayscale PNG, so pixel values survive exactly.</summary>
    /// <param name="labels">One byte per pixel.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    private static byte[] EncodeGrayPng(byte[] labels, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));
        labels.CopyTo(bitmap.GetPixelSpan());
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, Constants.Imaging.PngQuality);
        return png.ToArray();
    }

    /// <summary>Running totals for one object's visible pixels.</summary>
    private sealed class ObjectStats
    {
        private readonly int[] _red = new int[Constants.Imaging.ByteValues];
        private readonly int[] _green = new int[Constants.Imaging.ByteValues];
        private readonly int[] _blue = new int[Constants.Imaging.ByteValues];

        /// <summary>How many pixels the object has.</summary>
        public int Pixels { get; private set; }
        /// <summary>Leftmost pixel column.</summary>
        public int MinX { get; private set; } = int.MaxValue;
        /// <summary>Topmost pixel row.</summary>
        public int MinY { get; private set; } = int.MaxValue;
        /// <summary>Rightmost pixel column.</summary>
        public int MaxX { get; private set; } = -1;
        /// <summary>Bottom pixel row.</summary>
        public int MaxY { get; private set; } = -1;

        /// <summary>Counts one pixel.</summary>
        /// <param name="x">Its column.</param>
        /// <param name="y">Its row.</param>
        /// <param name="r">Its red value.</param>
        /// <param name="g">Its green value.</param>
        /// <param name="b">Its blue value.</param>
        public void Add(int x, int y, byte r, byte g, byte b)
        {
            Pixels++;
            (MinX, MinY) = (Math.Min(MinX, x), Math.Min(MinY, y));
            (MaxX, MaxY) = (Math.Max(MaxX, x), Math.Max(MaxY, y));
            _red[r]++;
            _green[g]++;
            _blue[b]++;
        }

        /// <summary>The median of each channel: the object's color, unswayed by highlights and shadows.</summary>
        public HexColor MedianColor()
        {
            return HexColor.Parse($"#{Median(_red):X2}{Median(_green):X2}{Median(_blue):X2}");
        }

        /// <summary>The middle value of a histogram.</summary>
        /// <param name="histogram">Counts per value 0..255.</param>
        private int Median(int[] histogram)
        {
            var half = Pixels / 2;
            for (int value = 0, seen = 0; value < Constants.Imaging.ByteValues; value++)
            {
                seen += histogram[value];
                if (seen > half)
                    return value;
            }
            return 255;
        }
    }
}

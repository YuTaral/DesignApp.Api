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
        {
            return new AnalysisImage(jpeg, source.Width, source.Height);
        }

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
        {
            objects = [.. objects.Take(Domain.Constants.Rooms.MaxObjectIndex)];
        }

        var (width, height) = (image.Width, image.Height);
        var labels = new byte[width * height];
        var minPixels = Application.Constants.Analysis.MinAreaFraction * width * height;
        var painted = new int[objects.Count + 1];
        var surfaces = objects.Select(o => o.Item.Category.IsSurface()).ToArray();
        var reach = Reach(objects, width, height);
        var renumber = new byte[objects.Count + 1];
        var detected = new List<DetectedObject>();
        // Paint the largest outlines first, so smaller objects in front of them stay on top.
        // Paint ids are list positions + 1; objects are renumbered once we know which survive.
        var paintOrder = objects
            .Select((o, i) => (Object: o, PaintId: (byte)(i + 1), Area: CountCovered(o.Mask)))
            .Where(p => p.Area > 0)
            .OrderByDescending(p => p.Area / (double)(p.Object.Mask.Width * p.Object.Mask.Height))
            .ToList();

        foreach (var (o, paintId, _) in paintOrder)
        {
            Paint(labels, width, height, o.Mask, paintId);
        }

        // Objects too small to keep are cleared before the cleanup, so they can't grow.
        foreach (var label in labels)
        {
            painted[label]++;
        }

        for (var p = 0; p < labels.Length; p++)
        {
            if (painted[labels[p]] < minPixels)
            {
                labels[p] = 0;
            }
        }

        using var photo = DecodeRgba(image);
        var pixels = photo.GetPixelSpan();

        // Without walls, floors or ceilings the background stays unassigned instead of going to the objects.
        LabelMapRefiner.Refine(labels, width, height, pixels, label => label <= surfaces.Length && surfaces[label - 1], reach,
            fillGaps: surfaces.Any(s => s));

        var stats = Measure(labels, width, height, objects.Count, pixels);

        // Keep Claude's order (most important first) for the final indexes.
        for (var i = 0; i < objects.Count; i++)
        {
            var s = stats[i + 1];

            if (s.Pixels < minPixels)
            {
                continue;
            }

            var index = detected.Count + 1;
            var item = objects[i].Item;
            var box = new BoundingBox(
                s.MinX / (double)width, s.MinY / (double)height,
                (s.MaxX - s.MinX + 1) / (double)width, (s.MaxY - s.MinY + 1) / (double)height);

            renumber[i + 1] = (byte)index;
            detected.Add(new DetectedObject(index, item.Label, item.Category, box, s.MedianColor()));
        }

        for (var p = 0; p < labels.Length; p++)
        {
            labels[p] = renumber[labels[p]];
        }

        return new LabelMap(MaskPixels.GrayPng(labels, width, height), detected);
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
            {
                continue;
            }

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
        if (MaskPixels.CoveredRect(mask) is not { } covered)
        {
            return;
        }

        var (sx, sy) = (mask.Width / (double)width, mask.Height / (double)height);
        var (fromX, toX) = ((int)(covered.X0 / sx), Math.Min(width - 1, (int)Math.Ceiling(covered.X1 / sx)));
        var (fromY, toY) = ((int)(covered.Y0 / sy), Math.Min(height - 1, (int)Math.Ceiling(covered.Y1 / sy)));

        for (var y = fromY; y <= toY; y++)
        {
            var my = Math.Min(mask.Height - 1, (int)((y + 0.5) * sy));

            for (var x = fromX; x <= toX; x++)
            {
                var mx = Math.Min(mask.Width - 1, (int)((x + 0.5) * sx));

                if (mask.Coverage[my * mask.Width + mx] >= Constants.Imaging.CoverageThreshold)
                {
                    labels[y * width + x] = paintId;
                }
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
        {
            stats[i] = new ObjectStats();
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = y * width + x;

                if (labels[p] != 0)
                {
                    stats[labels[p]].Add(x, y, pixels[p * 4], pixels[p * 4 + 1], pixels[p * 4 + 2]);
                }
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

        return changed.Succeeded ? MaskPixels.GrayPng(labels, width, height) : Result.Failure<byte[]>(changed.Error);
    }

    /// <inheritdoc/>
    public Result<MarkedObject> MarkObject(byte[] labelMapPng, Stream photo, NewObjectMark mark)
    {
        var (labels, width, height) = DecodeGrayPng(labelMapPng);
        var marked = ObjectMarker.Apply(labels, width, height, mark);

        if (!marked.Succeeded)
        {
            return Result.Failure<MarkedObject>(marked.Error);
        }

        using var pixels = PhotoAtMapSize(photo, width, height);
        var colors = pixels.GetPixelSpan();
        var stats = new ObjectStats();

        for (var p = 0; p < labels.Length; p++)
        {
            if (labels[p] == mark.Index)
            {
                stats.Add(p % width, p / width, colors[p * 4], colors[p * 4 + 1], colors[p * 4 + 2]);
            }
        }

        var box = new BoundingBox(
            stats.MinX / (double)width, stats.MinY / (double)height,
            (stats.MaxX - stats.MinX + 1) / (double)width, (stats.MaxY - stats.MinY + 1) / (double)height);

        return new MarkedObject(MaskPixels.GrayPng(labels, width, height), box, stats.MedianColor());
    }

    /// <inheritdoc/>
    public Result<LabelMap> AddParts(byte[] labelMapPng, Stream photo, DetectedObject whole, int firstIndex, IReadOnlyList<SegmentedObject> parts)
    {
        var (labels, width, height) = DecodeGrayPng(labelMapPng);
        var parent = (byte)whole.Index;
        var inWhole = labels.Count(l => l == parent);
        var minimum = Math.Max(1, (int)(inWhole * Constants.Imaging.MinPartShare));
        // Each part takes from the object as it was, so a smaller part can sit on top of a larger one (a seat on a frame).
        var original = (byte[])labels.Clone();
        var stats = parts.Select(_ => new ObjectStats()).ToArray();
        var renumber = new byte[Constants.Imaging.ByteValues];
        var found = new List<DetectedObject>();
        // Larger parts first, so a smaller one (a seat cushion on a seat) ends up on top of it.
        var ordered = parts.Select((part, i) => (Part: part, Index: (byte)(firstIndex + i), Area: part.Mask.Coverage.Count(c => c >= Constants.Imaging.CoverageThreshold)))
            .OrderByDescending(p => p.Area).ToList();

        foreach (var (part, index, _) in ordered)
        {
            var mask = part.Mask;

            for (var p = 0; p < labels.Length; p++)
            {
                if (original[p] != parent)
                {
                    continue;
                }

                // The masks are at the analysis size, which is the label map's; scale anyway in case they differ.
                var (mx, my) = (p % width * mask.Width / width, p / width * mask.Height / height);

                if (mask.Coverage[my * mask.Width + mx] >= Constants.Imaging.CoverageThreshold)
                {
                    labels[p] = index;
                }
            }
        }

        using var pixels = PhotoAtMapSize(photo, width, height);
        var colors = pixels.GetPixelSpan();

        for (var p = 0; p < labels.Length; p++)
        {
            var slot = labels[p] - firstIndex;

            if (slot >= 0 && slot < stats.Length)
            {
                stats[slot].Add(p % width, p / width, colors[p * 4], colors[p * 4 + 1], colors[p * 4 + 2]);
            }
        }

        // Parts too small to color are given back to the object; the others are numbered on from firstIndex.
        for (var slot = 0; slot < stats.Length; slot++)
        {
            var s = stats[slot];

            if (s.Pixels < minimum)
            {
                renumber[firstIndex + slot] = parent;
                continue;
            }

            var index = firstIndex + found.Count;
            var box = new BoundingBox(s.MinX / (double)width, s.MinY / (double)height, (s.MaxX - s.MinX + 1) / (double)width, (s.MaxY - s.MinY + 1) / (double)height);

            renumber[firstIndex + slot] = (byte)index;
            found.Add(new DetectedObject(index, parts[slot].Item.Label, whole.Category, box, s.MedianColor(), ParentIndex: whole.Index));
        }

        if (found.Count == 0)
        {
            return Result.Failure<LabelMap>(Constants.Messages.NoPartsInside(whole.Label));
        }

        for (var p = 0; p < labels.Length; p++)
        {
            if (labels[p] >= firstIndex && labels[p] < firstIndex + stats.Length)
            {
                labels[p] = renumber[labels[p]];
            }
        }

        return new LabelMap(MaskPixels.GrayPng(labels, width, height), found);
    }

    /// <inheritdoc/>
    public Result<byte[]> MergePart(byte[] labelMapPng, int partIndex, int parentIndex, string partLabel)
    {
        var (labels, width, height) = DecodeGrayPng(labelMapPng);
        var merged = 0;

        for (var p = 0; p < labels.Length; p++)
        {
            if (labels[p] != partIndex)
            {
                continue;
            }

            labels[p] = (byte)parentIndex;
            merged++;
        }

        return merged == 0 ? Result.Failure<byte[]>(Constants.Messages.NothingToMerge(partLabel)) : MaskPixels.GrayPng(labels, width, height);
    }

    /// <inheritdoc/>
    public byte[]? RemovalMask(byte[] labelMapPng, IReadOnlyCollection<int> indexes, int photoWidth, int photoHeight)
    {
        var (labels, width, height) = DecodeGrayPng(labelMapPng);
        var gone = new bool[Constants.Imaging.ByteValues];

        foreach (var index in indexes)
        {
            gone[index] = true;
        }

        if (!labels.Any(l => gone[l]))
        {
            return null;
        }

        // The label map can be smaller than the photo (the analysis size): each photo pixel takes the label under it.
        var mask = new byte[photoWidth * photoHeight];

        for (var y = 0; y < photoHeight; y++)
        {
            var row = Math.Min(height - 1, y * height / photoHeight) * width;

            for (var x = 0; x < photoWidth; x++)
            {
                if (gone[labels[row + Math.Min(width - 1, x * width / photoWidth)]])
                {
                    mask[y * photoWidth + x] = Constants.Imaging.FullCoverage;
                }
            }
        }

        return MaskPixels.GrayPng(mask, photoWidth, photoHeight);
    }

    /// <inheritdoc/>
    public RemovedObject AfterRemoval(byte[] labelMapPng, IReadOnlyCollection<int> indexes, byte[] newPhoto, int photoWidth, int photoHeight)
    {
        using var decoded = SKBitmap.Decode(newPhoto) ?? throw new InvalidOperationException("The new photo can't be decoded.");
        // The stored photo keeps its size, whatever size the eraser answered with.
        var photoInfo = new SKImageInfo(photoWidth, photoHeight, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        using var photo = decoded.Width == photoWidth && decoded.Height == photoHeight
            ? decoded.Copy(SKColorType.Rgba8888)
            : decoded.Resize(photoInfo, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        using var image = SKImage.FromBitmap(photo);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, Application.Constants.Photos.JpegQuality);

        var (labels, width, height) = DecodeGrayPng(labelMapPng);
        var mapInfo = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul, SKColorSpace.CreateSrgb());
        using var atMapSize = photo.Resize(mapInfo, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new InvalidOperationException("The new photo can't be scaled.");

        RemovalRefill.Apply(labels, width, height, atMapSize.GetPixelSpan(), indexes);

        return new RemovedObject(jpeg.ToArray(), MaskPixels.GrayPng(labels, width, height));
    }

    /// <summary>Decodes the stored photo to sRGB RGBA at the label map's size, one 4-byte pixel per label.</summary>
    /// <param name="photo">The stored room photo (JPEG).</param>
    /// <param name="width">Label map width.</param>
    /// <param name="height">Label map height.</param>
    /// <exception cref="InvalidOperationException">The photo can't be decoded.</exception>
    private static SKBitmap PhotoAtMapSize(Stream photo, int width, int height)
    {
        using var source = SKBitmap.Decode(photo) ?? throw new InvalidOperationException("The stored photo can't be decoded.");
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul, SKColorSpace.CreateSrgb());

        return source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new InvalidOperationException("The stored photo can't be scaled.");
    }

    /// <summary>Reads a label map PNG back into one byte per pixel.</summary>
    /// <param name="png">The label map file.</param>
    /// <returns>The labels and the map's size.</returns>
    /// <exception cref="InvalidOperationException">The file can't be read.</exception>
    internal static (byte[] Labels, int Width, int Height) DecodeGrayPng(byte[] png)
    {
        using var codec = SKCodec.Create(new MemoryStream(png)) ?? throw new InvalidOperationException("The label map can't be decoded.");
        var (width, height) = (codec.Info.Width, codec.Info.Height);
        using var bitmap = SKBitmap.Decode(codec, new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque))
            ?? throw new InvalidOperationException("The label map can't be decoded.");

        return (bitmap.GetPixelSpan().ToArray(), width, height);
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
                {
                    return value;
                }
            }

            return 255;
        }
    }
}

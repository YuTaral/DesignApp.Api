namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// Cleans up a freshly painted label map. Segmentation masks have speckles, holes and gaps
/// (plain white ceilings and walls are the worst), and a pixel no mask claimed can't be recolored.
/// Three passes: drop stray fragments, fill holes enclosed by one object, then grow objects into
/// the remaining gaps through pixels of similar color, stopping at the photo's real edges. Surfaces
/// (walls, floors, ceilings) may grow a short way to close the ragged gaps along their edges, but
/// not so far that a white ceiling takes over an unlisted white wall; other objects only move their
/// edges a few pixels onto the photo's edges, so a white vase can't spread over a white wall.
/// </summary>
internal static class LabelMapRefiner
{
    /// <summary>A piece of an object smaller than this fraction of the photo, apart from its largest piece, is a speckle.</summary>
    internal const double FragmentFraction = 0.0003;

    /// <summary>An enclosed gap is filled only up to this fraction of the photo (larger ones may be an unlisted object).</summary>
    internal const double MaxHoleFraction = 0.02;

    /// <summary>
    /// Growing crosses from a pixel to its neighbor only when their colors differ by at most this
    /// much (RGB distance, 0-441). JPEG noise and soft shading stay below it; object edges don't.
    /// </summary>
    internal const double MaxStepDistance = 24;

    /// <summary>How far (in pixels) an object that isn't a surface may grow past its mask.</summary>
    internal const int MaxObjectGrowth = 6;

    /// <summary>How far a surface may grow past its mask, as a fraction of the photo's long side.</summary>
    internal const double MaxSurfaceGrowthFraction = 0.05;

    /// <summary>Refines the label map in place.</summary>
    /// <param name="labels">One object id per pixel (0 = none); changed in place.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="rgba">The photo at the same size, 4 bytes per pixel.</param>
    /// <param name="isSurface">Whether a label is a wall, floor or ceiling, which may grow further.</param>
    public static void Refine(byte[] labels, int width, int height, ReadOnlySpan<byte> rgba, Func<byte, bool> isSurface)
    {
        RemoveFragments(labels, width, height);
        FillEnclosedHoles(labels, width, height);
        GrowIntoGaps(labels, width, height, rgba, isSurface);
    }

    /// <summary>Clears every piece of an object that is small and isn't the object's largest piece.</summary>
    /// <param name="labels">The label map.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    internal static void RemoveFragments(byte[] labels, int width, int height)
    {
        var minPixels = Math.Max(20, (int)(FragmentFraction * width * height));
        var pieces = Components(labels, width, height, keep: label => label != 0);
        var largest = new Dictionary<byte, int>();
        foreach (var piece in pieces)
            largest[piece.Label] = Math.Max(largest.GetValueOrDefault(piece.Label), piece.Pixels.Count);

        foreach (var piece in pieces)
        {
            if (piece.Pixels.Count < minPixels && piece.Pixels.Count < largest[piece.Label])
                foreach (var p in piece.Pixels)
                    labels[p] = 0;
        }
    }

    /// <summary>Gives each small gap that touches only one object (and not the photo's border) to that object.</summary>
    /// <param name="labels">The label map.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    internal static void FillEnclosedHoles(byte[] labels, int width, int height)
    {
        var maxPixels = (int)(MaxHoleFraction * width * height);
        foreach (var gap in Components(labels, width, height, keep: label => label == 0))
        {
            if (gap.Pixels.Count > maxPixels || gap.TouchesBorder || gap.Neighbors.Count != 1)
                continue;
            var owner = gap.Neighbors.First();
            foreach (var p in gap.Pixels)
                labels[p] = owner;
        }
    }

    /// <summary>
    /// Seeded region growing: unassigned pixels are claimed by neighboring objects, the most similar
    /// color step first, so gaps split along the photo's edges. Steps larger than
    /// <see cref="MaxStepDistance"/> are never taken, so a gap behind a real edge stays unassigned,
    /// objects that aren't surfaces stop after <see cref="MaxObjectGrowth"/> pixels, and surfaces after
    /// <see cref="MaxSurfaceGrowthFraction"/> of the long side.
    /// </summary>
    /// <param name="labels">The label map.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="rgba">The photo, 4 bytes per pixel.</param>
    /// <param name="isSurface">Whether a label is a surface, with the longer growth limit.</param>
    internal static void GrowIntoGaps(byte[] labels, int width, int height, ReadOnlySpan<byte> rgba, Func<byte, bool> isSurface)
    {
        var queue = new PriorityQueue<(int Pixel, byte Label, int Steps), float>();
        var surfaceGrowth = Math.Max(MaxObjectGrowth, (int)(MaxSurfaceGrowthFraction * Math.Max(width, height)));
        var limit = new int[256];
        for (var label = 1; label < 256; label++)
            limit[label] = isSurface((byte)label) ? surfaceGrowth : MaxObjectGrowth;

        void Offer(int from, int to, int steps, ReadOnlySpan<byte> photo)
        {
            if (labels[to] != 0 || steps > limit[labels[from]])
                return;
            var step = Distance(photo, from, to);
            if (step <= MaxStepDistance)
                queue.Enqueue((to, labels[from], steps), step);
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = y * width + x;
                if (labels[p] == 0)
                    continue;
                if (x > 0) Offer(p, p - 1, 1, rgba);
                if (x < width - 1) Offer(p, p + 1, 1, rgba);
                if (y > 0) Offer(p, p - width, 1, rgba);
                if (y < height - 1) Offer(p, p + width, 1, rgba);
            }
        }

        while (queue.TryDequeue(out var next, out _))
        {
            var (p, label, steps) = next;
            if (labels[p] != 0)
                continue;
            labels[p] = label;
            var (x, y) = (p % width, p / width);
            if (x > 0) Offer(p, p - 1, steps + 1, rgba);
            if (x < width - 1) Offer(p, p + 1, steps + 1, rgba);
            if (y > 0) Offer(p, p - width, steps + 1, rgba);
            if (y < height - 1) Offer(p, p + width, steps + 1, rgba);
        }
    }

    /// <summary>The RGB distance between two pixels.</summary>
    /// <param name="rgba">The photo, 4 bytes per pixel.</param>
    /// <param name="a">One pixel's index.</param>
    /// <param name="b">The other pixel's index.</param>
    private static float Distance(ReadOnlySpan<byte> rgba, int a, int b)
    {
        int dr = rgba[a * 4] - rgba[b * 4], dg = rgba[a * 4 + 1] - rgba[b * 4 + 1], db = rgba[a * 4 + 2] - rgba[b * 4 + 2];
        return MathF.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>One 4-connected region of equal labels.</summary>
    /// <param name="Label">The region's label.</param>
    /// <param name="Pixels">Its pixel indexes.</param>
    /// <param name="Neighbors">The different labels bordering it (0 excluded).</param>
    /// <param name="TouchesBorder">Whether it reaches the photo's edge.</param>
    private sealed record Component(byte Label, List<int> Pixels, HashSet<byte> Neighbors, bool TouchesBorder);

    /// <summary>Finds the 4-connected regions of the pixels whose label passes <paramref name="keep"/>.</summary>
    /// <param name="labels">The label map.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="keep">Which labels to group.</param>
    private static List<Component> Components(byte[] labels, int width, int height, Func<byte, bool> keep)
    {
        var seen = new bool[labels.Length];
        var result = new List<Component>();
        var stack = new Stack<int>();

        for (var start = 0; start < labels.Length; start++)
        {
            if (seen[start] || !keep(labels[start]))
                continue;
            var label = labels[start];
            var component = new Component(label, [], [], false);
            var touchesBorder = false;
            seen[start] = true;
            stack.Push(start);

            while (stack.TryPop(out var p))
            {
                component.Pixels.Add(p);
                var (x, y) = (p % width, p / width);
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    touchesBorder = true;

                void Visit(int q)
                {
                    if (labels[q] == label)
                    {
                        if (!seen[q])
                        {
                            seen[q] = true;
                            stack.Push(q);
                        }
                    }
                    else if (labels[q] != 0)
                    {
                        component.Neighbors.Add(labels[q]);
                    }
                }

                if (x > 0) Visit(p - 1);
                if (x < width - 1) Visit(p + 1);
                if (y > 0) Visit(p - width);
                if (y < height - 1) Visit(p + width);
            }
            result.Add(component with { TouchesBorder = touchesBorder });
        }
        return result;
    }
}

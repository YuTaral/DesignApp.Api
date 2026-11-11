namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// Cleans up a freshly painted label map. Segmentation masks have speckles, holes and gaps
/// (plain white ceilings and walls are the worst), and a pixel no mask claimed can't be recolored.
/// Three passes: drop stray fragments, fill holes enclosed by one object, then grow objects into
/// the remaining gaps through pixels of similar color, stopping at the photo's real edges. Surfaces
/// (walls, floors, ceilings) may grow a short way to close the ragged gaps along their edges, but
/// not so far that a white ceiling takes over an unlisted white wall; other objects only move their
/// edges a few pixels onto the photo's edges, so a white vase can't spread over a white wall. A last
/// pass fills what is still empty; there, an object that isn't a surface stays inside the area it is given
/// (Claude's box for it), so a small object can't take over a gap far from it.
/// </summary>
internal static class LabelMapRefiner
{
    /// <summary>Refines the label map in place.</summary>
    /// <param name="labels">One object id per pixel (0 = none); changed in place.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="rgba">The photo at the same size, 4 bytes per pixel.</param>
    /// <param name="isSurface">Whether a label is a wall, floor or ceiling, which may grow further.</param>
    /// <param name="reach">
    /// Per label, the area it may take in the last pass (<c>null</c> for anywhere); see
    /// <see cref="FillRemainingGaps"/>. Omitted, every label may reach anywhere.
    /// </param>
    public static void Refine(
        byte[] labels, int width, int height, ReadOnlySpan<byte> rgba, Func<byte, bool> isSurface, PixelRect?[]? reach = null)
    {
        RemoveFragments(labels, width, height);
        FillEnclosedHoles(labels, width, height);
        GrowIntoGaps(labels, width, height, rgba, isSurface);
        FillRemainingGaps(labels, width, height, rgba, reach);
    }

    /// <summary>Clears every piece of an object that is small and isn't the object's largest piece.</summary>
    /// <param name="labels">The label map.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    internal static void RemoveFragments(byte[] labels, int width, int height)
    {
        var minPixels = Math.Max(Constants.Refiner.MinFragmentPixels, (int)(Constants.Refiner.FragmentFraction * width * height));
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
        var maxPixels = (int)(Constants.Refiner.MaxHoleFraction * width * height);
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
    /// <see cref="Constants.Refiner.MaxStepDistance"/> are never taken, so a gap behind a real edge stays unassigned,
    /// objects that aren't surfaces stop after <see cref="Constants.Refiner.MaxObjectGrowth"/> pixels, and surfaces after
    /// <see cref="Constants.Refiner.MaxSurfaceGrowthFraction"/> of the long side.
    /// </summary>
    /// <param name="labels">The label map.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="rgba">The photo, 4 bytes per pixel.</param>
    /// <param name="isSurface">Whether a label is a surface, with the longer growth limit.</param>
    /// <param name="unlimited">True: labels <paramref name="isSurface"/> accepts have no distance limit; only the color steps stop them.</param>
    /// <param name="reach">Per label, the only area it may grow into (<c>null</c>, or no array: anywhere).</param>
    internal static void GrowIntoGaps(
        byte[] labels, int width, int height, ReadOnlySpan<byte> rgba, Func<byte, bool> isSurface, bool unlimited = false,
        PixelRect?[]? reach = null)
    {
        var queue = new PriorityQueue<(int Pixel, byte Label, int Steps), float>();
        var surfaceGrowth = Math.Max(Constants.Refiner.MaxObjectGrowth, (int)(Constants.Refiner.MaxSurfaceGrowthFraction * Math.Max(width, height)));
        var limit = new int[Constants.Imaging.ByteValues];
        for (var label = 1; label < Constants.Imaging.ByteValues; label++)
            limit[label] = isSurface((byte)label) ? (unlimited ? int.MaxValue : surfaceGrowth) : Constants.Refiner.MaxObjectGrowth;

        void Offer(int from, int to, int steps, ReadOnlySpan<byte> photo)
        {
            if (labels[to] != 0 || steps > limit[labels[from]] || !CanReach(reach, labels[from], to, width))
                return;
            var step = Distance(photo, from, to);
            if (step <= Constants.Refiner.MaxStepDistance)
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

    /// <summary>
    /// Leaves as few pixels untappable as possible. First every object keeps growing, without a distance
    /// limit, as long as each step stays within <see cref="Constants.Refiner.MaxStepDistance"/> of color, so it
    /// stops at real edges; then each gap still left goes to the neighbor whose mean color is closest, when
    /// that is within <see cref="Constants.Refiner.MaxGapColorDistance"/>. A gap unlike all its neighbors
    /// (probably an object nobody listed) stays empty. A label with a <paramref name="reach"/> takes no pixel
    /// outside it in either step, so a small object next to a surface of its color can't spread over it.
    /// </summary>
    /// <param name="labels">The label map, changed in place.</param>
    /// <param name="width">Its width.</param>
    /// <param name="height">Its height.</param>
    /// <param name="rgba">The photo at the same size.</param>
    /// <param name="reach">Per label, the only area it may take (<c>null</c>, or no array: anywhere).</param>
    internal static void FillRemainingGaps(byte[] labels, int width, int height, ReadOnlySpan<byte> rgba, PixelRect?[]? reach = null)
    {
        GrowIntoGaps(labels, width, height, rgba, _ => true, unlimited: true, reach: reach);

        var sums = new double[Constants.Imaging.ByteValues, 4];
        for (var p = 0; p < labels.Length; p++)
        {
            var label = labels[p];
            if (label == 0) continue;
            sums[label, 0] += rgba[p * 4]; sums[label, 1] += rgba[p * 4 + 1]; sums[label, 2] += rgba[p * 4 + 2]; sums[label, 3]++;
        }
        foreach (var gap in Components(labels, width, height, keep: label => label == 0))
        {
            if (gap.Neighbors.Count == 0) continue;
            double r = 0, g = 0, b = 0;
            foreach (var p in gap.Pixels) { r += rgba[p * 4]; g += rgba[p * 4 + 1]; b += rgba[p * 4 + 2]; }
            (r, g, b) = (r / gap.Pixels.Count, g / gap.Pixels.Count, b / gap.Pixels.Count);
            byte owner = 0; var closest = double.MaxValue;
            foreach (var n in gap.Neighbors)
            {
                if (!gap.Pixels.TrueForAll(p => CanReach(reach, n, p, width)))
                    continue;
                var count = sums[n, 3];
                var d = Math.Sqrt(Math.Pow(sums[n, 0] / count - r, 2) + Math.Pow(sums[n, 1] / count - g, 2) + Math.Pow(sums[n, 2] / count - b, 2));
                if (d < closest) (owner, closest) = (n, d);
            }
            if (closest > Constants.Refiner.MaxGapColorDistance) continue;
            foreach (var p in gap.Pixels) labels[p] = owner;
        }
    }

    /// <summary>Whether a label may take a pixel: always, unless its reach leaves the pixel out.</summary>
    /// <param name="reach">Per label, the area it may take; <c>null</c> for anywhere.</param>
    /// <param name="label">The label.</param>
    /// <param name="pixel">The pixel's index.</param>
    /// <param name="width">The label map's width.</param>
    private static bool CanReach(PixelRect?[]? reach, byte label, int pixel, int width)
    {
        if (reach is null || label >= reach.Length || reach[label] is not { } area)
            return true;
        return area.Contains(pixel % width, pixel / width);
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

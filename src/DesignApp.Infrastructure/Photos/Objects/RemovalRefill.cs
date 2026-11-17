namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// The label map after objects were removed from the photo: their pixels go to the objects around them, so the
/// revealed floor or wall belongs to the floor or wall (and takes its recolor). The rest of the map is unchanged.
/// </summary>
internal static class RemovalRefill
{
    /// <summary>Gives the removed objects' pixels to their neighbours, in place.</summary>
    /// <param name="labels">The label map, one object index per pixel.</param>
    /// <param name="width">Its width.</param>
    /// <param name="height">Its height.</param>
    /// <param name="rgba">The new photo at the map's size, 4 bytes per pixel.</param>
    /// <param name="indexes">The removed objects.</param>
    /// <returns>How many pixels they had.</returns>
    public static int Apply(byte[] labels, int width, int height, ReadOnlySpan<byte> rgba, IReadOnlyCollection<int> indexes)
    {
        var gone = new bool[Constants.Imaging.ByteValues];
        var count = 0;
        var removed = new bool[labels.Length];
        var empty = new bool[labels.Length];
        var queue = new Queue<int>();

        foreach (var index in indexes)
        {
            gone[index] = true;
        }

        for (var p = 0; p < labels.Length; p++)
        {
            empty[p] = labels[p] == 0;

            if (!gone[labels[p]])
            {
                continue;
            }

            (removed[p], labels[p]) = (true, 0);
            count++;
        }

        // First by color, the way the analysis fills gaps, so a floor and a wall meet at the real edge.
        LabelMapRefiner.FillRemainingGaps(labels, width, height, rgba);

        // Whatever is still empty of the removed area goes to the nearest object, so all of it can be tapped.
        for (var p = 0; p < labels.Length; p++)
        {
            if (labels[p] != 0)
            {
                queue.Enqueue(p);
            }
        }

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            var (x, y) = (p % width, p / width);

            if (x > 0)
            {
                Take(p - 1);
            }

            if (x < width - 1)
            {
                Take(p + 1);
            }

            if (y > 0)
            {
                Take(p - width);
            }

            if (y < height - 1)
            {
                Take(p + width);
            }

            void Take(int q)
            {
                if (labels[q] != 0 || !removed[q])
                {
                    return;
                }

                labels[q] = labels[p];
                queue.Enqueue(q);
            }
        }

        // Gaps that were there before stay as they were.
        for (var p = 0; p < labels.Length; p++)
        {
            if (empty[p])
            {
                labels[p] = 0;
            }
        }

        return count;
    }
}

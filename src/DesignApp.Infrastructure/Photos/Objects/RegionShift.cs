using CC = DesignApp.Infrastructure.Constants.ColorCorrection;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// One region's color correction, in two steps. <see cref="Measure"/> finds its typical color in the photo and in the
/// 3D view, on its inside (away from its edge, where the photo and the 3D view may not line up): either the medians of
/// its main part (the pixels near its most common color in the photo) or of the whole, whichever the photo shows nearer
/// the 3D view's color, so an object of several parts (a white cooker with a black oven window) is matched by the part
/// the plan describes, and decorations on it (books on a table) aren't. <see cref="Apply"/> spreads the move between them over its pixels: it fades
/// softly across the region's edge, and a pixel far from the region's typical color (another part, or something the AI
/// added on it) is shifted less.
/// </summary>
internal static class RegionShift
{
    /// <summary>Measures a region's typical colors.</summary>
    /// <param name="region">The region.</param>
    /// <param name="pixels">The photo, its 3D view and their regions.</param>
    /// <returns>The region's box and typical colors, or <c>null</c> when it is too small to measure.</returns>
    public static RegionColors? Measure(int region, PhotoPixels pixels)
    {
        if (MaskPixels.Bounds(pixels.Width, pixels.Height, i => pixels.Regions[i] == region) is not { } bounds)
        {
            return null;
        }

        var reach = (int)Math.Ceiling(CC.Feather * CC.FeatherReach);
        var (left, top) = (Math.Max(0, bounds.X0 - reach), Math.Max(0, bounds.Y0 - reach));
        var (width, height) = (Math.Min(pixels.Width, bounds.X1 + reach) - left, Math.Min(pixels.Height, bounds.Y1 + reach) - top);
        var member = new float[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                member[y * width + x] = pixels.Regions[(top + y) * pixels.Width + left + x] == region ? 1 : 0;
            }
        }

        var inside = Inside(member, width, height).Select(n => (top + n / width) * pixels.Width + left + n % width).ToList();

        if (inside.Count < CC.MinInsidePixels)
        {
            return null;
        }

        var main = MainPart(inside, pixels.Photo);
        var (mainPhoto, mainGuide) = (Medians(main, pixels.Photo), Medians(main, pixels.Guide));
        var (allPhoto, allGuide) = (Medians(inside, pixels.Photo), Medians(inside, pixels.Guide));

        // The main part, or the whole: whichever the photo shows nearer the 3D view's color. A white cooker's white body
        // is nearer than the grey between it and its black window; a wooden table top, under its white books, is nearer
        // a planned mustard than the books.
        return Distance(mainPhoto, mainGuide) <= Distance(allPhoto, allGuide)
            ? new RegionColors(region, left, top, width, height, member, main.Count, mainPhoto, mainGuide)
            : new RegionColors(region, left, top, width, height, member, inside.Count, allPhoto, allGuide);
    }

    /// <summary>Adds a measured region's shift to the photo's shifts.</summary>
    /// <param name="colors">The region, measured.</param>
    /// <param name="light">The light kept from the photo (three Lab values, added to the 3D view's color), or zeros.</param>
    /// <param name="pixels">The photo, its 3D view and their regions.</param>
    /// <param name="shift">Each pixel's summed shift (three Lab values each), added to.</param>
    /// <param name="weight">Each pixel's summed weight, added to.</param>
    /// <returns>Whether the region is shifted: its colors differ.</returns>
    public static bool Apply(RegionColors colors, float[] light, PhotoPixels pixels, float[] shift, float[] weight)
    {
        var move = new[] { colors.Guide[0] + light[0] - colors.Photo[0], colors.Guide[1] + light[1] - colors.Photo[1], colors.Guide[2] + light[2] - colors.Photo[2] };
        var likeness = CC.Likeness * CC.Likeness;

        if (move.All(m => Math.Abs(m) < CC.MinShift))
        {
            return false;
        }

        var soft = Blur(colors.Member, colors.Width, colors.Height);

        for (var y = 0; y < colors.Height; y++)
        {
            for (var x = 0; x < colors.Width; x++)
            {
                var s = soft[y * colors.Width + x];

                if (s < CC.MinWeight)
                {
                    continue;
                }

                var i = (colors.Top + y) * pixels.Width + colors.Left + x;
                var (dl, da, db) = (pixels.Photo[i * 3] - colors.Photo[0], pixels.Photo[i * 3 + 1] - colors.Photo[1], pixels.Photo[i * 3 + 2] - colors.Photo[2]);
                var w = s * (float)Math.Exp(-(dl * dl + da * da + db * db) / likeness);

                shift[i * 3] += w * move[0];
                shift[i * 3 + 1] += w * move[1];
                shift[i * 3 + 2] += w * move[2];
                weight[i] += s;
            }
        }

        return true;
    }

    /// <summary>
    /// The region's pixels at least <see cref="CC.EdgeMargin"/> from its edge (and from the box's): those whose whole
    /// square around them is in the region, counted with a summed-area table.
    /// </summary>
    /// <param name="member">1 where the box's pixel is in the region, else 0.</param>
    /// <param name="width">The box's width.</param>
    /// <param name="height">The box's height.</param>
    /// <returns>The inside pixels' positions in the box.</returns>
    private static List<int> Inside(float[] member, int width, int height)
    {
        var r = CC.EdgeMargin;
        var full = (2 * r + 1) * (2 * r + 1);
        var sums = new int[(width + 1) * (height + 1)];
        var inside = new List<int>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                sums[(y + 1) * (width + 1) + x + 1] = (int)member[y * width + x] + sums[y * (width + 1) + x + 1] + sums[(y + 1) * (width + 1) + x] - sums[y * (width + 1) + x];
            }
        }

        for (var y = r; y < height - r; y++)
        {
            for (var x = r; x < width - r; x++)
            {
                var (top, bottom, left, right) = (y - r, y + r + 1, x - r, x + r + 1);
                var count = sums[bottom * (width + 1) + right] - sums[top * (width + 1) + right] - sums[bottom * (width + 1) + left] + sums[top * (width + 1) + left];

                if (count == full)
                {
                    inside.Add(y * width + x);
                }
            }
        }

        return inside;
    }

    /// <summary>
    /// The pixels of a region's main part: those near its most common color in the photo (the fullest cell of a Lab
    /// grid, with its neighbours), or all of them when too few are.
    /// </summary>
    /// <param name="inside">The region's inside pixels, as positions in the picture.</param>
    /// <param name="photo">The photo's pixels in Lab.</param>
    /// <returns>The main part's pixels.</returns>
    private static List<int> MainPart(List<int> inside, float[] photo)
    {
        var cells = new Dictionary<(int, int, int), int>();
        var cell = (int i) => ((int)Math.Floor(photo[i * 3] / CC.ColorCell), (int)Math.Floor(photo[i * 3 + 1] / CC.ColorCell), (int)Math.Floor(photo[i * 3 + 2] / CC.ColorCell));
        var reach = CC.MainPartReach * CC.MainPartReach;

        foreach (var key in inside.Select(cell))
        {
            cells[key] = cells.GetValueOrDefault(key) + 1;
        }

        // The fullest cell counted with its neighbours, so a color on a cell's edge isn't split.
        var fullest = cells.Keys.MaxBy(k => Neighbours(k).Sum(n => cells.GetValueOrDefault(n)));
        var center = new[] { (fullest.Item1 + 0.5) * CC.ColorCell, (fullest.Item2 + 0.5) * CC.ColorCell, (fullest.Item3 + 0.5) * CC.ColorCell };
        var main = inside.Where(i =>
        {
            var (dl, da, db) = (photo[i * 3] - center[0], photo[i * 3 + 1] - center[1], photo[i * 3 + 2] - center[2]);

            return dl * dl + da * da + db * db <= reach;
        }).ToList();

        return main.Count >= CC.MinInsidePixels / 4 ? main : inside;
    }

    /// <summary>A cell of the Lab grid and the 26 around it.</summary>
    /// <param name="cell">The cell.</param>
    private static IEnumerable<(int, int, int)> Neighbours((int L, int A, int B) cell)
    {
        for (var l = -1; l <= 1; l++)
        {
            for (var a = -1; a <= 1; a++)
            {
                for (var b = -1; b <= 1; b++)
                {
                    yield return (cell.L + l, cell.A + a, cell.B + b);
                }
            }
        }
    }

    /// <summary>How far apart two Lab colors are.</summary>
    /// <param name="a">One.</param>
    /// <param name="b">The other.</param>
    private static double Distance(float[] a, float[] b)
    {
        return Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2]));
    }

    /// <summary>The median Lab color of some pixels.</summary>
    /// <param name="at">The pixels' positions in the picture.</param>
    /// <param name="lab">The picture's pixels in Lab.</param>
    /// <returns>Three Lab values.</returns>
    private static float[] Medians(List<int> at, float[] lab)
    {
        var medians = new float[3];

        for (var c = 0; c < 3; c++)
        {
            var values = at.Select(i => lab[i * 3 + c]).ToArray();

            Array.Sort(values);
            medians[c] = values[values.Length / 2];
        }

        return medians;
    }

    /// <summary>A Gaussian blur of the box's region (rows, then columns), for its soft edge.</summary>
    /// <param name="member">1 where the box's pixel is in the region, else 0.</param>
    /// <param name="width">The box's width.</param>
    /// <param name="height">The box's height.</param>
    /// <returns>The blurred values, 0-1.</returns>
    private static float[] Blur(float[] member, int width, int height)
    {
        var reach = (int)Math.Ceiling(CC.Feather * CC.FeatherReach);
        var kernel = Enumerable.Range(-reach, 2 * reach + 1).Select(d => Math.Exp(-d * d / (2 * CC.Feather * CC.Feather))).ToArray();
        var total = kernel.Sum();
        var normal = kernel.Select(k => k / total).ToArray();
        var rows = Pass(member, width, height, normal, down: false);

        return Pass(rows, width, height, normal, down: true);
    }

    /// <summary>One pass of the blur: along each row, or down each column, the edges held.</summary>
    /// <param name="values">The values to blur.</param>
    /// <param name="width">The box's width.</param>
    /// <param name="height">The box's height.</param>
    /// <param name="kernel">The weights, summing to 1, centred.</param>
    /// <param name="down">Whether to blur down the columns instead of along the rows.</param>
    /// <returns>The blurred values.</returns>
    private static float[] Pass(float[] values, int width, int height, double[] kernel, bool down)
    {
        var reach = kernel.Length / 2;
        var result = new float[values.Length];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var sum = 0.0;

                for (var k = -reach; k <= reach; k++)
                {
                    var at = down ? Math.Clamp(y + k, 0, height - 1) * width + x : y * width + Math.Clamp(x + k, 0, width - 1);

                    sum += kernel[k + reach] * values[at];
                }

                result[y * width + x] = (float)sum;
            }
        }

        return result;
    }
}

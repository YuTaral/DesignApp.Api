using System.Text.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Objects;
using SkiaSharp;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>
/// Hand-traced analyses of the sample room photos (the <c>Samples/*.json</c> files): every object's
/// label, category and outline. The fake providers recognize an uploaded sample photo by its
/// <see cref="Fingerprint"/> and return its traced objects, so development shows the real objects
/// of that photo without calling the AI. Any other photo gets the generic layout instead.
/// </summary>
internal static class SampleRooms
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>All traced samples, read once from the embedded JSON files.</summary>
    internal static IReadOnlyList<SampleRoom> All { get; } = Load();

    /// <summary>Finds the sample an analysis image is a copy of.</summary>
    /// <param name="image">The uploaded photo, as the AI providers see it.</param>
    /// <returns>The matching sample, or <c>null</c> for any other photo.</returns>
    internal static SampleRoom? Match(AnalysisImage image)
    {
        var aspect = image.Width / (double)image.Height;
        var candidates = All.Where(s => Math.Abs(s.Width / (double)s.Height / aspect - 1) <= Constants.Samples.MaxAspectDifference).ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var fingerprint = Fingerprint(image.Jpeg);

        return candidates
            .Select(s => (Sample: s, Difference: Difference(fingerprint, s.FingerprintBytes)))
            .Where(c => c.Difference <= Constants.Samples.MaxDifference)
            .OrderBy(c => c.Difference)
            .Select(c => c.Sample)
            .FirstOrDefault();
    }

    /// <summary>
    /// The mean brightness (Rec. 601 luma, over white for transparent pixels) of each cell of a
    /// 16 x 16 grid. Must match <c>fingerprint.py</c>, which wrote the samples' fingerprints.
    /// </summary>
    /// <param name="encoded">The image file.</param>
    internal static byte[] Fingerprint(byte[] encoded)
    {
        using var codec = SKCodec.Create(new MemoryStream(encoded)) ?? throw new ArgumentException("Not an image.", nameof(encoded));
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul, SKColorSpace.CreateSrgb());
        using var bitmap = SKBitmap.Decode(codec, info);
        var cells = new byte[Constants.Samples.Grid * Constants.Samples.Grid];
        var pixels = bitmap.GetPixelSpan();
        var (w, h) = (info.Width, info.Height);

        for (var cy = 0; cy < Constants.Samples.Grid; cy++)
        {
            for (var cx = 0; cx < Constants.Samples.Grid; cx++)
            {
                var (x0, x1, y0, y1) = (cx * w / Constants.Samples.Grid, (cx + 1) * w / Constants.Samples.Grid, cy * h / Constants.Samples.Grid, (cy + 1) * h / Constants.Samples.Grid);
                var total = 0.0;

                for (var y = y0; y < y1; y++)
                {
                    for (var x = x0; x < x1; x++)
                    {
                        var p = (y * w + x) * 4;
                        var a = pixels[p + 3] / 255.0;

                        double Over(byte c)
                        {
                            return c * a + 255 * (1 - a);
                        }
                        total += 0.299 * Over(pixels[p]) + 0.587 * Over(pixels[p + 1]) + 0.114 * Over(pixels[p + 2]);
                    }
                }

                cells[cy * Constants.Samples.Grid + cx] = (byte)Math.Round(total / ((x1 - x0) * (y1 - y0)));
            }
        }

        return cells;
    }

    /// <summary>The mean absolute difference between two fingerprints.</summary>
    /// <param name="a">One fingerprint.</param>
    /// <param name="b">The other.</param>
    internal static double Difference(byte[] a, byte[] b)
    {
        return a.Zip(b, (x, y) => Math.Abs(x - y)).Average();
    }

    /// <summary>
    /// Reads the embedded sample files. A recorded SAM 3 analysis (<c>name.sam3.json</c>) takes
    /// the place of the hand-traced one (<c>name.json</c>) of the same photo.
    /// </summary>
    private static List<SampleRoom> Load()
    {
        var assembly = typeof(SampleRooms).Assembly;

        return [.. assembly.GetManifestResourceNames()
            .Where(n => n.Contains(Constants.Samples.ResourceFolder, StringComparison.Ordinal) && n.EndsWith(Constants.Samples.Extension, StringComparison.Ordinal))
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                var sample = JsonSerializer.Deserialize<SampleRoom>(stream, Options)
                    ?? throw new InvalidDataException($"Sample {n} is empty.");

                return (Sample: sample, Recorded: n.EndsWith(Constants.Samples.RecordedSuffix, StringComparison.Ordinal));
            })
            .GroupBy(s => s.Sample.Name)
            .Select(g => g.OrderByDescending(s => s.Recorded).First().Sample)
            .OrderBy(s => s.Name, StringComparer.Ordinal)];
    }
}

using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;
using SkiaSharp;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>One traced sample photo.</summary>
/// <param name="Name">The photo's file name without extension.</param>
/// <param name="Width">The photo's width; shape coordinates are in its pixels.</param>
/// <param name="Height">The photo's height.</param>
/// <param name="Fingerprint">The photo's <see cref="SampleRooms.Fingerprint"/>, as hex.</param>
/// <param name="Objects">The traced objects, most important first.</param>
internal sealed record SampleRoom(string Name, int Width, int Height, string Fingerprint, IReadOnlyList<SampleObject> Objects)
{
    /// <summary>The fingerprint as bytes.</summary>
    public byte[] FingerprintBytes { get; } = Convert.FromHexString(Fingerprint);

    /// <summary>The objects, with every recorded mask told the photo size it covers.</summary>
    public IReadOnlyList<SampleObject> Objects { get; } = WithPhotoSize(Objects, Width, Height);

    /// <summary>Tells each shape the photo's size, which recorded masks are stretched over.</summary>
    /// <param name="objects">The objects as read.</param>
    /// <param name="width">The photo's width.</param>
    /// <param name="height">The photo's height.</param>
    private static IReadOnlyList<SampleObject> WithPhotoSize(IReadOnlyList<SampleObject> objects, int width, int height)
    {
        foreach (var shape in objects.SelectMany(o => o.Shapes))
            shape.PhotoSize = new SKSize(width, height);
        return objects;
    }

    /// <summary>The objects as the inventory step reports them, with boxes from their outlines.</summary>
    public IReadOnlyList<InventoryItem> Inventory()
    {
        return [.. Objects.Select(o => new InventoryItem(o.Label, o.Category, o.Box(Width, Height), HexColor.Parse(Domain.Constants.Colors.Fallback)))];
    }

    /// <summary>Draws one object's outline at the analysis image's size.</summary>
    /// <param name="label">The object's label.</param>
    /// <param name="width">The analysis image's width.</param>
    /// <param name="height">The analysis image's height.</param>
    /// <returns>The mask, or <c>null</c> when this sample has no object with that label.</returns>
    public ObjectMask? Outline(string label, int width, int height)
    {
        var sampleObject = Objects.FirstOrDefault(o => o.Label == label);
        if (sampleObject is null)
            return null;

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(width / (float)Width, height / (float)Height);
            foreach (var shape in sampleObject.Shapes)
                shape.Draw(canvas);
        }
        return new ObjectMask(width, height, bitmap.GetPixelSpan().ToArray());
    }
}

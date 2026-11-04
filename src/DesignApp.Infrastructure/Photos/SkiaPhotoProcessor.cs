using DesignApp.Application.Photos;
using SkiaSharp;

namespace DesignApp.Infrastructure.Photos;

internal sealed class SkiaPhotoProcessor : IPhotoProcessor
{
    private const string UnsupportedMessage = "This file isn't a photo we can read. Use a JPEG, PNG or WebP image.";

    public ProcessedPhoto Process(Stream upload)
    {
        using var buffer = new MemoryStream();
        upload.CopyTo(buffer);
        if (buffer.Length == 0)
            throw new InvalidPhotoException("The file is empty.");

        using var data = SKData.CreateCopy(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        using var codec = SKCodec.Create(data) ?? throw new InvalidPhotoException(UnsupportedMessage);
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            throw new InvalidPhotoException(UnsupportedMessage);

        var source = codec.Info.Size;
        if ((long)source.Width * source.Height > PhotoRules.MaxSourcePixels)
            throw new InvalidPhotoException("This photo has too many pixels. Use one under 100 megapixels.");
        if (Math.Min(source.Width, source.Height) < PhotoRules.MinShortEdge)
            throw new InvalidPhotoException($"This photo is too small. Use one at least {PhotoRules.MinShortEdge} pixels on each side.");

        // Phones store photos sideways and record how to turn them in EXIF. Bake that in, because
        // the metadata is about to be dropped and the AI and the editor need the upright image.
        var origin = codec.EncodedOrigin;
        var sideways = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var upright = sideways ? new SKSizeI(source.Height, source.Width) : source;

        var scale = Math.Min(1f, (float)PhotoRules.MaxLongEdge / Math.Max(upright.Width, upright.Height));
        var target = new SKSizeI(
            Math.Max(1, (int)Math.Round(upright.Width * scale)),
            Math.Max(1, (int)Math.Round(upright.Height * scale)));

        using var decoded = Decode(codec, scale);
        using var output = new SKBitmap(new SKImageInfo(target.Width, target.Height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()));
        using (var canvas = new SKCanvas(output))
        {
            // JPEG has no transparency, so transparent areas of a PNG become white rather than black.
            canvas.Clear(SKColors.White);
            var (uprightWidth, uprightHeight) = sideways ? (decoded.Height, decoded.Width) : (decoded.Width, decoded.Height);
            canvas.Scale((float)target.Width / uprightWidth, (float)target.Height / uprightHeight);
            var orientation = OrientationMatrix(origin, decoded.Width, decoded.Height);
            canvas.Concat(in orientation);
            using var image = SKImage.FromBitmap(decoded);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), null);
        }

        // Re-encoding writes no metadata, so EXIF (including GPS location) never reaches storage.
        using var encodedImage = SKImage.FromBitmap(output);
        using var encoded = encodedImage.Encode(SKEncodedImageFormat.Jpeg, PhotoRules.JpegQuality);
        return new ProcessedPhoto(encoded.ToArray(), target.Width, target.Height);
    }

    /// <summary>
    /// Decodes in sRGB, converting wide-gamut (Display P3) phone photos. JPEGs decode straight to
    /// 1/2, 1/4 or 1/8 size when that still covers the target, which is much faster for big photos.
    /// </summary>
    private static SKBitmap Decode(SKCodec codec, float scale)
    {
        var size = codec.GetScaledDimensions(scale);
        if (size.Width < codec.Info.Width * scale || size.Height < codec.Info.Height * scale)
            size = codec.Info.Size;

        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success)
        {
            bitmap.Dispose();
            throw new InvalidPhotoException("This photo is damaged and can't be read.");
        }
        return bitmap;
    }

    /// <summary>Maps the stored pixels (<paramref name="w"/> x <paramref name="h"/>) onto the upright image.</summary>
    private static SKMatrix OrientationMatrix(SKEncodedOrigin origin, float w, float h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),     // mirrored
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), // rotated 180
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),   // flipped
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),       // transposed
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),     // rotate 90 clockwise
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), // transverse
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),   // rotate 90 counter-clockwise
        _ => SKMatrix.Identity,
    };
}

using SkiaSharp;

namespace DesignApp.Tests;

/// <summary>Builds small images in memory so tests don't depend on fixture files.</summary>
internal static class TestImages
{
    /// <summary>Color of the top-left quadrant.</summary>
    public static readonly SKColor Red = new(220, 30, 30);
    /// <summary>Color of the rest of the image.</summary>
    public static readonly SKColor Blue = new(30, 60, 220);

    /// <summary>A blue image with a red top-left quadrant, so tests can see how it was turned.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="format">The encoding to use.</param>
    public static byte[] Create(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(Blue);
            using var paint = new SKPaint { Color = Red };
            canvas.DrawRect(0, 0, width / 2f, height / 2f, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 95);
        return data.ToArray();
    }

    /// <summary>Adds an EXIF block with the given orientation tag (1-8) to a JPEG.</summary>
    /// <param name="jpeg">The JPEG to add the block to.</param>
    /// <param name="orientation">The EXIF orientation value.</param>
    public static byte[] WithExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] tiff =
        [
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, // little-endian header, first IFD at offset 8
            0x01, 0x00,                                               // one entry
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,           // tag 0x0112 Orientation, SHORT, count 1
            (byte)orientation, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,                                   // no next IFD
        ];
        byte[] exifHeader = [(byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0];
        var length = 2 + exifHeader.Length + tiff.Length;

        using var output = new MemoryStream();
        output.Write(jpeg, 0, 2); // SOI marker
        output.Write([0xFF, 0xE1, (byte)(length >> 8), (byte)length]);
        output.Write(exifHeader);
        output.Write(tiff);
        output.Write(jpeg, 2, jpeg.Length - 2);
        return output.ToArray();
    }

    /// <summary>A valid 1x1 GIF. Skia can read GIFs but not write them.</summary>
    public static byte[] Gif()
    {
        return Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
    }

    /// <summary>A valid uncompressed 24-bit BMP. Skia can read BMPs but not write them.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    public static byte[] Bmp(int width, int height)
    {
        var rowSize = (width * 3 + 3) / 4 * 4;
        var pixelBytes = rowSize * height;
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(54 + pixelBytes); // file size
        writer.Write(0);               // reserved
        writer.Write(54);              // pixel data offset
        writer.Write(40);              // BITMAPINFOHEADER size
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);        // planes
        writer.Write((short)24);       // bits per pixel
        writer.Write(0);               // no compression
        writer.Write(pixelBytes);
        writer.Write(2835);            // 72 dpi
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        writer.Write(new byte[pixelBytes]);
        writer.Flush();
        return output.ToArray();
    }

    /// <summary>Decodes an encoded image so tests can read its pixels.</summary>
    /// <param name="encoded">The image file's bytes.</param>
    public static SKBitmap Decode(byte[] encoded)
    {
        return SKBitmap.Decode(encoded);
    }
}

using System.Text;
using DesignApp.Application.Photos;
using DesignApp.Infrastructure.Photos;
using DesignApp.Tests;
using SkiaSharp;

namespace DesignApp.UnitTests.Photos;

public class SkiaPhotoProcessorTests
{
    private readonly SkiaPhotoProcessor _processor = new();

    private ProcessedPhoto Process(byte[] file) => _processor.Process(new MemoryStream(file));

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public void Accepted_formats_become_a_jpeg_of_the_same_size(SKEncodedImageFormat format)
    {
        var photo = Process(TestImages.Create(800, 600, format));

        Assert.Equal((800, 600), (photo.Width, photo.Height));
        using var decoded = TestImages.Decode(photo.Content);
        Assert.Equal((800, 600), (decoded.Width, decoded.Height));
        Assert.Equal(SKEncodedImageFormat.Jpeg, SKCodec.Create(new MemoryStream(photo.Content)).EncodedFormat);
    }

    [Fact]
    public void Large_photos_are_scaled_down_to_the_maximum_long_edge()
    {
        var photo = Process(TestImages.Create(6000, 3000));

        Assert.Equal((PhotoRules.MaxLongEdge, PhotoRules.MaxLongEdge / 2), (photo.Width, photo.Height));
    }

    [Fact]
    public void Sideways_phone_photos_are_turned_upright()
    {
        // Orientation 6: the camera was held so the photo must be turned 90 degrees clockwise.
        var sideways = TestImages.WithExifOrientation(TestImages.Create(400, 300), orientation: 6);

        var photo = Process(sideways);

        Assert.Equal((300, 400), (photo.Width, photo.Height));
        using var decoded = TestImages.Decode(photo.Content);
        // The red top-left quadrant ends up top-right after a clockwise turn.
        AssertClose(TestImages.Red, decoded.GetPixel(290, 10));
        AssertClose(TestImages.Blue, decoded.GetPixel(10, 10));
    }

    [Fact]
    public void Metadata_such_as_exif_is_removed()
    {
        var withExif = TestImages.WithExifOrientation(TestImages.Create(400, 300), orientation: 1);
        Assert.Contains("Exif", Encoding.ASCII.GetString(withExif), StringComparison.Ordinal);

        var photo = Process(withExif);

        Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(photo.Content), StringComparison.Ordinal);
    }

    [Fact]
    public void Too_small_photos_are_rejected()
    {
        var error = Assert.Throws<InvalidPhotoException>(() => Process(TestImages.Create(200, 600)));
        Assert.Contains("too small", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gif_files_are_rejected() =>
        Assert.Throws<InvalidPhotoException>(() => Process(TestImages.Gif()));

    [Fact]
    public void Bmp_files_are_rejected() =>
        Assert.Throws<InvalidPhotoException>(() => Process(TestImages.Bmp(400, 300)));

    [Fact]
    public void Files_that_are_not_images_are_rejected() =>
        Assert.Throws<InvalidPhotoException>(() => Process(Encoding.UTF8.GetBytes("definitely not a photo")));

    [Fact]
    public void Empty_files_are_rejected() =>
        Assert.Throws<InvalidPhotoException>(() => Process([]));

    [Fact]
    public void Truncated_photos_are_rejected()
    {
        var jpeg = TestImages.Create(800, 600);

        Assert.Throws<InvalidPhotoException>(() => Process(jpeg[..(jpeg.Length / 2)]));
    }

    private static void AssertClose(SKColor expected, SKColor actual, int tolerance = 40)
    {
        Assert.InRange(actual.Red, Math.Max(0, expected.Red - tolerance), Math.Min(255, expected.Red + tolerance));
        Assert.InRange(actual.Green, Math.Max(0, expected.Green - tolerance), Math.Min(255, expected.Green + tolerance));
        Assert.InRange(actual.Blue, Math.Max(0, expected.Blue - tolerance), Math.Min(255, expected.Blue + tolerance));
    }
}

using DesignApp.Domain.Objects;

namespace DesignApp.Infrastructure.Photos;

/// <summary>
/// Converts between sRGB and CIE Lab (D65): Lab is close to how people see color differences, so a color can be moved
/// in it without changing its texture's look.
/// </summary>
internal static class LabColor
{
    /// <summary>The D65 white point's X, relative to its Y.</summary>
    private const double WhiteX = 0.95047;

    /// <summary>The D65 white point's Z, relative to its Y.</summary>
    private const double WhiteZ = 1.08883;

    /// <summary>Below this, the Lab curve is a straight line (6/29 cubed).</summary>
    private const double Epsilon = 216.0 / 24389;

    /// <summary>The slope of that straight line (29/3 cubed).</summary>
    private const double Kappa = 24389.0 / 27;

    /// <summary>Each 8-bit sRGB value in linear light.</summary>
    private static readonly double[] Linear = [.. Enumerable.Range(0, 256).Select(v => ToLinear(v / 255.0))];

    /// <summary>A pixel's color in Lab.</summary>
    /// <param name="r">Its red, 0-255.</param>
    /// <param name="g">Its green.</param>
    /// <param name="b">Its blue.</param>
    /// <returns>Its lightness (0-100), and its green-red and blue-yellow axes.</returns>
    public static (float L, float A, float B) FromRgb(byte r, byte g, byte b)
    {
        var (lr, lg, lb) = (Linear[r], Linear[g], Linear[b]);
        var x = Curve((0.4124 * lr + 0.3576 * lg + 0.1805 * lb) / WhiteX);
        var y = Curve(0.2126 * lr + 0.7152 * lg + 0.0722 * lb);
        var z = Curve((0.0193 * lr + 0.1192 * lg + 0.9505 * lb) / WhiteZ);

        return ((float)(116 * y - 16), (float)(500 * (x - y)), (float)(200 * (y - z)));
    }

    /// <summary>A planned color in Lab.</summary>
    /// <param name="color">The color.</param>
    /// <returns>Its lightness, and its green-red and blue-yellow axes.</returns>
    public static (float L, float A, float B) FromHex(HexColor color)
    {
        var value = Convert.ToInt32(color.Value[1..], 16);

        return FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    /// <summary>A Lab color in sRGB, clipped to what sRGB can show.</summary>
    /// <param name="l">Its lightness.</param>
    /// <param name="a">Its green-red axis.</param>
    /// <param name="b">Its blue-yellow axis.</param>
    /// <returns>Its red, green and blue, 0-255.</returns>
    public static (byte R, byte G, byte B) ToRgb(double l, double a, double b)
    {
        var fy = (l + 16) / 116;
        var x = Uncurve(fy + a / 500) * WhiteX;
        var y = Uncurve(fy);
        var z = Uncurve(fy - b / 200) * WhiteZ;

        return (
            ToByte(3.2406 * x - 1.5372 * y - 0.4986 * z),
            ToByte(-0.9689 * x + 1.8758 * y + 0.0415 * z),
            ToByte(0.0557 * x - 0.2040 * y + 1.0570 * z));
    }

    /// <summary>An sRGB value (0-1) in linear light.</summary>
    /// <param name="value">The value.</param>
    private static double ToLinear(double value)
    {
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    /// <summary>A linear-light value as an 8-bit sRGB value, clipped.</summary>
    /// <param name="value">The value.</param>
    private static byte ToByte(double value)
    {
        var clipped = Math.Clamp(value, 0, 1);
        var encoded = clipped <= 0.0031308 ? 12.92 * clipped : 1.055 * Math.Pow(clipped, 1 / 2.4) - 0.055;

        return (byte)Math.Round(encoded * 255);
    }

    /// <summary>Lab's curve of a share of white.</summary>
    /// <param name="t">The share.</param>
    private static double Curve(double t)
    {
        return t > Epsilon ? Math.Cbrt(t) : (Kappa * t + 16) / 116;
    }

    /// <summary>The inverse of <see cref="Curve"/>.</summary>
    /// <param name="f">The curved value.</param>
    private static double Uncurve(double f)
    {
        var cubed = f * f * f;

        return cubed > Epsilon ? cubed : (116 * f - 16) / Kappa;
    }
}

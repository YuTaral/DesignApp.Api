using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace DesignApp.Infrastructure.Ai.Fal;

/// <summary>
/// Reads the points of a binary little-endian PLY point cloud, as MoGe-2 exports it (x, y, z as floats, then colors).
/// MoGe-2 writes them OpenGL style (y up, the camera looking down -z); they are returned camera style instead: x right,
/// y down, z forward, so a point's pixel is its x/z and y/z through the camera matrix.
/// </summary>
internal static class PlyCloud
{
    private static readonly Dictionary<string, int> TypeSizes = new(StringComparer.Ordinal)
    {
        ["char"] = 1, ["uchar"] = 1, ["int8"] = 1, ["uint8"] = 1,
        ["short"] = 2, ["ushort"] = 2, ["int16"] = 2, ["uint16"] = 2,
        ["int"] = 4, ["uint"] = 4, ["int32"] = 4, ["uint32"] = 4, ["float"] = 4, ["float32"] = 4,
        ["double"] = 8, ["float64"] = 8,
    };

    /// <summary>The points of a PLY file, camera style.</summary>
    /// <param name="ply">The file.</param>
    /// <returns>Every vertex.</returns>
    /// <exception cref="FormatException">The file isn't a binary little-endian PLY with float x, y and z.</exception>
    public static Vector3[] Read(ReadOnlySpan<byte> ply)
    {
        var marker = "end_header\n"u8;
        var end = ply.IndexOf(marker);
        if (end < 0)
            throw new FormatException("The point cloud has no PLY header.");
        var header = Encoding.ASCII.GetString(ply[..end]).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (header.Length == 0 || header[0] != "ply" || !header.Contains("format binary_little_endian 1.0"))
            throw new FormatException("The point cloud isn't a binary little-endian PLY.");

        // The vertex element comes first; its properties give the stride and where x, y and z are.
        int count = -1, stride = 0, x = -1, y = -1, z = -1;
        var inVertex = false;
        foreach (var line in header)
        {
            var parts = line.Split(' ');
            if (parts[0] == "element")
            {
                if (count >= 0 && inVertex)
                    break;
                inVertex = parts[1] == "vertex";
                if (inVertex)
                    count = int.Parse(parts[2], CultureInfo.InvariantCulture);
                else if (count < 0)
                    throw new FormatException("The point cloud's vertices aren't its first element.");
            }
            else if (parts[0] == "property" && inVertex)
            {
                if (parts[1] == "list" || !TypeSizes.TryGetValue(parts[1], out var size))
                    throw new FormatException($"The point cloud has an unexpected vertex property: {line}");
                switch (parts[2])
                {
                    case "x": x = parts[1] is "float" or "float32" ? stride : -2; break;
                    case "y": y = parts[1] is "float" or "float32" ? stride : -2; break;
                    case "z": z = parts[1] is "float" or "float32" ? stride : -2; break;
                }
                stride += size;
            }
        }
        if (count < 0 || x < 0 || y < 0 || z < 0)
            throw new FormatException("The point cloud has no float x, y and z.");

        var body = ply[(end + marker.Length)..];
        if (body.Length < (long)count * stride)
            throw new FormatException("The point cloud is shorter than its header says.");
        var points = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            var vertex = body.Slice(i * stride, stride);
            points[i] = new Vector3(
                BinaryPrimitives.ReadSingleLittleEndian(vertex[x..]),
                -BinaryPrimitives.ReadSingleLittleEndian(vertex[y..]),
                -BinaryPrimitives.ReadSingleLittleEndian(vertex[z..]));
        }
        return points;
    }
}

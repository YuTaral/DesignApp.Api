using System.Collections.ObjectModel;
using System.Text.Json;
using DesignApp.Domain.Objects;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DesignApp.Infrastructure.Database;

/// <summary>Stores the sizes of a design room's walls, floors and ceilings as a jsonb array.</summary>
internal static class SizesJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Converts the sizes to and from the jsonb column.</summary>
    public static readonly ValueConverter<IReadOnlyList<SurfaceSize>, string> Converter = new(
        sizes => Serialize(sizes),
        json => Deserialize(json));

    /// <summary>Compares size lists by value, so changed sizes are saved.</summary>
    public static readonly ValueComparer<IReadOnlyList<SurfaceSize>> Comparer = new(
        (a, b) => a!.SequenceEqual(b!),
        sizes => sizes.Aggregate(0, (hash, s) => HashCode.Combine(hash, s)),
        sizes => sizes.ToList().AsReadOnly());

    /// <summary>The sizes as JSON.</summary>
    /// <param name="sizes">The sizes.</param>
    private static string Serialize(IReadOnlyList<SurfaceSize> sizes)
    {
        return JsonSerializer.Serialize(sizes.Select(s => new StoredSize(s.ObjectIndex, s.Width, s.Height)), Options);
    }

    /// <summary>The sizes stored in the JSON.</summary>
    /// <param name="json">The column's value.</param>
    private static ReadOnlyCollection<SurfaceSize> Deserialize(string json)
    {
        var stored = JsonSerializer.Deserialize<List<StoredSize>>(json, Options) ?? [];
        return stored.Select(s => new SurfaceSize(s.ObjectIndex, s.Width, s.Height)).ToList().AsReadOnly();
    }

    /// <summary>One size as stored.</summary>
    /// <param name="ObjectIndex">The surface.</param>
    /// <param name="Width">Its width, in metres.</param>
    /// <param name="Height">Its height or length, in metres.</param>
    private sealed record StoredSize(int ObjectIndex, double Width, double Height);
}

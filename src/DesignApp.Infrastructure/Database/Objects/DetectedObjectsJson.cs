using System.Collections.ObjectModel;
using System.Text.Json;
using DesignApp.Domain.Objects;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Database;

/// <summary>
/// Stores <see cref="DetectedObject"/>s through a flat record, so renaming a domain property
/// can't silently change the stored JSON.
/// </summary>
internal static class DetectedObjectsJson
{
    /// <summary>camelCase JSON, as stored in the column.</summary>
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Converts the objects list to and from the jsonb column.</summary>
    public static readonly ValueConverter<IReadOnlyList<DetectedObject>, string> Converter = new(
        objects => Serialize(objects),
        json => Deserialize(json));

    /// <summary>Lets EF Core detect changes to the list by comparing its items.</summary>
    public static readonly ValueComparer<IReadOnlyList<DetectedObject>> Comparer = new(
        (a, b) => a!.SequenceEqual(b!),
        objects => objects.Aggregate(0, (hash, o) => HashCode.Combine(hash, o)),
        objects => objects.ToList().AsReadOnly());

    /// <summary>Writes the objects as JSON.</summary>
    /// <param name="objects">The objects to write.</param>
    private static string Serialize(IReadOnlyList<DetectedObject> objects)
    {
        return JsonSerializer.Serialize(objects.Select(StoredObject.From), Options);
    }

    /// <summary>Reads the objects back from JSON.</summary>
    /// <param name="json">The stored column value.</param>
    private static ReadOnlyCollection<DetectedObject> Deserialize(string json)
    {
        return (JsonSerializer.Deserialize<List<StoredObject>>(json, Options) ?? []).Select(o => o.ToDomain()).ToList().AsReadOnly();
    }

    /// <summary>The stored JSON shape of one object, kept separate from the domain type.</summary>
    /// <param name="Index">Label-map value.</param>
    /// <param name="Label">What the object is.</param>
    /// <param name="Category">The kind of object.</param>
    /// <param name="X">Box left edge.</param>
    /// <param name="Y">Box top edge.</param>
    /// <param name="Width">Box width.</param>
    /// <param name="Height">Box height.</param>
    /// <param name="Color">Dominant color as "#RRGGBB".</param>
    /// <param name="Hand">Whether the user marked it by hand; absent (false) for objects stored before that existed.</param>
    /// <param name="Parent">For a part, the object it is a part of; absent for whole objects.</param>
    private sealed record StoredObject(
        int Index, string Label, ObjectCategory Category, double X, double Y, double Width, double Height, string Color, bool Hand = false, int? Parent = null)
    {
        /// <summary>Flattens a domain object for storage.</summary>
        /// <param name="o">The object to store.</param>
        public static StoredObject From(DetectedObject o)
        {
            return new(o.Index, o.Label, o.Category, o.Box.X, o.Box.Y, o.Box.Width, o.Box.Height, o.DominantColor.Value, o.MarkedByHand, o.ParentIndex);
        }

        /// <summary>Rebuilds the domain object, re-checking its rules.</summary>
        public DetectedObject ToDomain()
        {
            return new(Index, Label, Category, new BoundingBox(X, Y, Width, Height), HexColor.Parse(Color), Hand, Parent);
        }
    }
}

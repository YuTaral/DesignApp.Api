using System.Text.Json;
using DesignApp.Domain.Objects;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DesignApp.Infrastructure.Database;

/// <summary>Stores a built room's outline as a jsonb array of its corners, <c>[{"x":0,"y":0}, ...]</c>.</summary>
internal static class RoomOutlineJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Converts the outline to and from the jsonb column.</summary>
    public static readonly ValueConverter<RoomOutline, string> Converter = new(
        outline => Serialize(outline),
        json => Deserialize(json));

    /// <summary>Compares outlines by their corners, so a changed outline is saved.</summary>
    public static readonly ValueComparer<RoomOutline> Comparer = new(
        (a, b) => Equals(a, b),
        outline => outline.GetHashCode(),
        outline => new RoomOutline(outline.Points));

    /// <summary>The outline as JSON.</summary>
    /// <param name="outline">The outline.</param>
    private static string Serialize(RoomOutline outline)
    {
        return JsonSerializer.Serialize(outline.Points, Options);
    }

    /// <summary>The outline stored in the JSON.</summary>
    /// <param name="json">The column's value.</param>
    private static RoomOutline Deserialize(string json)
    {
        return new RoomOutline(JsonSerializer.Deserialize<List<OutlinePoint>>(json, Options) ?? []);
    }
}

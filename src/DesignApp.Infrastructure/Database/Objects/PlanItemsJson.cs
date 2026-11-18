using System.Collections.ObjectModel;
using System.Text.Json;
using DesignApp.Domain.Objects;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Database;

/// <summary>Stores the objects of a built room as a jsonb array (always read and written with their plan).</summary>
internal static class PlanItemsJson
{
    private static readonly JsonSerializerOptions Options = JsonColumnOptions.Value;

    /// <summary>Converts the objects to and from the jsonb column.</summary>
    public static readonly ValueConverter<IReadOnlyList<PlanItem>, string> Converter = new(
        items => Serialize(items),
        json => Deserialize(json));

    /// <summary>Compares object lists by value, so changed objects are saved.</summary>
    public static readonly ValueComparer<IReadOnlyList<PlanItem>> Comparer = new(
        (a, b) => a!.SequenceEqual(b!),
        items => items.Aggregate(0, (hash, i) => HashCode.Combine(hash, i)),
        items => items.ToList().AsReadOnly());

    /// <summary>The objects as JSON.</summary>
    /// <param name="items">The objects.</param>
    private static string Serialize(IReadOnlyList<PlanItem> items)
    {
        return JsonSerializer.Serialize(items.Select(StoredItem.From), Options);
    }

    /// <summary>The objects stored in the JSON.</summary>
    /// <param name="json">The column's value.</param>
    private static ReadOnlyCollection<PlanItem> Deserialize(string json)
    {
        return (JsonSerializer.Deserialize<List<StoredItem>>(json, Options) ?? []).Select(i => i.ToDomain()).ToList().AsReadOnly();
    }

    /// <summary>One object as stored.</summary>
    /// <param name="Label">What it is.</param>
    /// <param name="Shape">How it is drawn.</param>
    /// <param name="X">Its middle across the room, in centimetres.</param>
    /// <param name="Y">Its middle along the room, in centimetres.</param>
    /// <param name="Width">Its width, in centimetres.</param>
    /// <param name="Depth">Its depth, in centimetres.</param>
    /// <param name="Height">Its height, in centimetres.</param>
    /// <param name="Rotation">Its turn, in degrees.</param>
    /// <param name="Color">Its color, #RRGGBB.</param>
    /// <param name="Wall">The number of the wall a window or door is on (missing for other objects).</param>
    /// <param name="Elevation">A window's height above the floor, in centimetres (missing means 0).</param>
    /// <param name="Form">Its outline from above (missing for a rectangle, and in older plans).</param>
    /// <param name="Parts">An L- or P-shaped object's parts (missing for other outlines; the usual ones in older plans).</param>
    /// <param name="Decorations">The small things on it (missing when none).</param>
    /// <param name="Material">What it is made of (missing when none, and in older plans).</param>
    private sealed record StoredItem(string Label, ItemShape Shape, double X, double Y, double Width, double Depth, double Height, int Rotation, string Color,
        int? Wall = null, double Elevation = 0, ItemForm? Form = null, ItemParts? Parts = null, IReadOnlyList<string>? Decorations = null, string? Material = null)
    {
        /// <summary>The stored form of an object.</summary>
        /// <param name="i">The object.</param>
        public static StoredItem From(PlanItem i)
        {
            return new(i.Label, i.Shape, i.X, i.Y, i.Width, i.Depth, i.Height, i.Rotation, i.Color.Value, i.Wall, i.Elevation,
                i.Form == ItemForm.Standard ? null : i.Form, i.Parts, i.Decorations.IsEmpty ? null : i.Decorations.Names, i.Material.Length == 0 ? null : i.Material);
        }

        /// <summary>The object this stored form describes.</summary>
        public PlanItem ToDomain()
        {
            return new(Label, Shape, X, Y, Width, Depth, Height, Rotation, HexColor.Parse(Color), Wall, Elevation, Form ?? ItemForm.Standard, Parts,
                Decorations is null ? null : new ItemDecorations(Decorations), Material);
        }
    }
}

using System.Collections.ObjectModel;
using System.Text.Json;
using DesignApp.Domain.Common;
using DesignApp.Domain.Designs;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DesignApp.Infrastructure.Database;

/// <summary>Stores a room's edits as JSON, each with a type so new kinds of edits can be added.</summary>
internal static class EditsJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Converts the edits to and from the jsonb column.</summary>
    public static readonly ValueConverter<IReadOnlyList<EditOperation>, string> Converter = new(
        edits => Serialize(edits),
        json => Deserialize(json));

    /// <summary>Compares edit lists by value, so changed edits are saved.</summary>
    public static readonly ValueComparer<IReadOnlyList<EditOperation>> Comparer = new(
        (a, b) => a!.SequenceEqual(b!),
        edits => edits.Aggregate(0, (hash, e) => HashCode.Combine(hash, e)),
        edits => edits.ToList().AsReadOnly());

    /// <summary>The edits as JSON.</summary>
    /// <param name="edits">The edits.</param>
    private static string Serialize(IReadOnlyList<EditOperation> edits) =>
        JsonSerializer.Serialize(edits.Select(StoredEdit.From), Options);

    /// <summary>The edits stored in the JSON.</summary>
    /// <param name="json">The column's value.</param>
    private static ReadOnlyCollection<EditOperation> Deserialize(string json) =>
        (JsonSerializer.Deserialize<List<StoredEdit>>(json, Options) ?? []).Select(e => e.ToDomain()).ToList().AsReadOnly();

    /// <summary>One edit as stored.</summary>
    /// <param name="Type">The kind of edit; only "recolor" so far.</param>
    /// <param name="ObjectIndex">The edited object.</param>
    /// <param name="Color">The new color.</param>
    /// <param name="Strength">How strongly it is applied.</param>
    private sealed record StoredEdit(string Type, int ObjectIndex, string Color, double Strength)
    {
        /// <summary>The stored form of an edit.</summary>
        /// <param name="edit">The edit.</param>
        public static StoredEdit From(EditOperation edit) => edit switch
        {
            RecolorEdit r => new(Constants.Persistence.RecolorEdit, r.ObjectIndex, r.Color.Value, r.Strength),
            _ => throw new NotSupportedException($"Edits of type {edit.GetType().Name} can't be stored yet."),
        };

        /// <summary>The edit this stored form describes.</summary>
#pragma warning disable CA1859 // more kinds of edits will come; callers only need EditOperation
        public EditOperation ToDomain() => Type switch
        {
            Constants.Persistence.RecolorEdit => new RecolorEdit(ObjectIndex, HexColor.Parse(Color), Strength),
            _ => throw new NotSupportedException($"Unknown stored edit type '{Type}'."),
        };
#pragma warning restore CA1859
    }
}

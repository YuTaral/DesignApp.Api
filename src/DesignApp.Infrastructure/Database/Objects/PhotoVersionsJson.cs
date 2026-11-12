using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DesignApp.Infrastructure.Database;

/// <summary>Stores a room's photo version per label map version as a jsonb array of numbers.</summary>
internal static class PhotoVersionsJson
{
    /// <summary>Between the list and its JSON text.</summary>
    public static readonly ValueConverter<IReadOnlyList<int>, string> Converter = new(
        versions => JsonSerializer.Serialize(versions, (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<int[]>(json, (JsonSerializerOptions?)null) ?? Array.Empty<int>());

    /// <summary>Compares lists by their numbers, so EF Core sees a change.</summary>
    public static readonly ValueComparer<IReadOnlyList<int>> Comparer = new(
        (a, b) => a!.SequenceEqual(b!),
        versions => versions.Aggregate(0, HashCode.Combine),
        versions => versions.ToArray());
}

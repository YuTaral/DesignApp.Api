using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DesignApp.Infrastructure.Database;

/// <summary>Stores an optional value as a jsonb object (no value when there is none), such as a plan's camera spot.</summary>
/// <typeparam name="T">The value's type.</typeparam>
internal static class OptionalJson<T>
    where T : class
{
    private static readonly JsonSerializerOptions Options = JsonColumnOptions.Value;

    /// <summary>Converts the value to and from the jsonb column.</summary>
    public static readonly ValueConverter<T?, string?> Converter = new(
        value => Serialize(value),
        json => Deserialize(json));

    /// <summary>The value as JSON.</summary>
    /// <param name="value">The value.</param>
    private static string? Serialize(T? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value, Options);
    }

    /// <summary>The value stored in the JSON.</summary>
    /// <param name="json">The column's value.</param>
    private static T? Deserialize(string? json)
    {
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<T>(json, Options);
    }
}

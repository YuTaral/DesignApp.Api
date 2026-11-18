using System.Text.Json;
using DesignApp.Domain.Objects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DesignApp.Infrastructure.Database;

/// <summary>Stores a built room's camera spot as a jsonb point (no value when it has none).</summary>
internal static class CameraSpotJson
{
    private static readonly JsonSerializerOptions Options = JsonColumnOptions.Value;

    /// <summary>Converts the spot to and from the jsonb column.</summary>
    public static readonly ValueConverter<OutlinePoint?, string?> Converter = new(
        spot => Serialize(spot),
        json => Deserialize(json));

    /// <summary>The spot as JSON.</summary>
    /// <param name="spot">The spot.</param>
    private static string? Serialize(OutlinePoint? spot)
    {
        return spot is null ? null : JsonSerializer.Serialize(spot, Options);
    }

    /// <summary>The spot stored in the JSON.</summary>
    /// <param name="json">The column's value.</param>
    private static OutlinePoint? Deserialize(string? json)
    {
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<OutlinePoint>(json, Options);
    }
}

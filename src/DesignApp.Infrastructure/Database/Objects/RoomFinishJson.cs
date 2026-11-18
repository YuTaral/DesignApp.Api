using System.Text.Json;
using DesignApp.Domain.Objects;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Database;

/// <summary>Stores how a built room's walls and ceiling are finished and lit as a jsonb object.</summary>
internal static class RoomFinishJson
{
    private static readonly JsonSerializerOptions Options = JsonColumnOptions.Value;

    /// <summary>Converts the finish to and from the jsonb column.</summary>
    public static readonly ValueConverter<RoomFinish, string> Converter = new(
        finish => Serialize(finish),
        json => Deserialize(json));

    /// <summary>Compares finishes by value, so a changed finish is saved.</summary>
    public static readonly ValueComparer<RoomFinish> Comparer = new(
        (a, b) => Equals(a, b),
        finish => finish.GetHashCode(),
        finish => finish);

    /// <summary>The finish as JSON.</summary>
    /// <param name="finish">The finish.</param>
    private static string Serialize(RoomFinish finish)
    {
        return JsonSerializer.Serialize(StoredFinish.From(finish), Options);
    }

    /// <summary>The finish stored in the JSON.</summary>
    /// <param name="json">The column's value.</param>
    private static RoomFinish Deserialize(string json)
    {
        return JsonSerializer.Deserialize<StoredFinish>(json, Options)?.ToDomain() ?? RoomFinish.Default;
    }

    /// <summary>A finish as stored.</summary>
    /// <param name="Walls">What the walls are finished in.</param>
    /// <param name="AccentWall">The accent wall's number (missing for none).</param>
    /// <param name="AccentColor">The accent wall's color, #RRGGBB.</param>
    /// <param name="AccentFinish">What the accent wall is finished in.</param>
    /// <param name="CeilingColor">The ceiling's color, #RRGGBB.</param>
    /// <param name="TrimColor">The skirting boards' color, #RRGGBB.</param>
    /// <param name="CeilingLight">The ceiling light.</param>
    /// <param name="TimeOfDay">When the photo is taken.</param>
    private sealed record StoredFinish(WallFinish Walls, int? AccentWall, string AccentColor, WallFinish AccentFinish, string CeilingColor, string TrimColor,
        CeilingLight CeilingLight, TimeOfDay TimeOfDay)
    {
        /// <summary>The stored form of a finish.</summary>
        /// <param name="f">The finish.</param>
        public static StoredFinish From(RoomFinish f)
        {
            return new(f.Walls, f.AccentWall, f.AccentColor.Value, f.AccentFinish, f.CeilingColor.Value, f.TrimColor.Value, f.CeilingLight, f.TimeOfDay);
        }

        /// <summary>The finish this stored form describes.</summary>
        public RoomFinish ToDomain()
        {
            return new(Walls, AccentWall, HexColor.Parse(AccentColor), AccentFinish, HexColor.Parse(CeilingColor), HexColor.Parse(TrimColor), CeilingLight, TimeOfDay);
        }
    }
}

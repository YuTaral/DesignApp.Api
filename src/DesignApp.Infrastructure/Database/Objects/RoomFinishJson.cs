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

    /// <summary>
    /// A finish as stored. Finishes stored before walls had their own looks have one accent wall instead
    /// (<paramref name="AccentWall"/>, <paramref name="AccentColor"/>, <paramref name="AccentFinish"/>): it is read as
    /// that wall's own look, and saved again in the new form. Rows from before ceiling lights were objects had a
    /// <c>ceilingLight</c>, which a migration turned into a light on the plan (it is no longer read); they have no
    /// <paramref name="Tone"/> or <paramref name="North"/>, read as warm light with north at the back.
    /// </summary>
    /// <param name="Walls">What the walls are finished in.</param>
    /// <param name="OwnWalls">The walls in their own finish and color (missing in older rows).</param>
    /// <param name="CeilingColor">The ceiling's color, #RRGGBB.</param>
    /// <param name="TrimColor">The skirting boards' color, #RRGGBB.</param>
    /// <param name="TimeOfDay">When the photo is taken.</param>
    /// <param name="Tone">The color of the lights' light (missing in older rows).</param>
    /// <param name="North">The side of the plan facing north (missing in older rows).</param>
    /// <param name="AccentWall">An older row's accent wall's number (missing for none, and in newer rows).</param>
    /// <param name="AccentColor">An older row's accent wall's color, #RRGGBB.</param>
    /// <param name="AccentFinish">What an older row's accent wall is finished in.</param>
    private sealed record StoredFinish(WallFinish Walls, IReadOnlyList<StoredWall>? OwnWalls, string CeilingColor, string TrimColor, TimeOfDay TimeOfDay,
        LightTone? Tone = null, PlanWall? North = null, int? AccentWall = null, string? AccentColor = null, WallFinish? AccentFinish = null)
    {
        /// <summary>The stored form of a finish.</summary>
        /// <param name="f">The finish.</param>
        public static StoredFinish From(RoomFinish f)
        {
            var own = f.OwnWalls.Select(w => new StoredWall(w.Wall, w.Finish, w.Color.Value)).ToList();

            return new(f.Walls, own, f.CeilingColor.Value, f.TrimColor.Value, f.TimeOfDay, f.Tone, f.North);
        }

        /// <summary>The finish this stored form describes.</summary>
        public RoomFinish ToDomain()
        {
            var accent = AccentWall is { } wall ? [new WallLook(wall, AccentFinish ?? WallFinish.Paint, HexColor.Parse(AccentColor ?? ""))] : new List<WallLook>();
            var own = OwnWalls?.Select(w => new WallLook(w.Wall, w.Finish, HexColor.Parse(w.Color))).ToList() ?? accent;

            return new(Walls, own, HexColor.Parse(CeilingColor), HexColor.Parse(TrimColor), TimeOfDay, Tone ?? LightTone.Warm, North ?? PlanWall.Back);
        }
    }

    /// <summary>A wall in its own finish, as stored.</summary>
    /// <param name="Wall">The wall's number.</param>
    /// <param name="Finish">What it is finished in.</param>
    /// <param name="Color">Its color, #RRGGBB.</param>
    private sealed record StoredWall(int Wall, WallFinish Finish, string Color);
}

namespace DesignApp.Api.Requests;

/// <summary>One recolor, as sent and as returned.</summary>
/// <param name="ObjectIndex">The object, as in the room's objects and label map.</param>
/// <param name="Color">The new color as #RRGGBB.</param>
/// <param name="Strength">How strongly it is applied, from 0 to 1.</param>
internal sealed record EditRequest(int ObjectIndex, string? Color, double Strength);

namespace DesignApp.Api.Requests;

/// <summary>One recolor, as sent and as returned.</summary>
/// <param name="ObjectIndex">The object, as in the room's objects and label map.</param>
/// <param name="Color">The new color as #RRGGBB.</param>
/// <param name="Strength">How strongly it is applied, from 0 to 1.</param>
/// <param name="Paint">The real paint the color is, such as <c>ral-classic/9010</c> (see <c>GET /api/palettes</c>); missing for a free color.</param>
internal sealed record EditRequest(int ObjectIndex, string? Color, double Strength, string? Paint = null);

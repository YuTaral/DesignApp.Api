namespace DesignApp.Api.Requests;

/// <summary>One tone of an object split by color (such as the bricks or the mortar of a wall), as sent and as returned.</summary>
/// <param name="Center">The tone's original color in the photo as #RRGGBB; the object's pixels nearest it belong to it.</param>
/// <param name="Share">How much of the object the tone covers, from 0 to 1.</param>
/// <param name="Color">The tone's new color as #RRGGBB; missing to keep it as in the photo.</param>
/// <param name="Strength">How strongly it is applied, from 0 to 1.</param>
/// <param name="Paint">The real paint the color is, such as <c>ral-classic/9010</c>; missing for a free color.</param>
internal sealed record ToneRequest(string? Center, double Share, string? Color, double Strength, string? Paint = null);

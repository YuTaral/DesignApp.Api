using DesignApp.Domain.Objects;

namespace DesignApp.Application.Objects;

/// <summary>One real paint color of a palette, such as RAL 9010 Pure white.</summary>
/// <param name="Id">The paint's id inside its palette, such as <c>9010</c>.</param>
/// <param name="Code">The code shops use, such as <c>RAL 9010</c>.</param>
/// <param name="Name">The color's name, such as "Pure white".</param>
/// <param name="Color">The color on screen (an approximation of the real paint).</param>
public sealed record Paint(string Id, string Code, string Name, HexColor Color);

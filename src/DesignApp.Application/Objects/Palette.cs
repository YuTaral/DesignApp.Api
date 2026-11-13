namespace DesignApp.Application.Objects;

/// <summary>A set of real paint colors from one standard or brand, such as RAL Classic.</summary>
/// <param name="Id">The palette's id, such as <c>ral-classic</c>; it starts every paint reference.</param>
/// <param name="Name">The name people see, such as "RAL Classic".</param>
/// <param name="Notice">Where the colors come from and how exact they are, shown with the palette.</param>
/// <param name="Paints">The paints, in the palette's own order.</param>
public sealed record Palette(string Id, string Name, string Notice, IReadOnlyList<Paint> Paints)
{
    /// <summary>The reference an edit stores for one of this palette's paints, such as <c>ral-classic/9010</c>.</summary>
    /// <param name="paint">The paint.</param>
    public string ReferenceTo(Paint paint)
    {
        return $"{Id}{Domain.Constants.Colors.PaintSeparator}{paint.Id}";
    }
}

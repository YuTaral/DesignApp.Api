using DesignApp.Application.Objects;

namespace DesignApp.Application.Interfaces;

/// <summary>The paint palettes the editor offers, such as RAL Classic. They never change while the API runs.</summary>
public interface IPaletteCatalog
{
    /// <summary>Every palette, in the order they are shown.</summary>
    IReadOnlyList<Palette> Palettes { get; }

    /// <summary>The paint a reference names, or <c>null</c> when there is none.</summary>
    /// <param name="paintReference">The reference, as <c>palette/paint</c> (such as <c>ral-classic/9010</c>).</param>
    Paint? Find(string paintReference);
}

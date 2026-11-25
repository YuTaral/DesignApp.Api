using DesignApp.Domain.Objects;

namespace DesignApp.Application.Objects;

/// <summary>
/// The regions of a photo made from a built room whose colors are brought back to its 3D view: its objects, and the
/// planned colors on its floor, walls and ceiling.
/// </summary>
/// <param name="Objects">
/// The mask ids of the objects to correct (1 + their position in the plan's objects); windows and doors keep the colors
/// the image model gave them, and so does a picture's art, screen or mirror (its masks show only its frame).
/// </param>
/// <param name="Surfaces">
/// For each surface's mask id (see <see cref="Constants.Builder.MaskIds"/>), the colors planned on it: its own first,
/// then each area in another material and the accent wall's. A pixel of the surface counts as the one its 3D view's
/// color is nearest.
/// </param>
/// <param name="KeepsLight">
/// Whether the photo keeps the light the image model gave it (a morning, golden-hour or evening photo): each region
/// moves to its 3D view's color in that light. At midday the photo takes the 3D view's even light.
/// </param>
public sealed record PhotoRegions(IReadOnlySet<int> Objects, IReadOnlyDictionary<int, IReadOnlyList<HexColor>> Surfaces, bool KeepsLight = false);

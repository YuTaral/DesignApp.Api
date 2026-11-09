using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;
using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Analysis;

/// <summary>One object the inventory step found, before it is outlined.</summary>
/// <param name="Label">What the object is, such as "velvet sofa".</param>
/// <param name="Category">The kind of object.</param>
/// <param name="Box">Roughly where the object is.</param>
/// <param name="Color">The object's main color as the AI saw it.</param>
/// <param name="Points">
/// A few spots on the object itself, for walls, floors and ceilings: they tell the outlining step
/// which surface is meant when its box also holds furniture. Empty when the AI gave none.
/// </param>
public sealed record InventoryItem(
    string Label, ObjectCategory Category, BoundingBox Box, HexColor Color, IReadOnlyList<ImagePoint>? Points = null);

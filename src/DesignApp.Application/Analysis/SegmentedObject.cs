namespace DesignApp.Application.Analysis;

/// <summary>An inventory item together with its outline.</summary>
/// <param name="Item">What the object is.</param>
/// <param name="Mask">Its outline.</param>
public sealed record SegmentedObject(InventoryItem Item, ObjectMask Mask);

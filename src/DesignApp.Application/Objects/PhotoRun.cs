namespace DesignApp.Application.Objects;

/// <summary>Where a photo made from a room plan goes: which Generate run it belongs to, and what it is.</summary>
/// <param name="RoomId">The room of the run's earlier photos, to add this one to; <c>null</c> for the run's first, which makes a room.</param>
/// <param name="Note">What the photo is, such as its view's name.</param>
/// <param name="Replaces">
/// For the run's first photo: a room of the same plan which the new room replaces, in its designs and in its place there;
/// the old room and its photos are deleted. <c>null</c> to add the new room instead.
/// </param>
/// <param name="ReferenceBoxes">
/// For the run's later photos: where each object is in the run's first photo, so the image model copies the objects it
/// shows from there (the same furniture, materials and decorations in every view); <c>null</c> for none.
/// </param>
public sealed record PhotoRun(Guid? RoomId, string? Note, Guid? Replaces = null, IReadOnlyList<ItemBox>? ReferenceBoxes = null);

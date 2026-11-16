using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Objects;

/// <summary>
/// Which view of a built room one photo is made from, when it isn't the room's own (the user may make photos of several
/// views at once): the wall it looks at and where the camera stands.
/// </summary>
/// <param name="View">The number of the wall the photo looks at.</param>
/// <param name="Corner">Where the camera stands to look at it.</param>
public sealed record PlanShot(int View, ViewCorner Corner);

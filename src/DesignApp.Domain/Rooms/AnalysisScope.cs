namespace DesignApp.Domain.Rooms;

/// <summary>Which objects the analysis looks for, chosen by the user at upload.</summary>
public enum AnalysisScope
{
    /// <summary>Everything worth restyling: walls, floor, ceiling, doors, windows, furniture and decor.</summary>
    WholeRoom,
    /// <summary>Everything except doors and windows (glass and white door panels outline poorly and are rarely recolored).</summary>
    NoDoorsOrWindows,
    /// <summary>Only objects: furniture, cabinets, textiles, lamps and decor; no walls, floor, ceiling, doors or windows.</summary>
    ObjectsOnly,
}

/// <summary>Rules for <see cref="AnalysisScope"/>.</summary>
public static class AnalysisScopes
{
    /// <summary>Whether objects of a category are looked for in a scope.</summary>
    /// <param name="scope">The room's scope.</param>
    /// <param name="category">The object's category.</param>
    public static bool Includes(this AnalysisScope scope, ObjectCategory category) => scope switch
    {
        AnalysisScope.NoDoorsOrWindows => category is not ObjectCategory.Opening,
        AnalysisScope.ObjectsOnly => !category.IsSurface() && category is not ObjectCategory.Opening,
        _ => true,
    };
}

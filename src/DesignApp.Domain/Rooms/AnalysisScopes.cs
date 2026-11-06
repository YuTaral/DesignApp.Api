using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Rooms;

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

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Extensions;

/// <summary>Rules about object categories.</summary>
public static class ObjectCategories
{
    /// <summary>Whether a category is a large surface of the room: a wall, the floor or the ceiling.</summary>
    /// <param name="category">The category.</param>
    public static bool IsSurface(this ObjectCategory category)
    {
        return category is ObjectCategory.Wall or ObjectCategory.Floor or ObjectCategory.Ceiling;
    }
}

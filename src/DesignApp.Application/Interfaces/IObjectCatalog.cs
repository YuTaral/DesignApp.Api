using DesignApp.Application.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Interfaces;

/// <summary>The common objects of each room type that the room builder offers, with their usual sizes.</summary>
public interface IObjectCatalog
{
    /// <summary>The common objects of a room type, most common first.</summary>
    /// <param name="roomType">The room type.</param>
    IReadOnlyList<CatalogObject> ForRoom(RoomType roomType);
}

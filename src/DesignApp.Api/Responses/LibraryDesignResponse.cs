using DesignApp.Application.Objects;

namespace DesignApp.Api.Responses;

/// <summary>A library design with its rooms.</summary>
/// <param name="Id">The design's id, such as <c>minimalist</c>.</param>
/// <param name="Name">The name people see.</param>
/// <param name="Style">The style.</param>
/// <param name="Description">A sentence about the house.</param>
/// <param name="CoverUrl">Where its cover is: its first rooms in a grid, for lists.</param>
/// <param name="Rooms">The rooms, in the order they are shown.</param>
internal sealed record LibraryDesignResponse(string Id, string Name, string Style, string Description, string CoverUrl, List<LibraryRoomResponse> Rooms)
{
    /// <summary>The response for a library design.</summary>
    /// <param name="design">The design.</param>
    public static LibraryDesignResponse From(LibraryDesign design)
    {
        return new(
            design.Id,
            design.Name,
            design.Style,
            design.Description,
            Constants.Routes.LibraryDesignCoverUrl(design.Id),
            [.. design.Rooms.Select(r => LibraryRoomResponse.From(design.Id, r))]);
    }
}

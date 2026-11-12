using DesignApp.Application.Objects;

namespace DesignApp.Api.Responses;

/// <summary>One room of a library design, with its layouts.</summary>
/// <param name="Id">The room's id inside its design, such as <c>kitchen</c>.</param>
/// <param name="Title">The name people see.</param>
/// <param name="Layouts">Its layouts to choose from; the first is the default.</param>
internal sealed record LibraryRoomResponse(string Id, string Title, List<LibraryLayoutResponse> Layouts)
{
    /// <summary>The response for a library room.</summary>
    /// <param name="designId">Its design's id.</param>
    /// <param name="room">The room.</param>
    public static LibraryRoomResponse From(string designId, LibraryRoom room)
    {
        return new(room.Id, room.Title, [.. room.Layouts.Select(l => LibraryLayoutResponse.From(designId, room.Id, l))]);
    }
}

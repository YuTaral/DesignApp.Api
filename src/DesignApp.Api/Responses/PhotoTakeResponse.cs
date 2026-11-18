using DesignApp.Domain.Entities;

namespace DesignApp.Api.Responses;

/// <summary>One of a room's photos to choose from.</summary>
/// <param name="Id">The photo's id.</param>
/// <param name="PhotoUrl">Where the photo is (it never changes).</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Note">Its view's name, or the change asked for.</param>
/// <param name="ChangedFrom">The photo it was changed from, if any.</param>
/// <param name="CreatedAt">When it was made.</param>
public sealed record PhotoTakeResponse(Guid Id, string PhotoUrl, int Width, int Height, string Note, Guid? ChangedFrom, DateTimeOffset CreatedAt)
{
    /// <summary>A photo as the API returns it.</summary>
    /// <param name="take">The photo.</param>
    public static PhotoTakeResponse From(PhotoTake take)
    {
        ArgumentNullException.ThrowIfNull(take);

        return new(take.Id, Constants.Routes.TakePhotoUrl(take.RoomId, take.Id), take.Width, take.Height, take.Note, take.ChangedFrom, take.CreatedAt);
    }
}

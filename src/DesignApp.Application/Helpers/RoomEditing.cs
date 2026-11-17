using DesignApp.Domain.Entities;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.Helpers;

/// <summary>The checks every change to a room's outlines starts with.</summary>
public static class RoomEditing
{
    /// <summary>
    /// Why a room's outlines can't be changed from a version of them, or <c>null</c> when they can: the analysis must
    /// be done, and the version must be one of the room's.
    /// </summary>
    /// <param name="room">The room.</param>
    /// <param name="baseRevision">The label map version the change starts from.</param>
    /// <returns>The message to show, or <c>null</c>.</returns>
    public static string? Refusal(Room room, int baseRevision)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (room.Status is not RoomStatus.Ready)
        {
            return Constants.Messages.OutlinesNotReady;
        }

        if (!room.HasLabelMapRevision(baseRevision))
        {
            return Constants.Messages.UnknownRevision;
        }

        return null;
    }
}

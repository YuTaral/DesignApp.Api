using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Use case: the user describes a change to one of a room's photos ("make the sofa leather", "evening light"), and the
/// image model makes a new version of that photo with it, added to the room's photos. With the real AI it costs money.
/// </summary>
/// <param name="rooms">The room database.</param>
/// <param name="currentUser">Whose rooms may be changed.</param>
/// <param name="ai">The AI providers.</param>
/// <param name="takes">Stores the new photo.</param>
public sealed class ChangeRoomTake(IRoomRepository rooms, ICurrentUser currentUser, IAiProviders ai, TakeStore takes)
{
    /// <summary>Makes the changed photo.</summary>
    /// <param name="roomId">The room.</param>
    /// <param name="takeId">The photo to change.</param>
    /// <param name="change">The user's words.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The new photo, a failure the user can read, or <c>null</c> when the user has no such room or photo.</returns>
    public async Task<Result<PhotoTake>?> HandleAsync(Guid roomId, Guid takeId, string? change, CancellationToken cancellationToken)
    {
        var room = await rooms.FindAsync(roomId, currentUser.Id, cancellationToken);
        var take = room is null ? null : await rooms.FindTakeAsync(roomId, takeId, cancellationToken);
        var words = change?.Trim() ?? "";

        if (take is null)
        {
            return null;
        }

        if (words.Length is 0 or > Constants.Builder.MaxInstructionsLength)
        {
            return Result.Failure<PhotoTake>(Constants.Messages.ChangeWords);
        }

        var picture = await takes.ReadAsync(take, cancellationToken);
        var changed = await ai.Images(room!.AiMode).GenerateAsync(picture, null, Constants.Builder.Prompt.Change(words), null, null, null, cancellationToken);

        if (!changed.Succeeded)
        {
            return Result.Failure<PhotoTake>(changed.Error);
        }

        var stored = await takes.StoreAsync(room.Id, changed.Value, words, take.Id, cancellationToken);

        if (!stored.Succeeded)
        {
            return stored;
        }

        rooms.AddTake(stored.Value);
        await rooms.SaveChangesAsync(cancellationToken);

        return stored;
    }
}

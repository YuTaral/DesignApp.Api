using System.Buffers.Binary;
using DesignApp.Application.Helpers;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Application.UseCases;

/// <summary>
/// Turns a built room into one 360-degree photo from one spot: the browser's 360-degree 3D view and each object's box in
/// it go to the image model with the room in words (<see cref="PanoramaPrompt"/>). The photo is stored with the new room,
/// whose own photo is the view straight ahead cut out of it, so every view saved later shows exactly the same room. The
/// room goes into the plan's design, or a replaced room's place, as a Generate run's room does.
/// </summary>
/// <param name="plans">Where plans are stored.</param>
/// <param name="rooms">Where rooms and their photos are stored.</param>
/// <param name="currentUser">The signed-in user or guest.</param>
/// <param name="ai">The AI providers.</param>
/// <param name="imaging">Prepares the photo and cuts its first view.</param>
/// <param name="storage">Stores the photo.</param>
/// <param name="takes">Stores the first view.</param>
/// <param name="planPhotos">Checks the request, and puts the room into the plan's design or a replaced room's place.</param>
/// <param name="clock">When the room is made.</param>
public sealed class GeneratePlanPanorama(
    IPlanRepository plans, IRoomRepository rooms, ICurrentUser currentUser, IAiProviders ai, IPanoramaImaging imaging, IFileStorage storage, TakeStore takes,
    PlanPhotos planPhotos, TimeProvider clock)
{
    /// <summary>Generates the 360-degree photo and the room.</summary>
    /// <param name="planId">The plan.</param>
    /// <param name="guidePng">The room's 360-degree 3D view as the browser drew it (a PNG twice as wide as it is tall).</param>
    /// <param name="style">The interior style of the photo.</param>
    /// <param name="instructions">The user's own extra instructions for the AI, if any.</param>
    /// <param name="boxes">Where each object is in the 3D view; <c>null</c> to send none.</param>
    /// <param name="spot">Where the camera stands, in cm on the plan.</param>
    /// <param name="startYaw">Which way its views start, in degrees from its middle: towards most of the room.</param>
    /// <param name="replaces">An older room of the plan the new room replaces; <c>null</c> to add one.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The room (waiting for its analysis), a failure the user can read, or <c>null</c> when the user has no such plan.</returns>
    public async Task<Result<Room>?> HandleAsync(
        Guid planId, byte[] guidePng, PhotoStyle style, string? instructions, IReadOnlyList<ItemBox>? boxes, OutlinePoint spot, double startYaw,
        Guid? replaces, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(guidePng);
        ArgumentNullException.ThrowIfNull(spot);

        var plan = await plans.FindAsync(planId, currentUser.Id, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        var replaced = await planPhotos.ReplacedAsync(plan, replaces, cancellationToken);

        if (!replaced.Succeeded)
        {
            return Result.Failure<Room>(replaced.Error);
        }

        if (PlanPhotos.Problem(plan, guidePng, Constants.Panoramas.MaxGuideBytes, style, instructions) is { } problem)
        {
            return Result.Failure<Room>(problem);
        }

        if (!IsPanoramaPng(guidePng))
        {
            return Result.Failure<Room>(Constants.Messages.PanoramaGuide);
        }

        if (!PlanPhotos.FitAll(boxes, plan.Items.Count))
        {
            return Result.Failure<Room>(Constants.Messages.PhotoBoxes);
        }

        if (!plan.Outline.Contains(spot.X, spot.Y))
        {
            return Result.Failure<Room>(Constants.Messages.PanoramaSpot);
        }

        if (!double.IsFinite(startYaw) || Math.Abs(startYaw) > Rooms.MaxPanoramaStartYaw)
        {
            return Result.Failure<Room>(Constants.Messages.PanoramaStart);
        }

        var prompt = PanoramaPrompt.Write(plan, style, instructions);
        var layout = boxes?.Select(box => PlanPhotos.Layout(plan, box, null)).ToList();
        var size = new PhotoSize(Constants.Panoramas.Width, Constants.Panoramas.Height);
        var photo = await ai.Images(plan.AiMode).GenerateAsync(guidePng, null, prompt, null, layout, size, cancellationToken);

        if (!photo.Succeeded)
        {
            return Result.Failure<Room>(photo.Error);
        }

        var picture = imaging.Prepare(photo.Value, size.Width, size.Height);

        if (!picture.Succeeded)
        {
            return Result.Failure<Room>(picture.Error);
        }

        var now = clock.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        var key = Constants.Rooms.PanoramaKey(id);
        var front = imaging.Cut(picture.Value.Content, new PanoramaView(startYaw, Constants.Panoramas.FrontPitch, Constants.Panoramas.FrontFov),
            Constants.Panoramas.ViewWidth, Constants.Panoramas.ViewHeight);

        using (var content = new MemoryStream(picture.Value.Content, writable: false))
        {
            await storage.SaveAsync(key, content, Constants.ContentTypes.Jpeg, cancellationToken);
        }

        var stored = await takes.StoreAsync(id, front, Constants.Panoramas.ViewNote(startYaw), null, cancellationToken);

        if (!stored.Succeeded)
        {
            return Result.Failure<Room>(stored.Error);
        }

        await takes.UseAsRoomPhotoAsync(stored.Value, cancellationToken);
        var room = Room.FromPlan(id, currentUser.Id, Constants.Rooms.PhotoKey(id), stored.Value, now, plan.AiMode, plan.Id);

        room.UsePanorama(new Panorama(key, picture.Value.Width, picture.Value.Height, spot, startYaw));
        rooms.Add(room);
        rooms.AddTake(stored.Value);
        await rooms.SaveChangesAsync(cancellationToken);

        await planPhotos.PlaceAsync(plan, room, replaced.Value, cancellationToken);

        return room;
    }

    /// <summary>Whether an image is a PNG twice as wide as it is tall, from its header.</summary>
    /// <param name="image">The image.</param>
    private static bool IsPanoramaPng(byte[] image)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        if (image.Length < 24 || !image.AsSpan(0, 8).SequenceEqual(signature))
        {
            return false;
        }

        // The first chunk is IHDR: the width, then the height, as big-endian integers.
        var width = BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(20, 4));

        return height > 0 && width == height * 2 && width <= Constants.Panoramas.Width;
    }

}

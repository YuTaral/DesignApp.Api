using DesignApp.Application.Objects;

namespace DesignApp.Application.Interfaces;

/// <summary>Makes a realistic photo of a built room from its simple 3D view, keeping where everything stands.</summary>
public interface IImageGenerator
{
    /// <summary>Generates the photo, or edits one.</summary>
    /// <param name="image">The room's simple 3D view (PNG), the layout the photo keeps; or a photo to edit (JPEG or PNG).</param>
    /// <param name="reference">
    /// A photo of the same room from another view, whose objects the photo shows exactly as they look there (the first
    /// photo of a Generate run); <c>null</c> for none.
    /// </param>
    /// <param name="prompt">What the photo shows, written from the room's plan; or the edit to make.</param>
    /// <param name="seed">
    /// Makes the result repeatable: the same image, prompt and seed give the same photo (to compare changes fairly);
    /// <c>null</c> for a new random photo each time.
    /// </param>
    /// <param name="boxes">
    /// Objects to draw inside given boxes of the photo (the room's layout), each copied from its box in the reference when
    /// it has one there; <c>null</c> for none.
    /// </param>
    /// <param name="size">The photo's size; <c>null</c> for a room builder photo's (1024 x 768).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The photo (JPEG or PNG), or a failure the user can read.</returns>
    Task<Result<byte[]>> GenerateAsync(
        byte[] image, byte[]? reference, string prompt, int? seed, IReadOnlyList<LayoutBox>? boxes, PhotoSize? size, CancellationToken cancellationToken);
}

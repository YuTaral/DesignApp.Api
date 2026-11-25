using DesignApp.Application.Objects;

namespace DesignApp.Application.Interfaces;

/// <summary>Brings the colors of a photo made from a built room back to the room as planned.</summary>
public interface IPhotoColors
{
    /// <summary>
    /// Moves each region of a generated photo (an object, or one planned color of the floor, walls or ceiling) from its
    /// typical color to its typical color in the 3D view the photo was made from, keeping the photo's texture, light
    /// and shade. The image model darkens light colors, mutes strong ones and swaps planned colors for natural ones
    /// (a mustard table for plain wood); this undoes that.
    /// </summary>
    /// <param name="photo">The generated photo (JPEG or PNG).</param>
    /// <param name="guidePng">The 3D view it was made from.</param>
    /// <param name="masksPng">
    /// Which region each pixel of the 3D view shows, in its red channel: 1 + an object's position in the plan, or a
    /// surface (see <see cref="Constants.Builder.MaskIds"/>); 0 for none.
    /// </param>
    /// <param name="regions">Which regions to correct.</param>
    /// <returns>The corrected photo (JPEG), or the photo itself when nothing needs correcting or a picture can't be read.</returns>
    byte[] Correct(byte[] photo, byte[] guidePng, byte[] masksPng, PhotoRegions regions);
}

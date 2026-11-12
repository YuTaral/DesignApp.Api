namespace DesignApp.Application.Objects;

/// <summary>A room after an object was removed from its photo: the files of the new version.</summary>
/// <param name="PhotoJpeg">The new photo, a JPEG the size of the stored photo.</param>
/// <param name="LabelMapPng">The new label map, where the object's pixels went to the objects around it.</param>
public sealed record RemovedObject(byte[] PhotoJpeg, byte[] LabelMapPng);

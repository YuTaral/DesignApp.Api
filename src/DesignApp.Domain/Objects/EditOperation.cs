namespace DesignApp.Domain.Objects;

/// <summary>
/// One non-destructive change to an object in the room photo. Designs store these instead of
/// edited pixels, so later operations (resize, remove) slot in as new subtypes.
/// </summary>
/// <param name="ObjectIndex">The <see cref="DetectedObject.Index"/> of the object to change.</param>
public abstract record EditOperation(int ObjectIndex);

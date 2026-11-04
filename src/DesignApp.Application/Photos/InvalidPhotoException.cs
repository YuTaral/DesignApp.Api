namespace DesignApp.Application.Photos;

/// <summary>An upload that cannot be used. The message is safe to show to the user.</summary>
public sealed class InvalidPhotoException(string message) : Exception(message);

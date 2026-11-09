namespace DesignApp.Domain.Common;

/// <summary>Thrown when an operation would put an entity into an invalid state.</summary>
/// <param name="message">Which rule was broken, in words safe to show the user.</param>
public sealed class DomainException(string message) : Exception(message);

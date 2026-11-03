namespace DesignApp.Domain.Common;

/// <summary>Thrown when an operation would put an entity into an invalid state.</summary>
public sealed class DomainException(string message) : Exception(message);

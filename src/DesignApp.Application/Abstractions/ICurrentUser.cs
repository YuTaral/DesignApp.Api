namespace DesignApp.Application.Abstractions;

/// <summary>Who is making the request. Every query is scoped to this id so users only see their own data.</summary>
public interface ICurrentUser
{
    /// <summary>The user's id, stored as the owner of everything they create.</summary>
    string Id { get; }
}

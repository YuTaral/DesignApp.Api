namespace DesignApp.Application.Abstractions;

/// <summary>Who is making the request. Every query is scoped to this id so users only see their own data.</summary>
public interface ICurrentUser
{
    /// <summary>
    /// The user's id, stored as the owner of everything they create: the account's id when signed
    /// in, else this browser's guest id (see <see cref="GuestIds"/>).
    /// </summary>
    string Id { get; }

    /// <summary>Whether the request comes from a signed-in account rather than a guest.</summary>
    bool IsSignedIn { get; }
}

using DesignApp.Application.Interfaces;


namespace DesignApp.UnitTests;

/// <summary>A current user with a fixed id.</summary>
/// <param name="id">The user's id.</param>
/// <param name="signedIn">Whether it is an account rather than a guest.</param>
internal sealed class FixedUser(string id, bool signedIn = true) : ICurrentUser
{
    /// <inheritdoc/>
    public string Id => id;

    /// <inheritdoc/>
    public bool IsSignedIn => signedIn;
}

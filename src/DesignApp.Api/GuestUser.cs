using DesignApp.Application.Abstractions;

namespace DesignApp.Api;

/// <summary>Until sign-in arrives (milestone 5), every visitor works as one shared guest.</summary>
internal sealed class GuestUser : ICurrentUser
{
    /// <summary>The shared id that all guest data is stored under.</summary>
    public string Id => "guest";
}

using DesignApp.Application.Abstractions;

namespace DesignApp.Api;

/// <summary>Until sign-in arrives (milestone 5), every visitor works as one shared guest.</summary>
internal sealed class GuestUser : ICurrentUser
{
    public string Id => "guest";
}

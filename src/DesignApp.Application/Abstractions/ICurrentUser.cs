namespace DesignApp.Application.Abstractions;

/// <summary>Who is making the request. Every query is scoped to this id so users only see their own data.</summary>
public interface ICurrentUser
{
    string Id { get; }
}

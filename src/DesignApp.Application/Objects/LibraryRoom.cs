namespace DesignApp.Application.Objects;

/// <summary>One room of a built-in library design, with one or more layouts to choose from.</summary>
/// <param name="Id">The room's id inside its design, such as <c>kitchen</c>.</param>
/// <param name="Title">The name people see, such as "Kitchen".</param>
/// <param name="Layouts">Its layouts; the first is the one a whole-house copy uses.</param>
public sealed record LibraryRoom(string Id, string Title, IReadOnlyList<LibraryLayout> Layouts)
{
    /// <summary>The layout with this id, or <c>null</c>.</summary>
    /// <param name="layoutId">The layout's id inside the room.</param>
    public LibraryLayout? FindLayout(string layoutId)
    {
        return Layouts.FirstOrDefault(l => l.Id == layoutId);
    }
}

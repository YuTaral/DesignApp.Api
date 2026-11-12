namespace DesignApp.Infrastructure.Library;

/// <summary>
/// One room in a <c>design.json</c>. Each layout's files are in the room's folder: <c>&lt;layout&gt;.jpg</c>,
/// <c>&lt;layout&gt;.labels.png</c> and <c>&lt;layout&gt;.objects.json</c>.
/// </summary>
/// <param name="Id">The room's id inside the design, such as <c>kitchen</c>; also its folder's name.</param>
/// <param name="Title">The name people see, such as "Kitchen".</param>
/// <param name="Layouts">The layouts' ids, such as <c>1</c>, <c>2</c>, <c>3</c>; the first is the default.</param>
internal sealed record LibraryRoomEntry(string Id, string Title, IReadOnlyList<string> Layouts);

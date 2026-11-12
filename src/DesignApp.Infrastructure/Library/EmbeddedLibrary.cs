using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Photos;

namespace DesignApp.Infrastructure.Library;

/// <summary>
/// The built-in library, read from the files embedded in this assembly (<c>Library/&lt;design&gt;/</c>): each design's
/// <c>design.json</c>, and per layout of a room (<c>&lt;room&gt;/&lt;layout&gt;</c>) a photo, a label map and an objects
/// file. The lists are read once; photos and label maps are opened from the assembly each time.
/// </summary>
internal sealed class EmbeddedLibrary : ILibrary
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }, // categories by name, so the files read well
    };

    private readonly Assembly _assembly;
    // Resource names by their path inside the library, with forward slashes ("minimalist/kitchen/2.jpg").
    private readonly Dictionary<string, string> _resources;

    /// <summary>Reads the library embedded in this assembly.</summary>
    public EmbeddedLibrary() : this(typeof(EmbeddedLibrary).Assembly)
    {
    }

    /// <summary>Reads the library embedded in an assembly.</summary>
    /// <param name="assembly">The assembly holding the files.</param>
    /// <exception cref="InvalidDataException">A design's files are missing or don't match its <c>design.json</c>.</exception>
    internal EmbeddedLibrary(Assembly assembly)
    {
        _assembly = assembly;
        _resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Constants.Library.ResourcePrefix, StringComparison.Ordinal))
            .ToDictionary(n => n[Constants.Library.ResourcePrefix.Length..].Replace('\\', '/'), n => n, StringComparer.Ordinal);
        Designs = [.. _resources.Keys
            .Where(path => path.EndsWith('/' + Constants.Library.DesignFile, StringComparison.Ordinal))
            .Select(ReadDesign)
            .OrderBy(d => d.Order)
            .ThenBy(d => d.Design.Id, StringComparer.Ordinal)
            .Select(d => d.Design)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<LibraryDesign> Designs { get; }

    /// <summary>Every library file's path inside the library, such as <c>minimalist/kitchen/2.jpg</c>.</summary>
    internal IReadOnlyCollection<string> Files => _resources.Keys;

    /// <inheritdoc/>
    public LibraryDesign? FindDesign(string designId)
    {
        return Designs.FirstOrDefault(d => d.Id == designId);
    }

    /// <inheritdoc/>
    public Stream OpenPhoto(string designId, string roomId, string layoutId)
    {
        return Open(LayoutFile(designId, roomId, layoutId, Constants.Library.PhotoSuffix));
    }

    /// <inheritdoc/>
    public Stream OpenLabelMap(string designId, string roomId, string layoutId)
    {
        return Open(LayoutFile(designId, roomId, layoutId, Constants.Library.LabelMapSuffix));
    }

    /// <inheritdoc/>
    public byte[] Pack(LibraryExport room)
    {
        ArgumentNullException.ThrowIfNull(room);
        // Removed objects stay listed on the room (for undo) but have no pixels left: a library layout leaves them out.
        var (labels, _, _) = SkiaRoomImaging.DecodeGrayPng(room.LabelMap);
        var present = labels.Where(l => l > 0).ToHashSet();
        var objects = room.Objects.Where(o => present.Contains((byte)o.Index)).OrderBy(o => o.Index).Select(LibraryObjectFile.From).ToList();
        var file = new LibraryRoomFile(room.Width, room.Height, objects);

        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, room.RoomId + Constants.Library.PhotoSuffix, room.Photo);
            Add(archive, room.RoomId + Constants.Library.LabelMapSuffix, room.LabelMap);
            Add(archive, room.RoomId + Constants.Library.ObjectsSuffix, JsonSerializer.SerializeToUtf8Bytes(file, Options));
        }
        return zip.ToArray();
    }

    /// <summary>Reads one design's <c>design.json</c> and the objects files of its rooms' layouts.</summary>
    /// <param name="path">The design file's path, such as <c>minimalist/design.json</c>.</param>
    /// <returns>The design and where it is listed.</returns>
    private (LibraryDesign Design, int Order) ReadDesign(string path)
    {
        var folder = path[..path.IndexOf('/', StringComparison.Ordinal)];
        var file = Read<LibraryDesignFile>(path);
        if (file.Id != folder)
            throw new InvalidDataException($"Library design {path} has the id '{file.Id}'; it must match its folder '{folder}'.");
        if (file.Rooms.Count == 0)
            throw new InvalidDataException($"Library design {path} has no rooms.");

        var rooms = file.Rooms.Select(entry =>
        {
            if (entry.Layouts.Count == 0 || entry.Layouts.Distinct(StringComparer.Ordinal).Count() != entry.Layouts.Count)
                throw new InvalidDataException($"Library room {file.Id}/{entry.Id} needs one or more layouts, each listed once.");
            return new LibraryRoom(entry.Id, entry.Title, [.. entry.Layouts.Select(layout => ReadLayout(file.Id, entry.Id, layout))]);
        }).ToList();
        if (rooms.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != rooms.Count)
            throw new InvalidDataException($"Library design {path} lists a room twice.");

        return (new LibraryDesign(file.Id, file.Name, file.Style, file.Description, rooms), file.Order);
    }

    /// <summary>Reads one layout's objects file, checking that its photo and label map are there too.</summary>
    /// <param name="designId">The design's id.</param>
    /// <param name="roomId">The room's id.</param>
    /// <param name="layoutId">The layout's id.</param>
    private LibraryLayout ReadLayout(string designId, string roomId, string layoutId)
    {
        var name = $"{designId}/{roomId}/{layoutId}";
        foreach (var suffix in new[] { Constants.Library.PhotoSuffix, Constants.Library.LabelMapSuffix })
        {
            if (!_resources.ContainsKey(LayoutFile(designId, roomId, layoutId, suffix)))
                throw new InvalidDataException($"Library layout {name} has no {suffix} file.");
        }
        var layout = Read<LibraryRoomFile>(LayoutFile(designId, roomId, layoutId, Constants.Library.ObjectsSuffix));
        if (layout.Width <= 0 || layout.Height <= 0 || layout.Objects.Count == 0)
            throw new InvalidDataException($"Library layout {name} needs a size and at least one object.");
        return new LibraryLayout(layoutId, layout.Width, layout.Height, [.. layout.Objects.Select(o => o.ToDomain()).OrderBy(o => o.Index)]);
    }

    /// <summary>Reads and parses one JSON file of the library.</summary>
    /// <typeparam name="T">The file's shape.</typeparam>
    /// <param name="path">Its path inside the library.</param>
    private T Read<T>(string path)
    {
        using var stream = Open(path);
        return JsonSerializer.Deserialize<T>(stream, Options) ?? throw new InvalidDataException($"Library file {path} is empty.");
    }

    /// <summary>Opens one library file.</summary>
    /// <param name="path">Its path inside the library.</param>
    /// <exception cref="KeyNotFoundException">There is no such file.</exception>
    private Stream Open(string path)
    {
        if (!_resources.TryGetValue(path, out var name))
            throw new KeyNotFoundException($"The library has no file {path}.");
        return _assembly.GetManifestResourceStream(name) ?? throw new KeyNotFoundException($"The library has no file {path}.");
    }

    /// <summary>The path of one of a layout's files.</summary>
    /// <param name="designId">The design's id.</param>
    /// <param name="roomId">The room's id.</param>
    /// <param name="layoutId">The layout's id.</param>
    /// <param name="suffix">Which file, such as <see cref="Constants.Library.PhotoSuffix"/>.</param>
    private static string LayoutFile(string designId, string roomId, string layoutId, string suffix)
    {
        return $"{designId}/{roomId}/{layoutId}{suffix}";
    }

    /// <summary>Adds one file to the zip.</summary>
    /// <param name="archive">The zip.</param>
    /// <param name="name">The file's name.</param>
    /// <param name="content">Its bytes.</param>
    private static void Add(ZipArchive archive, string name, byte[] content)
    {
        using var entry = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
        entry.Write(content);
    }
}

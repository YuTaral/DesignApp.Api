using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Builder;

/// <summary>
/// The room builder's common objects, read once from the embedded <c>Builder/catalog.json</c>: for each room type,
/// its most common objects with their usual sizes (centimetres) and colors, after the windows and doors every room has.
/// Each object comes in variations (shapes and sizes); the first one is the object as it is first offered.
/// </summary>
internal sealed class EmbeddedObjectCatalog : IObjectCatalog
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly Dictionary<RoomType, IReadOnlyList<CatalogObject>> _byRoom;

    /// <summary>Reads the catalog from this assembly.</summary>
    /// <exception cref="InvalidDataException">The catalog is missing, malformed, or misses a room type.</exception>
    public EmbeddedObjectCatalog()
    {
        var assembly = typeof(EmbeddedObjectCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(Constants.Builder.CatalogResource)
            ?? throw new InvalidDataException($"The resource {Constants.Builder.CatalogResource} is missing.");
        var file = JsonSerializer.Deserialize<CatalogFile>(stream, Options)
            ?? throw new InvalidDataException("The object catalog is empty.");

        _byRoom = Enum.GetValues<RoomType>().ToDictionary(
            type => type,
            type => (IReadOnlyList<CatalogObject>)file.EveryRoom
                .Concat(file.Rooms.TryGetValue(type, out var objects) ? objects : throw new InvalidDataException($"The catalog has no {type}."))
                .Select(ToObject)
                .ToList()
                .AsReadOnly());
    }

    /// <inheritdoc/>
    public IReadOnlyList<CatalogObject> ForRoom(RoomType roomType)
    {
        return _byRoom.GetValueOrDefault(roomType) ?? [];
    }

    /// <summary>An object of the file with its variations, the first being the object itself.</summary>
    /// <param name="o">The object as the file stores it.</param>
    /// <exception cref="InvalidDataException">The object has no variations.</exception>
    private static CatalogObject ToObject(CatalogFileObject o)
    {
        var variants = o.Variants
            .Select(v => new CatalogVariant(v.Key, v.Name, v.Label, v.Shape ?? o.Shape, v.Width, v.Depth, v.Height, v.Elevation, v.Form))
            .ToList()
            .AsReadOnly();
        var first = variants.FirstOrDefault() ?? throw new InvalidDataException($"The catalog's {o.Key} has no variations.");

        return new CatalogObject(o.Key, first.Label, first.Shape, first.Width, first.Depth, first.Height, HexColor.Parse(o.Color), first.Elevation, first.Form, variants,
            (o.Decorations ?? []).AsReadOnly());
    }

    /// <summary>The file: the objects every room has (windows and doors), and each room type's own.</summary>
    /// <param name="EveryRoom">The windows and doors.</param>
    /// <param name="Rooms">The objects of each room type.</param>
    private sealed record CatalogFile(List<CatalogFileObject> EveryRoom, Dictionary<RoomType, List<CatalogFileObject>> Rooms);

    /// <summary>One object as the file stores it.</summary>
    /// <param name="Key">Its id within the room type.</param>
    /// <param name="Shape">How it is drawn (unless a variation says otherwise).</param>
    /// <param name="Color">Its color, #RRGGBB.</param>
    /// <param name="Variants">Its variations, the first being the object as first offered.</param>
    /// <param name="Decorations">The most popular small things to put on it, if any.</param>
    private sealed record CatalogFileObject(string Key, ItemShape Shape, string Color, List<CatalogFileVariant> Variants, List<string>? Decorations = null);

    /// <summary>One variation of an object as the file stores it.</summary>
    /// <param name="Key">Its id within the object.</param>
    /// <param name="Name">What the choice is called.</param>
    /// <param name="Label">The object's name in this variation.</param>
    /// <param name="Width">Its width, in centimetres.</param>
    /// <param name="Depth">Its depth, in centimetres.</param>
    /// <param name="Height">Its height, in centimetres.</param>
    /// <param name="Shape">How it is drawn, when not as the object.</param>
    /// <param name="Elevation">How high its bottom is above the floor, in centimetres (windows).</param>
    /// <param name="Form">Its outline from above.</param>
    private sealed record CatalogFileVariant(string Key, string Name, string Label, double Width, double Depth, double Height, ItemShape? Shape = null,
        double Elevation = 0, ItemForm Form = ItemForm.Standard);
}

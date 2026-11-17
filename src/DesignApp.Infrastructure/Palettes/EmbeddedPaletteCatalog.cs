using System.Reflection;
using System.Text.Json;
using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;

namespace DesignApp.Infrastructure.Palettes;

/// <summary>
/// The paint palettes embedded in this assembly, one JSON file each (<c>Palettes/&lt;id&gt;.json</c>), read once.
/// Adding a palette (a paint brand, say) is adding a file.
/// </summary>
internal sealed class EmbeddedPaletteCatalog : IPaletteCatalog
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    // Paints by their reference ("ral-classic/9010").
    private readonly Dictionary<string, Paint> _paints;

    /// <summary>Reads the palettes embedded in this assembly.</summary>
    public EmbeddedPaletteCatalog() : this(typeof(EmbeddedPaletteCatalog).Assembly)
    {
    }

    /// <summary>Reads the palettes embedded in an assembly.</summary>
    /// <param name="assembly">The assembly holding the files.</param>
    /// <exception cref="InvalidDataException">A palette file is malformed, or two palettes or paints share an id.</exception>
    internal EmbeddedPaletteCatalog(Assembly assembly)
    {
        Palettes = [.. assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Constants.Palettes.ResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(n => Read(assembly, n))];

        if (Palettes.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1) is { } repeated)
        {
            throw new InvalidDataException($"Two palettes have the id '{repeated.Key}'.");
        }

        _paints = Palettes
            .SelectMany(palette => palette.Paints.Select(paint => (Reference: palette.ReferenceTo(paint), Paint: paint)))
            .ToDictionary(p => p.Reference, p => p.Paint, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public IReadOnlyList<Palette> Palettes { get; }

    /// <inheritdoc/>
    public Paint? Find(string paintReference)
    {
        return _paints.GetValueOrDefault(paintReference);
    }

    /// <summary>Reads one palette file.</summary>
    /// <param name="assembly">The assembly holding it.</param>
    /// <param name="resource">Its resource name.</param>
    /// <exception cref="InvalidDataException">The file is malformed, or two of its paints share an id.</exception>
    private static Palette Read(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;
        var file = JsonSerializer.Deserialize<PaletteFile>(stream, Options);

        if (file is null || string.IsNullOrWhiteSpace(file.Id) || file.Paints is null)
        {
            throw new InvalidDataException($"The palette file {resource} is malformed.");
        }

        var paints = file.Paints.Select(p => new Paint(p.Id, p.Code, p.Name, HexColor.Parse(p.Hex))).ToList();

        if (paints.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1) is { } repeated)
        {
            throw new InvalidDataException($"The palette {file.Id} has two paints with the id '{repeated.Key}'.");
        }

        return new Palette(file.Id, file.Name, file.Notice ?? "", paints.AsReadOnly());
    }

    /// <summary>A palette file.</summary>
    /// <param name="Id">The palette's id.</param>
    /// <param name="Name">Its name.</param>
    /// <param name="Notice">Where its colors come from.</param>
    /// <param name="Paints">Its paints.</param>
    private sealed record PaletteFile(string Id, string Name, string? Notice, List<PaintFile>? Paints);

    /// <summary>One paint of a palette file.</summary>
    /// <param name="Id">The paint's id.</param>
    /// <param name="Code">Its code.</param>
    /// <param name="Name">Its name.</param>
    /// <param name="Hex">Its color as #RRGGBB.</param>
    private sealed record PaintFile(string Id, string Code, string Name, string Hex);
}

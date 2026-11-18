using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;
using Microsoft.Extensions.Options;

using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Ai.Fakes;

/// <summary>
/// The room builder's free stand-in for Claude Haiku: a described object gets its shape from words such as "table" or
/// "round", its width from a number with "cm", and its color from a color word.
/// </summary>
/// <param name="options">The fake step delay.</param>
internal sealed class FakeRoomBuilderAi(IOptions<AiOptions> options) : IRoomBuilderAi
{
    /// <summary>Shapes by a word in the description, the first match wins.</summary>
    private static readonly (string Word, ItemShape Shape, double Width, double Depth, double Height)[] Shapes =
    [
        ("sofa", ItemShape.Sofa, 200, 90, 85), ("couch", ItemShape.Sofa, 200, 90, 85), ("armchair", ItemShape.Sofa, 85, 85, 85),
        ("bed", ItemShape.Bed, 160, 210, 100), ("table", ItemShape.Table, 120, 70, 75), ("desk", ItemShape.Table, 120, 60, 75),
        ("chair", ItemShape.Chair, 45, 50, 85), ("lamp", ItemShape.Lamp, 40, 40, 150), ("plant", ItemShape.Plant, 45, 45, 100),
        ("rug", ItemShape.Rug, 160, 230, 1), ("carpet", ItemShape.Rug, 160, 230, 1), ("round", ItemShape.Cylinder, 60, 60, 60),
        ("pouf", ItemShape.Rounded, 50, 50, 40), ("window", ItemShape.Window, 120, 10, 140), ("door", ItemShape.Door, 90, 5, 210),
        ("painting", ItemShape.Picture, 80, 3, 60), ("mirror", ItemShape.Picture, 60, 3, 90), ("wall shelf", ItemShape.WallBox, 80, 25, 4),
    ];

    /// <summary>Colors by a word in the description.</summary>
    private static readonly (string Word, string Color)[] Colors =
    [
        ("oak", "#B8864B"), ("walnut", "#6B4A2E"), ("white", "#F2F0EB"), ("black", "#2A2A2A"), ("grey", "#8A8580"),
        ("gray", "#8A8580"), ("blue", "#4A6274"), ("green", "#4F6B3A"), ("red", "#B5543C"), ("yellow", "#D9C27A"),
    ];

    /// <inheritdoc/>
    public async Task<Result<CatalogObject>> DescribeObjectAsync(RoomType roomType, string description, CancellationToken cancellationToken)
    {
        await Task.Delay(options.Value.FakeStepDelay, cancellationToken);
        var words = description.ToLowerInvariant();
        var shape = Shapes.FirstOrDefault(s => words.Contains(s.Word, StringComparison.Ordinal));
        var (kind, width, depth, height) = shape.Word is null ? (ItemShape.Box, 80, 50, 80) : (shape.Shape, shape.Width, shape.Depth, shape.Height);
        var sized = System.Text.RegularExpressions.Regex.Match(words, @"(\d{2,4})\s*cm");

        if (sized.Success)
        {
            var scale = double.Parse(sized.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) / width;

            (width, depth) = (width * scale, kind is ItemShape.Cylinder ? width * scale : depth);
        }

        var color = Colors.FirstOrDefault(c => words.Contains(c.Word, StringComparison.Ordinal)).Color ?? "#A0A0A0";
        var label = description.Trim();

        label = label.Length <= Domain.Constants.Plans.MaxLabelLength ? label : label[..Domain.Constants.Plans.MaxLabelLength];

        return new CatalogObject(Constants.Ai.RoomBuilder.CustomKey, label, kind, Math.Round(width), Math.Round(depth), height, HexColor.Parse(color), kind.UsualElevation());
    }

    /// <summary>
    /// The same plan for every photo: a 4 × 5 m living room with a window, a door, a sofa facing a TV stand with the TV
    /// above it, a coffee table, a rug and an armchair. Placed roughly, as a real answer is: the armchair overlaps the
    /// sofa, the TV stand stands a little off the wall and the TV crosses the window, for the builder to tidy.
    /// </summary>
    /// <param name="image">The photo (not looked at).</param>
    /// <param name="catalog">The objects it may use; those missing from it are left out.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task<Result<PhotoPlan>> PlanFromPhotoAsync(AnalysisImage image, IReadOnlyList<CatalogObject> catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        await Task.Delay(options.Value.FakeStepDelay, cancellationToken);
        var keys = catalog.Select(o => o.Key).ToHashSet();
        var objects = FakePlan.Where(o => keys.Contains(o.Key)).ToList();

        return new PhotoPlan(RoomType.LivingRoom, 4, 5, 2.6, "#E8E2D6", WallFinish.Paint, "#B89B7A", FloorType.Wood, CeilingLight.Pendant, new OutlinePoint(200, 470), objects);
    }

    /// <summary>The fake photo's objects, as <see cref="PlanFromPhotoAsync"/> describes them.</summary>
    private static readonly PhotoPlanItem[] FakePlan =
    [
        new("window", "standard", 0, 0, 0, 120, 10, 140, "#F4F1EA", "uPVC", PlanWall.Back, 300, 90),
        new("door", "standard", 0, 0, 0, 90, 5, 210, "#F4F1EA", "painted wood", PlanWall.Left, 60, 0),
        new("tv-stand", "standard", 200, 40, 0, 160, 40, 50, "#3A3633", "oak veneer", null, 0, 0),
        new("wall-tv", "standard", 0, 0, 0, 123, 5, 71, "#2A2A2A", "", PlanWall.Back, 230, 100),
        new("sofa", "standard", 200, 400, 180, 220, 95, 85, "#8A8580", "velvet", null, 0, 0),
        new("armchair", "standard", 300, 380, 180, 85, 85, 85, "#A8B89A", "linen", null, 0, 0),
        new("coffee-table", "standard", 200, 260, 0, 110, 60, 42, "#B8864B", "oak", null, 0, 0),
        new("rug", "standard", 200, 280, 0, 200, 300, 1, "#D8C3A5", "wool", null, 0, 0),
    ];
}

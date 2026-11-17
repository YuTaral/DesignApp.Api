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
}

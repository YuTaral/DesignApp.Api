using System.Globalization;
using System.Text;
using DesignApp.Domain.Entities;
using DesignApp.Domain.Extensions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

using P = DesignApp.Application.Constants.Panoramas.Prompt;

namespace DesignApp.Application.Helpers;

/// <summary>
/// The image model's instruction for a room's 360-degree photo: keep the panorama's projection, turn each plain shape
/// into its real object, add nothing; then the room's size and surfaces (as <see cref="PlanPrompt"/> words them), and
/// every object with its color, material, width and decorations. Where each object goes is the boxes' job.
/// </summary>
public static class PanoramaPrompt
{
    /// <summary>The instruction for a plan.</summary>
    /// <param name="plan">The room.</param>
    /// <param name="style">The interior style of the photo.</param>
    /// <param name="instructions">The user's own extra instructions, if any.</param>
    /// <returns>The prompt.</returns>
    public static string Write(RoomPlan plan, PhotoStyle style = PhotoStyle.AsBuilt, string? instructions = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder(P.Intro(PlanPrompt.Words(plan.RoomType.ToString())));
        var objects = plan.Items.Where(i => !i.Shape.IsZone()).Select((item, n) => $"{n + 1}. {PlanPrompt.Describe(item, P.Wide(PlanPrompt.Cm(item.Width)))}");
        var areas = plan.Items.Where(i => i.Shape.IsZone()).Select(Area).ToList();

        // The style and the user's own words come early, as in the photo prompt: at the end the model ignored them.
        if (Constants.Builder.Prompt.Style(style) is { Length: > 0 } styled)
        {
            text.Append(culture, $" {styled}");
        }

        if (instructions?.Trim().TrimEnd('.') is { Length: > 0 } extra)
        {
            text.Append(culture, $" {Constants.Builder.Prompt.Extra(extra)}");
        }

        text.Append(culture, $" {P.Room(PlanPrompt.Cm(plan.Width * 100), PlanPrompt.Cm(plan.Length * 100), PlanPrompt.Cm(plan.Height * 100))}");
        text.Append(culture, $" {PlanPrompt.Surfaces(plan, _ => P.AccentWall)} {Constants.Builder.Prompt.Mood(plan.Finish.TimeOfDay)}");
        text.Append(culture, $" {P.Contents} {P.Objects(string.Join("; ", objects))}");

        if (areas.Count > 0)
        {
            text.Append(culture, $" {P.Areas(string.Join("; ", areas))}");
        }

        return text.ToString();
    }

    /// <summary>An area of the floor or a wall in its own material, in words: what it is, its material and color, and its size.</summary>
    /// <param name="zone">The area.</param>
    private static string Area(PlanItem zone)
    {
        return zone.Shape is ItemShape.FloorZone
            ? P.FloorArea(Constants.Builder.Prompt.FloorWords(Enum.Parse<FloorType>(zone.Material)), PlanPrompt.Color(zone.Color), PlanPrompt.Cm(zone.Width), PlanPrompt.Cm(zone.Depth))
            : P.WallArea(PlanPrompt.Look(Enum.Parse<WallFinish>(zone.Material), zone.Color), PlanPrompt.Cm(zone.Width), PlanPrompt.Cm(zone.Height));
    }
}

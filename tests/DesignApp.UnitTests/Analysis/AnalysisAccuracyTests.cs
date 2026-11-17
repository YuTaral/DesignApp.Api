using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Claude;
using DesignApp.Infrastructure.Ai.Fal;
using DesignApp.Infrastructure.Photos;
using AiDefaults = DesignApp.Infrastructure.Constants.Ai.Defaults;

namespace DesignApp.UnitTests.Analysis;

/// <summary>
/// The three fixes for photos the AI outlined badly (a portrait living room): Claude answers in the
/// photo's pixels, a mask must fill its box, and gaps left after growing go to the matching neighbor.
/// </summary>
public class AnalysisAccuracyTests
{
    [Fact]
    public void Claude_s_pixel_boxes_and_points_become_fractions_of_the_photo()
    {
        var result = InventoryJson.Parse("""
            {"isRoom": true, "objects": [{"label": "wall mounted tv", "category": "Furniture",
              "box": {"x": 688, "y": 985, "width": 353, "height": 266}, "color": "#111111", "points": []},
              {"label": "back wall", "category": "Wall", "box": {"x": 0, "y": 600, "width": 1536, "height": 1200},
               "color": "#BBBBBB", "points": [{"x": 768, "y": 1024}]}]}
            """, maxPoints: 3, width: 1536, height: 2048);

        var tv = result.Value[0];

        Assert.Equal(688 / 1536.0, tv.Box.X, 6);
        Assert.Equal(985 / 2048.0, tv.Box.Y, 6);
        Assert.Equal(266 / 2048.0, tv.Box.Height, 6);
        Assert.Equal(new ImagePoint(0.5, 0.5), Assert.Single(result.Value[1].Points!));
    }

    [Fact]
    public void Claude_is_told_the_photo_s_size_and_to_list_wall_panels()
    {
        var prompt = new AiSettings().SystemPrompt();

        Assert.Contains("in the photo's pixels", prompt, StringComparison.Ordinal);
        Assert.Contains("wood slats", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("fractions of the image", prompt, StringComparison.Ordinal);
        Assert.Contains("1536 x 2048 pixels", DesignApp.Infrastructure.Constants.Ai.Claude.ImageSize(1536, 2048), StringComparison.Ordinal);
    }

    [Fact]
    public void A_mask_that_only_frames_its_box_loses_to_one_that_fills_it()
    {
        var box = new BoundingBox(0.2, 0.2, 0.6, 0.6);
        var frame = Mask(100, 100, (x, y) => x is >= 20 and < 80 && y is >= 20 and < 80 && (x == 20 || x == 79 || y == 20 || y == 79));
        var filled = Mask(100, 100, (x, y) => x is >= 22 and < 78 && y is >= 22 and < 78);

        var frameAlone = FalSegmentationProvider.Pick([frame], box);
        var picked = FalSegmentationProvider.Pick([frame, filled], box);

        Assert.True(frameAlone!.Value.Fit < AiDefaults.MinFit, $"a hollow frame scored {frameAlone.Value.Fit:0.00}");
        // The filled mask wins (or the frame and the filled mask joined, which fills the box even better).
        Assert.True(FalSegmentationProvider.Fill(picked!.Value.Mask, box) >= FalSegmentationProvider.Fill(filled, box));
        Assert.True(picked.Value.Fit >= AiDefaults.MinFit);
    }

    [Fact]
    public void Thin_objects_that_fill_enough_of_their_box_still_pass()
    {
        // A lamp's cage fills about a third of its box: above the minimum fill, so its fit stands.
        var box = new BoundingBox(0.2, 0.2, 0.6, 0.6);
        var cage = Mask(100, 100, (x, y) => x is >= 20 and < 80 && y is >= 20 and < 80 && (x + y) % 3 == 0);

        Assert.InRange(FalSegmentationProvider.Fill(cage, box), 0.3, 0.4);
        Assert.True(FalSegmentationProvider.Pick([cage], box)!.Value.Fit >= AiDefaults.MinFit);
    }

    [Fact]
    public void A_gap_goes_to_the_neighbor_with_the_closest_color_and_an_unlike_one_stays_empty()
    {
        // Left half grey (object 1), right half dark (object 2); two gaps: a grey one walled off from
        // object 1 by a black stripe, and a bright red one. The grey gap joins object 1; red stays empty.
        const int width = 60, height = 20;
        var rgba = new byte[width * height * 4];
        var labels = new byte[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = y * width + x;
                (byte r, byte g, byte b) color = x < 30 ? ((byte)180, (byte)180, (byte)180) : ((byte)40, (byte)40, (byte)40);

                if (x is >= 10 and < 12)
                {
                    color = (0, 0, 0); // an edge no step crosses
                }

                if (x < 10)
                {
                    color = (182, 178, 181); // the grey gap, behind it
                }

                if (x is >= 40 and < 50 && y is >= 5 and < 15)
                {
                    color = (230, 20, 20); // the red gap
                }

                (rgba[p * 4], rgba[p * 4 + 1], rgba[p * 4 + 2], rgba[p * 4 + 3]) = (color.r, color.g, color.b, 255);
                labels[p] = x < 10 || x is >= 40 and < 50 && y is >= 5 and < 15 ? (byte)0 : x < 30 ? (byte)1 : (byte)2;
            }
        }

        for (var y = 0; y < height; y++)
        {
            labels[y * width + 10] = labels[y * width + 11] = 1; // the black edge belongs to object 1
        }

        LabelMapRefiner.FillRemainingGaps(labels, width, height, rgba);

        Assert.Equal(1, labels[10 * width + 3]);   // grey gap: closest to object 1's mean color
        Assert.Equal(0, labels[10 * width + 45]);  // red gap: unlike both neighbors, left empty
    }

    [Fact]
    public void A_small_object_does_not_spread_past_its_reach_into_a_gap_of_its_color()
    {
        // One white photo: a small "teapot" (object 1) at x 10-14 and a white "wall" (object 2) at x 40+.
        // The gap left of the teapot is reachable only through it. Without a reach the teapot takes all of
        // it; with its reach (x 5-19) it stops at x 5 and never passes x 19, where the wall takes over.
        const int width = 60, height = 10;
        var rgba = Enumerable.Repeat((byte)240, width * height * 4).ToArray();

        byte[] Labels()
        {
            var labels = new byte[width * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 10; x < 15; x++)
                {
                    labels[y * width + x] = 1;
                }

                for (var x = 40; x < width; x++)
                {
                    labels[y * width + x] = 2;
                }
            }

            return labels;
        }

        var free = Labels();

        LabelMapRefiner.FillRemainingGaps(free, width, height, rgba);
        var limited = Labels();

        LabelMapRefiner.FillRemainingGaps(limited, width, height, rgba, [null, new PixelRect(5, 0, 20, height), null]);

        Assert.Equal(1, free[5 * width + 0]);       // unlimited: the teapot spread to the photo's edge
        Assert.Equal(0, limited[5 * width + 0]);    // limited: out of its reach, and no one else touches it
        Assert.Equal(1, limited[5 * width + 5]);    // the edge of its reach is still its own
        Assert.DoesNotContain(Enumerable.Range(0, width * height), p => limited[p] == 1 && p % width is < 5 or >= 20);
        Assert.All(Enumerable.Range(20, 20), x => Assert.Equal(2, limited[5 * width + x])); // the wall took the rest
    }

    /// <summary>A mask whose pixels are set where the rule says.</summary>
    private static ObjectMask Mask(int width, int height, Func<int, int, bool> on)
    {
        var coverage = new byte[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                coverage[y * width + x] = on(x, y) ? (byte)255 : (byte)0;
            }
        }

        return new ObjectMask(width, height, coverage);
    }
}

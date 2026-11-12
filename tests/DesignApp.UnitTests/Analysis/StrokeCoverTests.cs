using DesignApp.Application.Helpers;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Photos;
using static DesignApp.Application.Constants;

namespace DesignApp.UnitTests.Analysis;

/// <summary>
/// The box and lasso strokes: which pixels they cover, how the outline and mark rules apply to them, and the point
/// counts each shape needs.
/// </summary>
public class StrokeCoverTests
{
    // A 20x10 label map: object 1 covers x < 6, object 2 covers x >= 10, x 6..9 is unassigned.
    private const int W = 20, H = 10;

    private static byte[] Labels()
    {
        var labels = new byte[W * H];
        for (var p = 0; p < W * H; p++)
            labels[p] = (byte)(p % W < 6 ? 1 : p % W >= 10 ? 2 : 0);
        return labels;
    }

    /// <summary>A point at label-map pixel coordinates (not centres), as a fraction of the map.</summary>
    private static StrokePoint At(double x, double y)
    {
        return new StrokePoint(x / W, y / H);
    }

    /// <summary>The covered pixels, as (x, y), in a set.</summary>
    private static HashSet<(int X, int Y)> Cover(StrokeShape shape, params StrokePoint[] points)
    {
        var covered = new HashSet<(int, int)>();
        StrokeCover.Walk(W, H, shape, 1, points, p => Assert.True(covered.Add((p % W, p / W)), $"pixel {p} twice"));
        return covered;
    }

    [Fact]
    public void A_box_covers_the_pixels_whose_centres_are_inside_whichever_way_it_is_drawn()
    {
        // Corners at 2..5 and 1..3: centres 2.5, 3.5, 4.5 across and 1.5, 2.5 down.
        var expected = new HashSet<(int, int)> { (2, 1), (3, 1), (4, 1), (2, 2), (3, 2), (4, 2) };

        Assert.Equal(expected, Cover(StrokeShape.Box, At(2, 1), At(5, 3)));
        Assert.Equal(expected, Cover(StrokeShape.Box, At(5, 3), At(2, 1)));
        Assert.Equal(expected, Cover(StrokeShape.Box, At(5, 1), At(2, 3)));
    }

    [Fact]
    public void A_box_past_the_edges_is_clipped_to_the_photo()
    {
        var covered = Cover(StrokeShape.Box, new StrokePoint(-0.2, -0.2), new StrokePoint(1.2, 1.2));

        Assert.Equal(W * H, covered.Count);
    }

    [Fact]
    public void A_box_too_small_to_hold_a_pixel_centre_covers_nothing()
    {
        Assert.Empty(Cover(StrokeShape.Box, At(2.6, 2.6), At(3.4, 3.4)));
    }

    [Fact]
    public void A_concave_lasso_leaves_its_notch_out()
    {
        // An L: the square 0..6 x 0..6 without its top-right quarter (3..6 x 0..3).
        var covered = Cover(StrokeShape.Lasso, At(0, 0), At(3, 0), At(3, 3), At(6, 3), At(6, 6), At(0, 6));

        Assert.Equal(36 - 9, covered.Count);
        Assert.Contains((1, 1), covered);
        Assert.Contains((5, 5), covered);
        Assert.DoesNotContain((4, 1), covered);
    }

    [Fact]
    public void A_self_crossing_lasso_fills_by_the_even_odd_rule()
    {
        // A bow tie: two triangles meeting at (4, 4); both halves are inside, nothing above or below the crossing.
        var covered = Cover(StrokeShape.Lasso, At(0, 0), At(8, 8), At(8, 0), At(0, 8));

        Assert.Contains((1, 4), covered);
        Assert.Contains((6, 4), covered);
        Assert.DoesNotContain((4, 1), covered);
        Assert.DoesNotContain((4, 7), covered);
    }

    [Fact]
    public void A_lasso_that_leaves_the_photo_is_clipped_to_it()
    {
        var covered = Cover(StrokeShape.Lasso, new StrokePoint(-0.5, -0.5), new StrokePoint(1.5, -0.5), new StrokePoint(1.5, 1.5), new StrokePoint(-0.5, 1.5));

        Assert.Equal(W * H, covered.Count);
    }

    [Fact]
    public void A_box_paints_its_area_into_the_object_and_erases_only_the_objects_own_pixels()
    {
        var labels = Labels();

        // Across the gap and into object 2, rows 2..4.
        var painted = OutlineBrush.Apply(labels, W, H, new OutlineStroke(1, "wall", OutlineBrushMode.Paint, 1, [At(6, 2), At(12, 5)], StrokeShape.Box));

        Assert.Equal(6 * 3, painted.Value);
        Assert.Equal(1, labels[3 * W + 11]);
        Assert.Equal(2, labels[1 * W + 11]);

        var erased = OutlineBrush.Apply(labels, W, H, new OutlineStroke(2, "rug", OutlineBrushMode.Erase, 1, [At(0, 0), At(20, 3)], StrokeShape.Box));

        Assert.Equal(10 * 3 - 2 * 1, erased.Value); // object 2's pixels in rows 0..2, less the two the wall took in row 2
        Assert.Equal(1, labels[0 * W + 0]);
    }

    [Fact]
    public void A_box_over_the_whole_object_can_not_erase_it_all()
    {
        var labels = Labels();

        var result = OutlineBrush.Apply(labels, W, H, new OutlineStroke(1, "wall", OutlineBrushMode.Erase, 1, [At(0, 0), At(W, H)], StrokeShape.Box));

        Assert.False(result.Succeeded);
        Assert.Contains("erase the whole wall", result.Error, StringComparison.Ordinal);
        Assert.Equal(Labels(), labels);
    }

    [Fact]
    public void A_lasso_marks_a_new_object_and_a_box_unmarks_part_of_it()
    {
        var labels = Labels();
        MarkStroke lasso = new(OutlineBrushMode.Paint, 1, [At(6, 0), At(10, 0), At(10, 10), At(6, 10)], StrokeShape.Lasso);
        MarkStroke box = new(OutlineBrushMode.Erase, 1, [At(6, 0), At(10, 5)], StrokeShape.Box);

        var marked = ObjectMarker.Apply(labels, W, H, new NewObjectMark(3, [lasso, box]));

        Assert.Equal(4 * 5, marked.Value);
        Assert.Equal(0, labels[2 * W + 7]);
        Assert.Equal(3, labels[7 * W + 7]);
    }

    [Theory]
    [InlineData(StrokeShape.Brush, 1, null)]
    [InlineData(StrokeShape.Box, 2, null)]
    [InlineData(StrokeShape.Box, 1, "two corners")]
    [InlineData(StrokeShape.Box, 3, "two corners")]
    [InlineData(StrokeShape.Lasso, 3, null)]
    [InlineData(StrokeShape.Lasso, 2, "at least 3")]
    [InlineData((StrokeShape)7, 3, "brush, the box or the lasso")]
    public void Each_shape_needs_its_number_of_points(StrokeShape shape, int count, string? message)
    {
        var points = Enumerable.Range(0, count).Select(i => new StrokePoint(i * 0.1, i * 0.05)).ToList();

        var error = StrokeShapes.Check(shape, points);

        if (message is null)
            Assert.Null(error);
        else
            Assert.Contains(message, error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stroke_without_a_shape_is_the_brush()
    {
        Assert.Equal(StrokeShape.Brush, new MarkStroke(OutlineBrushMode.Paint, 1, [At(1, 1)]).Shape);
        Assert.Equal(StrokeShape.Brush, new OutlineStroke(1, "wall", OutlineBrushMode.Paint, 1, [At(1, 1)]).Shape);
    }
}

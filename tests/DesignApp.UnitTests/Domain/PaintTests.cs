using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

namespace DesignApp.UnitTests.Domain;

/// <summary>Recolors that name a real paint, and the sizes users give walls, floors and ceilings.</summary>
public class PaintTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly HexColor White = HexColor.Parse("#F7F9EF");

    [Fact]
    public void A_recolor_may_name_a_paint_and_keeps_it()
    {
        var edit = new RecolorEdit(1, White, 0.8, "ral-classic/9010");

        Assert.Equal("ral-classic/9010", edit.Paint);
        Assert.Null(new RecolorEdit(1, White, 0.8).Paint);
        Assert.NotEqual(new RecolorEdit(1, White, 0.8), edit); // the paint is part of the edit
    }

    [Theory]
    [InlineData("")]
    [InlineData("9010")]
    [InlineData("ral-classic/")]
    [InlineData("/9010")]
    [InlineData("ral classic/9010")]
    public void A_malformed_paint_reference_is_refused(string paint)
    {
        var error = Assert.Throws<DomainException>(() => new RecolorEdit(1, White, 0.8, paint));

        Assert.Equal(DesignApp.Domain.Constants.Messages.PaintReference, error.Message);
    }

    [Fact]
    public void A_paint_reference_is_limited_in_length()
    {
        var paint = "p/" + new string('x', DesignApp.Domain.Constants.Colors.MaxPaintLength - 1);

        Assert.Throws<DomainException>(() => new RecolorEdit(1, White, 0.8, paint));
    }

    [Theory]
    [InlineData(4, 2.5, 10)]
    [InlineData(0.1, 50, 5)]
    public void A_size_has_an_area(double width, double height, double area)
    {
        Assert.Equal(area, new SurfaceSize(1, width, height).Area, 6);
    }

    [Theory]
    [InlineData(0.05, 2.5)]
    [InlineData(4, 50.1)]
    [InlineData(double.NaN, 2.5)]
    [InlineData(4, double.PositiveInfinity)]
    public void A_size_outside_the_limits_is_refused(double width, double height)
    {
        var error = Assert.Throws<DomainException>(() => new SurfaceSize(1, width, height));

        Assert.Equal(DesignApp.Domain.Constants.Messages.SurfaceSize, error.Message);
    }

    [Fact]
    public void A_design_room_keeps_its_sizes_until_its_colors_are_saved_without_them()
    {
        var room = RoomTests.ReadyRoom();
        var design = Design.Create(room, "user-1", "Flat", null, [new RecolorEdit(1, White, 1, "ral-classic/9010")], [new SurfaceSize(1, 4, 2.5)], Now);

        var entry = Assert.Single(design.Rooms);

        Assert.Equal([new SurfaceSize(1, 4, 2.5)], entry.Sizes);
        Assert.Equal("ral-classic/9010", Assert.IsType<RecolorEdit>(Assert.Single(entry.Edits)).Paint);

        design.SetRoomEdits(room, [], Now.AddMinutes(1));
        Assert.Empty(entry.Sizes);
    }

    [Fact]
    public void Sizes_must_be_for_the_rooms_objects_and_one_each()
    {
        var room = RoomTests.ReadyRoom();
        var design = Design.Create(room, "user-1", "Flat", null, [], Now);

        var unknown = Assert.Throws<DomainException>(() => design.SetRoomEdits(room, [], [new SurfaceSize(99, 4, 2.5)], Now));

        Assert.Equal(DesignApp.Domain.Constants.Messages.UnknownSizeObject(99), unknown.Message);
        var twice = Assert.Throws<DomainException>(() => design.SetRoomEdits(room, [], [new SurfaceSize(1, 4, 2.5), new SurfaceSize(1, 3, 2.5)], Now));

        Assert.Equal(DesignApp.Domain.Constants.Messages.DuplicateSize(1), twice.Message);
    }
}

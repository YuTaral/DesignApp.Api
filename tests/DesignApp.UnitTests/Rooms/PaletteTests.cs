using DesignApp.Application.Helpers;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Palettes;

namespace DesignApp.UnitTests.Rooms;

/// <summary>The embedded paint palettes, and the check that a recolor's paint exists and matches its color.</summary>
public class PaletteTests
{
    private static readonly EmbeddedPaletteCatalog Catalog = new();

    [Fact]
    public void RAL_Classic_is_embedded_with_about_two_hundred_paints()
    {
        var ral = Assert.Single(Catalog.Palettes, p => p.Id == "ral-classic");

        Assert.Equal("RAL Classic", ral.Name);
        Assert.InRange(ral.Paints.Count, 200, 230);
        Assert.Equal(ral.Paints.Count, ral.Paints.Select(p => p.Id).Distinct().Count());
        Assert.All(ral.Paints, p => Assert.StartsWith("RAL ", p.Code));
        Assert.All(ral.Paints, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
        Assert.NotEmpty(ral.Notice);
    }

    [Fact]
    public void A_paint_is_found_by_its_reference()
    {
        var white = Catalog.Find("ral-classic/9010");

        Assert.NotNull(white);
        Assert.Equal(("RAL 9010", "Pure white"), (white.Code, white.Name));
        Assert.Null(Catalog.Find("ral-classic/0000"));
        Assert.Null(Catalog.Find("brand/9010"));
    }

    [Fact]
    public void Edits_with_a_known_paint_in_its_own_color_pass()
    {
        var white = Catalog.Find("ral-classic/9010")!;

        new EditChecks(Catalog).Check([new RecolorEdit(1, white.Color, 1, "ral-classic/9010"), new RecolorEdit(2, HexColor.Parse("#123456"), 1)]);
    }

    [Fact]
    public void An_unknown_paint_is_refused()
    {
        var error = Assert.Throws<DomainException>(() =>
            new EditChecks(Catalog).Check([new RecolorEdit(1, HexColor.Parse("#FFFFFF"), 1, "ral-classic/0000")]));

        Assert.Equal(DesignApp.Application.Constants.Messages.UnknownPaint("ral-classic/0000"), error.Message);
    }

    [Fact]
    public void A_paint_in_another_color_is_refused()
    {
        var error = Assert.Throws<DomainException>(() =>
            new EditChecks(Catalog).Check([new RecolorEdit(1, HexColor.Parse("#000000"), 1, "ral-classic/9010")]));

        Assert.Equal(DesignApp.Application.Constants.Messages.PaintColorMismatch("RAL 9010"), error.Message);
    }
}

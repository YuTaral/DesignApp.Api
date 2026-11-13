using DesignApp.Application.Helpers;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Database;
using DesignApp.Infrastructure.Palettes;

namespace DesignApp.UnitTests.Domain;

/// <summary>Objects split by color into tones (bricks and mortar), each recolored on its own.</summary>
public class ToneTests
{
    private static readonly HexColor Brick = HexColor.Parse("#9A4B36");
    private static readonly HexColor Mortar = HexColor.Parse("#C8C2B6");
    private static readonly HexColor White = HexColor.Parse("#F7F9EF");

    private static List<ToneRecolor> BricksAndMortar(string? paint = null)
    {
        return [new(Brick, 0.7, White, 0.9, paint), new(Mortar, 0.3, null, 0.8)];
    }

    [Fact]
    public void A_recolor_keeps_its_tones_and_compares_them_by_value()
    {
        var edit = new RecolorEdit(1, White, 0.9, null, BricksAndMortar());

        Assert.Equal(2, edit.Tones.Count);
        Assert.Null(edit.Tones[1].Color);
        Assert.Empty(new RecolorEdit(1, White, 0.9).Tones);
        Assert.Equal(new RecolorEdit(1, White, 0.9, null, BricksAndMortar()), edit);
        Assert.Equal(new RecolorEdit(1, White, 0.9, null, BricksAndMortar()).GetHashCode(), edit.GetHashCode());
        Assert.NotEqual(new RecolorEdit(1, White, 0.9), edit);
        Assert.NotEqual(new RecolorEdit(1, White, 0.9, null, [new(Brick, 0.7, White, 0.5), new(Mortar, 0.3, null, 0.8)]), edit);
    }

    [Fact]
    public void Tones_must_be_two_to_four_different_colors_that_fit_in_the_object()
    {
        ToneRecolor Tone(string center, double share)
        {
            return new(HexColor.Parse(center), share, null, 0.8);
        }

        Assert.Equal(DesignApp.Domain.Constants.Messages.ToneCount,
            Assert.Throws<DomainException>(() => new RecolorEdit(1, White, 1, null, [Tone("#111111", 1)])).Message);
        Assert.Equal(DesignApp.Domain.Constants.Messages.ToneCount,
            Assert.Throws<DomainException>(() => new RecolorEdit(1, White, 1, null,
                [Tone("#111111", 0.2), Tone("#222222", 0.2), Tone("#333333", 0.2), Tone("#444444", 0.2), Tone("#555555", 0.2)])).Message);
        Assert.Equal(DesignApp.Domain.Constants.Messages.DuplicateTones,
            Assert.Throws<DomainException>(() => new RecolorEdit(1, White, 1, null, [Tone("#111111", 0.5), Tone("#111111", 0.5)])).Message);
        Assert.Equal(DesignApp.Domain.Constants.Messages.ToneShare,
            Assert.Throws<DomainException>(() => new RecolorEdit(1, White, 1, null, [Tone("#111111", 0.7), Tone("#222222", 0.7)])).Message);
    }

    [Fact]
    public void A_tone_checks_its_share_strength_and_paint()
    {
        Assert.Throws<DomainException>(() => new ToneRecolor(Brick, 1.5, null, 0.8));
        Assert.Throws<DomainException>(() => new ToneRecolor(Brick, double.NaN, null, 0.8));
        Assert.Throws<DomainException>(() => new ToneRecolor(Brick, 0.5, White, 2));
        Assert.Equal(DesignApp.Domain.Constants.Messages.TonePaintNeedsColor,
            Assert.Throws<DomainException>(() => new ToneRecolor(Brick, 0.5, null, 0.8, "ral-classic/9010")).Message);
        Assert.Equal(DesignApp.Domain.Constants.Messages.PaintReference,
            Assert.Throws<DomainException>(() => new ToneRecolor(Brick, 0.5, White, 0.8, "9010")).Message);
    }

    [Fact]
    public void Tones_are_stored_and_read_back_and_older_edits_have_none()
    {
        IReadOnlyList<EditOperation> edits = [new RecolorEdit(1, White, 0.9, "ral-classic/9010", BricksAndMortar("ral-classic/9010")), new RecolorEdit(2, Brick, 1)];

        var json = (string)EditsJson.Converter.ConvertToProvider(edits)!;
        var back = (IReadOnlyList<EditOperation>)EditsJson.Converter.ConvertFromProvider(json)!;
        var old = (IReadOnlyList<EditOperation>)EditsJson.Converter.ConvertFromProvider(
            """[{"type":"recolor","objectIndex":3,"color":"#123456","strength":1}]""")!;

        Assert.Equal(edits, back);
        Assert.DoesNotContain("\"tones\":null", json, StringComparison.Ordinal);
        Assert.Empty(Assert.IsType<RecolorEdit>(Assert.Single(old)).Tones);
    }

    [Fact]
    public void A_tones_paint_must_exist_and_have_its_color()
    {
        var checks = new EditChecks(new EmbeddedPaletteCatalog());

        checks.Check([new RecolorEdit(1, White, 0.9, "ral-classic/9010", BricksAndMortar("ral-classic/9010"))]);
        Assert.Equal(DesignApp.Application.Constants.Messages.UnknownPaint("ral-classic/0000"),
            Assert.Throws<DomainException>(() => checks.Check([new RecolorEdit(1, White, 0.9, null, BricksAndMortar("ral-classic/0000"))])).Message);
        Assert.Equal(DesignApp.Application.Constants.Messages.PaintColorMismatch("RAL 9010"),
            Assert.Throws<DomainException>(() => checks.Check([new RecolorEdit(1, White, 0.9, null,
                [new(Brick, 0.7, Brick, 0.9, "ral-classic/9010"), new(Mortar, 0.3, null, 0.8)])])).Message);
    }
}

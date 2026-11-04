using DesignApp.Domain.Common;
using DesignApp.Domain.Designs;
using DesignApp.Domain.Rooms;

namespace DesignApp.UnitTests.Domain;

/// <summary>Rules for creating and editing designs, and for parsing colors.</summary>
public class DesignTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly HexColor Terracotta = HexColor.Parse("#C2552F");

    [Fact]
    public void Create_stores_trimmed_name_and_edits()
    {
        var room = RoomTests.ReadyRoom();

        var design = Design.Create(room, "user-1", "  Warm living room  ", [new RecolorEdit(1, Terracotta, 0.8)], Now);

        Assert.Equal("Warm living room", design.Name);
        Assert.Equal(room.Id, design.RoomId);
        var edit = Assert.IsType<RecolorEdit>(Assert.Single(design.Edits));
        Assert.Equal("#C2552F", edit.Color.Value);
    }

    [Fact]
    public void Cannot_design_a_room_that_is_still_analyzing()
    {
        var room = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/photo.jpg", 1600, 1200, Now);

        Assert.Throws<DomainException>(() => Design.Create(room, "user-1", "Draft", [], Now));
    }

    [Fact]
    public void Cannot_design_someone_elses_room()
    {
        var room = RoomTests.ReadyRoom(ownerId: "user-1");

        Assert.Throws<DomainException>(() => Design.Create(room, "user-2", "Draft", [], Now));
    }

    [Fact]
    public void Edits_must_target_objects_in_the_room()
    {
        var room = RoomTests.ReadyRoom();

        Assert.Throws<DomainException>(() => Design.Create(room, "user-1", "Draft", [new RecolorEdit(99, Terracotta, 1)], Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_cannot_be_blank(string name)
    {
        var room = RoomTests.ReadyRoom();

        Assert.Throws<DomainException>(() => Design.Create(room, "user-1", name, [], Now));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Recolor_strength_must_be_between_zero_and_one(double strength)
    {
        Assert.Throws<DomainException>(() => new RecolorEdit(1, Terracotta, strength));
    }

    [Theory]
    [InlineData("#c2552f", true)]
    [InlineData("#C2552F", true)]
    [InlineData("C2552F", false)]
    [InlineData("#C2552", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData(null, false)]
    public void Hex_color_parsing(string? input, bool valid)
    {
        Assert.Equal(valid, HexColor.TryParse(input, out var color));
        if (valid)
            Assert.Equal(input!.ToUpperInvariant(), color.Value);
    }
}

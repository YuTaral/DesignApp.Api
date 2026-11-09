using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;


namespace DesignApp.UnitTests.Domain;

/// <summary>Rules for designs (a name, a description and one or more rooms with their colors), and for parsing colors.</summary>
public class DesignTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly HexColor Terracotta = HexColor.Parse("#C2552F");

    [Fact]
    public void Create_stores_trimmed_details_and_the_first_room_with_its_colors()
    {
        var room = RoomTests.ReadyRoom();

        var design = Design.Create(room, "user-1", "  Warm living room  ", "  Terracotta and oak  ", [new RecolorEdit(1, Terracotta, 0.8)], Now);

        Assert.Equal(("Warm living room", "Terracotta and oak", Now, Now), (design.Name, design.Description, design.CreatedAt, design.UpdatedAt));
        var entry = Assert.Single(design.Rooms);
        Assert.Equal(room.Id, entry.RoomId);
        var edit = Assert.IsType<RecolorEdit>(Assert.Single(entry.Edits));
        Assert.Equal("#C2552F", edit.Color.Value);
    }

    [Fact]
    public void The_description_is_optional_and_limited()
    {
        var design = Design.Create(RoomTests.ReadyRoom(), "user-1", "Draft", null, [], Now);
        Assert.Equal("", design.Description);

        var error = Assert.Throws<DomainException>(() => design.Rename("Draft", new string('x', DesignApp.Domain.Constants.Designs.MaxDescriptionLength + 1), Now));
        Assert.Equal(DesignApp.Domain.Constants.Messages.DescriptionLength, error.Message);
    }

    [Fact]
    public void Rooms_are_added_in_order_without_colors_and_get_them_later()
    {
        var first = RoomTests.ReadyRoom();
        var design = Design.Create(first, "user-1", "Flat", null, [], Now);
        var analyzing = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/b.jpg", 800, 600, Now);
        var second = RoomTests.ReadyRoom();

        design.AddRoom(analyzing, Now.AddMinutes(1));
        design.AddRoom(second, Now.AddMinutes(2));
        design.SetRoomEdits(second, [new RecolorEdit(2, Terracotta, 1)], Now.AddMinutes(3));

        Assert.Equal([first.Id, analyzing.Id, second.Id], design.Rooms.Select(r => r.RoomId));
        Assert.Equal([0, 1, 2], design.Rooms.Select(r => r.Position));
        Assert.Single(design.Rooms[2].Edits);
        Assert.Equal(Now.AddMinutes(3), design.UpdatedAt);
    }

    [Fact]
    public void Colors_need_an_analyzed_room()
    {
        var design = Design.Create(RoomTests.ReadyRoom(), "user-1", "Flat", null, [], Now);
        var analyzing = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/b.jpg", 800, 600, Now);
        design.AddRoom(analyzing, Now);

        Assert.Throws<DomainException>(() => design.SetRoomEdits(analyzing, [new RecolorEdit(1, Terracotta, 1)], Now));
    }

    [Fact]
    public void Only_your_own_rooms_once_each_and_up_to_the_limit()
    {
        var design = Design.Create(RoomTests.ReadyRoom(), "user-1", "Flat", null, [], Now);
        var room = RoomTests.ReadyRoom();
        design.AddRoom(room, Now);

        Assert.Equal(DesignApp.Domain.Constants.Messages.RoomAlreadyInThisDesign, Assert.Throws<DomainException>(() => design.AddRoom(room, Now)).Message);
        Assert.Equal(DesignApp.Domain.Constants.Messages.NotYourRoom,
            Assert.Throws<DomainException>(() => design.AddRoom(RoomTests.ReadyRoom(ownerId: "user-2"), Now)).Message);
        while (design.Rooms.Count < DesignApp.Domain.Constants.Designs.MaxRooms)
            design.AddRoom(RoomTests.ReadyRoom(), Now);
        Assert.Throws<DomainException>(() => design.AddRoom(RoomTests.ReadyRoom(), Now));
    }

    [Fact]
    public void A_room_can_be_removed_but_never_the_last_one()
    {
        var first = RoomTests.ReadyRoom();
        var design = Design.Create(first, "user-1", "Flat", null, [], Now);
        var second = RoomTests.ReadyRoom();
        design.AddRoom(second, Now);

        design.RemoveRoom(first.Id, Now);

        Assert.Equal([second.Id], design.Rooms.Select(r => r.RoomId));
        var error = Assert.Throws<DomainException>(() => design.RemoveRoom(second.Id, Now));
        Assert.Equal(DesignApp.Domain.Constants.Messages.LastRoom, error.Message);
    }

    [Fact]
    public void Cannot_design_someone_elses_room()
    {
        var room = RoomTests.ReadyRoom(ownerId: "user-1");

        Assert.Throws<DomainException>(() => Design.Create(room, "user-2", "Draft", null, [], Now));
    }

    [Fact]
    public void Edits_must_target_objects_in_the_room()
    {
        var room = RoomTests.ReadyRoom();

        Assert.Throws<DomainException>(() => Design.Create(room, "user-1", "Draft", null, [new RecolorEdit(99, Terracotta, 1)], Now));
    }

    [Fact]
    public void An_object_gets_at_most_one_edit()
    {
        var room = RoomTests.ReadyRoom();

        var error = Assert.Throws<DomainException>(() => Design.Create(room, "user-1", "Draft", null,
            [new RecolorEdit(1, Terracotta, 1), new RecolorEdit(1, HexColor.Parse("#FFFFFF"), 0.5)], Now));
        Assert.Equal(DesignApp.Domain.Constants.Messages.DuplicateEdit(1), error.Message);
    }

    [Fact]
    public void Saving_colors_replaces_them_without_changing_a_list_handed_out_earlier()
    {
        var room = RoomTests.ReadyRoom();
        var design = Design.Create(room, "user-1", "Draft", null, [new RecolorEdit(1, Terracotta, 1)], Now);
        var before = design.Rooms[0].Edits;

        design.SetRoomEdits(room, [], Now.AddMinutes(1));

        Assert.Empty(design.Rooms[0].Edits);
        Assert.Single(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_cannot_be_blank(string name)
    {
        var room = RoomTests.ReadyRoom();

        Assert.Throws<DomainException>(() => Design.Create(room, "user-1", name, null, [], Now));
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

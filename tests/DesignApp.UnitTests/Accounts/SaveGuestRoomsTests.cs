using DesignApp.Application.Helpers;
using DesignApp.Application.UseCases;
using DesignApp.Domain.Entities;


namespace DesignApp.UnitTests.Accounts;

/// <summary>Moving a guest's rooms into an account, and the guest ids themselves.</summary>
public class SaveGuestRoomsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Every_room_of_the_guest_moves_to_the_account_and_no_one_elses()
    {
        var rooms = new FakeRooms();
        var guest = GuestIds.New();
        var otherGuest = GuestIds.New();
        var mine = await SaveAsync(rooms, guest, guest, otherGuest);

        var moved = await new SaveGuestRooms(rooms).HandleAsync(guest, "user-1", CancellationToken.None);

        Assert.Equal(2, moved);
        Assert.All(mine.Take(2), r => Assert.Equal("user-1", r.OwnerId));
        Assert.Equal(otherGuest, mine[2].OwnerId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("user-2")] // an account's id is never saved as if it were a guest
    public async Task Only_guest_ids_are_saved(string? from)
    {
        var rooms = new FakeRooms();
        var saved = await SaveAsync(rooms, "user-2");
        var saves = rooms.SaveCount;

        var moved = await new SaveGuestRooms(rooms).HandleAsync(from, "user-1", CancellationToken.None);

        Assert.Equal(0, moved);
        Assert.Equal("user-2", saved[0].OwnerId);
        Assert.Equal(saves, rooms.SaveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("user-1")]
    public void Tokens_we_did_not_hand_out_give_no_guest_id(string token)
    {
        Assert.Null(GuestIds.FromToken(token));
    }

    [Fact]
    public void A_guest_id_round_trips_through_its_token_and_never_looks_like_an_account()
    {
        var id = GuestIds.New();

        Assert.Equal(id, GuestIds.FromToken(GuestIds.ToToken(id)));
        Assert.True(GuestIds.IsGuest(id));
        Assert.False(GuestIds.IsGuest(Guid.NewGuid().ToString()));
    }

    /// <summary>Saves one ready-to-analyze room per owner.</summary>
    /// <param name="rooms">The repository.</param>
    /// <param name="owners">Who owns each room.</param>
    private static async Task<List<Room>> SaveAsync(FakeRooms rooms, params string[] owners)
    {
        var saved = owners.Select((owner, i) =>
            Room.Upload(Guid.CreateVersion7(Now.AddMinutes(i)), owner, $"rooms/{i}.jpg", 800, 600, Now.AddMinutes(i))).ToList();
        saved.ForEach(rooms.Add);
        await rooms.SaveChangesAsync(CancellationToken.None);
        return saved;
    }
}

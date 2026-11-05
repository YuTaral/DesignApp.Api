using DesignApp.Application.Abstractions;
using DesignApp.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DesignApp.IntegrationTests;

/// <summary>
/// Two writers saving the same room in real Postgres: changes to different fields are merged
/// (a guest signs in while the room is analyzed), the same field changed twice still fails.
/// </summary>
/// <param name="factory">The API running against test containers.</param>
public class RoomConcurrencyTests(ApiFactoryWithoutAnalysis factory) : IClassFixture<ApiFactoryWithoutAnalysis>
{
    [Fact]
    public async Task Moving_a_room_to_an_account_during_its_analysis_keeps_both_changes()
    {
        var id = await AddRoomAsync();
        using var worker = factory.Services.CreateScope();
        using var signIn = factory.Services.CreateScope();
        var analyzing = await Rooms(worker).GetForAnalysisAsync(id, CancellationToken.None);
        var claiming = await Rooms(signIn).GetForAnalysisAsync(id, CancellationToken.None);

        claiming!.TransferTo("user-1");
        await Rooms(signIn).SaveChangesAsync(CancellationToken.None);
        analyzing!.StartAnalysis();
        await Rooms(worker).SaveChangesAsync(CancellationToken.None);

        using var check = factory.Services.CreateScope();
        var stored = await Rooms(check).GetForAnalysisAsync(id, CancellationToken.None);
        Assert.Equal(("user-1", RoomStatus.Analyzing), (stored!.OwnerId, stored.Status));
        Assert.Equal("user-1", analyzing.OwnerId); // the worker's copy took the new owner too
    }

    [Fact]
    public async Task Two_workers_moving_the_same_rooms_status_still_conflict()
    {
        var id = await AddRoomAsync();
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var a = await Rooms(first).GetForAnalysisAsync(id, CancellationToken.None);
        var b = await Rooms(second).GetForAnalysisAsync(id, CancellationToken.None);

        a!.StartAnalysis();
        await Rooms(first).SaveChangesAsync(CancellationToken.None);
        b!.StartAnalysis();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Rooms(second).SaveChangesAsync(CancellationToken.None));
    }

    /// <summary>The room repository of a scope.</summary>
    /// <param name="scope">The scope, like one request or one analysis.</param>
    private static IRoomRepository Rooms(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IRoomRepository>();

    /// <summary>Stores an uploaded room straight in the database, without scheduling its analysis.</summary>
    private async Task<Guid> AddRoomAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var room = Room.Upload(Guid.CreateVersion7(now), GuestIds.New(), "rooms/test/photo.jpg", 800, 600, now);
        using var scope = factory.Services.CreateScope();
        Rooms(scope).Add(room);
        await Rooms(scope).SaveChangesAsync(CancellationToken.None);
        return room.Id;
    }
}

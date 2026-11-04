using DesignApp.Application.Abstractions;
using DesignApp.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace DesignApp.Infrastructure.Persistence;

internal sealed class RoomRepository(AppDbContext db) : IRoomRepository
{
    public void Add(Room room) => db.Rooms.Add(room);

    public Task<Room?> FindAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        db.Rooms.SingleOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

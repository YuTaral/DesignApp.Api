using DesignApp.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace DesignApp.Infrastructure.Persistence;

/// <summary>The EF Core database session for Postgres.</summary>
/// <param name="options">Connection and provider settings.</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>The rooms table.</summary>
    public DbSet<Room> Rooms => Set<Room>();

    /// <summary>Applies every entity configuration in this assembly, such as <see cref="RoomConfiguration"/>.</summary>
    /// <param name="modelBuilder">Builds the database model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}

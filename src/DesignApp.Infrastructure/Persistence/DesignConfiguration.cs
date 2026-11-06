using DesignApp.Domain.Designs;
using DesignApp.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesignApp.Infrastructure.Persistence;

/// <summary>Maps <see cref="Design"/> to the <c>designs</c> table and its rooms to <c>design_rooms</c>.</summary>
internal sealed class DesignConfiguration : IEntityTypeConfiguration<Design>
{
    /// <summary>Configures the tables, their columns and indexes.</summary>
    /// <param name="builder">Builds the entity's mapping.</param>
    public void Configure(EntityTypeBuilder<Design> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.OwnerId).HasMaxLength(Constants.Persistence.OwnerIdLength);
        builder.Property(d => d.Name).HasMaxLength(Domain.Constants.Designs.MaxNameLength);
        builder.Property(d => d.Description).HasMaxLength(Domain.Constants.Designs.MaxDescriptionLength);
        builder.Property<uint>(Constants.Persistence.Version).IsRowVersion();
        builder.HasIndex(d => new { d.OwnerId, d.UpdatedAt });

        // A design's rooms are part of it: loaded, saved and deleted with it.
        builder.OwnsMany(d => d.Rooms, rooms =>
        {
            rooms.ToTable(Constants.Persistence.Tables.DesignRooms);
            rooms.WithOwner().HasForeignKey("DesignId");
            rooms.HasKey("DesignId", nameof(DesignRoom.RoomId));
            // A room is in at most one design, and deleting a room takes it out of its design.
            rooms.HasIndex(r => r.RoomId).IsUnique();
            rooms.HasOne<Room>().WithMany().HasForeignKey(r => r.RoomId).OnDelete(DeleteBehavior.Cascade);
            // The edits are always read and written with their room, so they live in one jsonb column.
            rooms.Property(r => r.Edits)
                .HasField("_edits")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasColumnType(Constants.Persistence.Json)
                .HasConversion(EditsJson.Converter, EditsJson.Comparer);
        });
        builder.Navigation(d => d.Rooms).HasField("_rooms").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

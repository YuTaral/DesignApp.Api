using DesignApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesignApp.Infrastructure.Database;

/// <summary>
/// Maps <see cref="PhotoTake"/> to the <c>photo_takes</c> table: one row per photo, so photos made at the same time
/// are added without touching their room's row; deleting the room deletes them.
/// </summary>
internal sealed class PhotoTakeConfiguration : IEntityTypeConfiguration<PhotoTake>
{
    /// <summary>Sets the key, column sizes, the cascade from the room and the room's index.</summary>
    /// <param name="builder">Configures the PhotoTake entity.</param>
    public void Configure(EntityTypeBuilder<PhotoTake> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Key).HasMaxLength(Constants.Persistence.StorageKeyLength);
        builder.Property(t => t.Note).HasMaxLength(Domain.Constants.Rooms.MaxTakeNoteLength);
        builder.HasOne<Room>().WithMany().HasForeignKey(t => t.RoomId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => new { t.RoomId, t.CreatedAt });
    }
}

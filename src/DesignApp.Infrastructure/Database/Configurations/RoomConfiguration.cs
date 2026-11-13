using DesignApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesignApp.Infrastructure.Database;

/// <summary>Maps <see cref="Room"/> to the <c>rooms</c> table.</summary>
internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    /// <summary>Sets keys, column sizes, the jsonb objects column and the owner index.</summary>
    /// <param name="builder">Configures the Room entity.</param>
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.OwnerId).HasMaxLength(Constants.Persistence.OwnerIdLength);
        builder.Property(r => r.PhotoKey).HasMaxLength(Constants.Persistence.StorageKeyLength);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(Constants.Persistence.EnumNameLength);
        builder.Property(r => r.FailureReason).HasMaxLength(Constants.Persistence.FailureReasonLength);
        builder.Property(r => r.LabelMapKey).HasMaxLength(Constants.Persistence.StorageKeyLength);
        builder.Property(r => r.Scope).HasConversion<string>().HasMaxLength(Constants.Persistence.EnumNameLength);
        builder.Property(r => r.AiMode).HasConversion<string>().HasMaxLength(Constants.Persistence.AiModeLength);
        builder.Property(r => r.LibraryRoom).HasMaxLength(Constants.Persistence.LibraryRoomLength);
        builder.Property(r => r.DepthKey).HasMaxLength(Constants.Persistence.StorageKeyLength);
        builder.Property(r => r.HasBackdrop).HasDefaultValue(false);

        // Postgres's xmin system column as an optimistic-concurrency token: if two workers ever
        // analyze the same room, the second save fails instead of silently overwriting the first.
        builder.Property<uint>(Constants.Persistence.Version).IsRowVersion();

        // The detected objects are always read and written together with their room,
        // so they live in one jsonb column instead of a separate table.
        builder.Property(r => r.Objects)
            .HasField("_objects")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnType(Constants.Persistence.Json)
            .HasConversion(DetectedObjectsJson.Converter, DetectedObjectsJson.Comparer);

        // Which photo goes with each label map version; read and written with the room, like its objects.
        builder.Property(r => r.PhotoVersions)
            .HasField("_photoVersions")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnType(Constants.Persistence.Json)
            .HasDefaultValueSql(Constants.Persistence.EmptyJsonArray)
            .HasConversion(PhotoVersionsJson.Converter, PhotoVersionsJson.Comparer);

        builder.HasIndex(r => new { r.OwnerId, r.CreatedAt });
        builder.HasIndex(r => r.Status); // finding unfinished rooms on startup
    }
}

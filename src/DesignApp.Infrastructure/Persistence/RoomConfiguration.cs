using System.Collections.ObjectModel;
using System.Text.Json;
using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using static DesignApp.Domain.Constants;

namespace DesignApp.Infrastructure.Persistence;

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

        builder.HasIndex(r => new { r.OwnerId, r.CreatedAt });
        builder.HasIndex(r => r.Status); // finding unfinished rooms on startup
    }
}

/// <summary>
/// Stores <see cref="DetectedObject"/>s through a flat record, so renaming a domain property
/// can't silently change the stored JSON.
/// </summary>
internal static class DetectedObjectsJson
{
    /// <summary>camelCase JSON, as stored in the column.</summary>
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Converts the objects list to and from the jsonb column.</summary>
    public static readonly ValueConverter<IReadOnlyList<DetectedObject>, string> Converter = new(
        objects => Serialize(objects),
        json => Deserialize(json));

    /// <summary>Lets EF Core detect changes to the list by comparing its items.</summary>
    public static readonly ValueComparer<IReadOnlyList<DetectedObject>> Comparer = new(
        (a, b) => a!.SequenceEqual(b!),
        objects => objects.Aggregate(0, (hash, o) => HashCode.Combine(hash, o)),
        objects => objects.ToList().AsReadOnly());

    /// <summary>Writes the objects as JSON.</summary>
    /// <param name="objects">The objects to write.</param>
    private static string Serialize(IReadOnlyList<DetectedObject> objects) =>
        JsonSerializer.Serialize(objects.Select(StoredObject.From), Options);

    /// <summary>Reads the objects back from JSON.</summary>
    /// <param name="json">The stored column value.</param>
    private static ReadOnlyCollection<DetectedObject> Deserialize(string json) =>
        (JsonSerializer.Deserialize<List<StoredObject>>(json, Options) ?? []).Select(o => o.ToDomain()).ToList().AsReadOnly();

    /// <summary>The stored JSON shape of one object, kept separate from the domain type.</summary>
    /// <param name="Index">Label-map value.</param>
    /// <param name="Label">What the object is.</param>
    /// <param name="Category">The kind of object.</param>
    /// <param name="X">Box left edge.</param>
    /// <param name="Y">Box top edge.</param>
    /// <param name="Width">Box width.</param>
    /// <param name="Height">Box height.</param>
    /// <param name="Color">Dominant color as "#RRGGBB".</param>
    private sealed record StoredObject(int Index, string Label, ObjectCategory Category, double X, double Y, double Width, double Height, string Color)
    {
        /// <summary>Flattens a domain object for storage.</summary>
        /// <param name="o">The object to store.</param>
        public static StoredObject From(DetectedObject o) =>
            new(o.Index, o.Label, o.Category, o.Box.X, o.Box.Y, o.Box.Width, o.Box.Height, o.DominantColor.Value);

        /// <summary>Rebuilds the domain object, re-checking its rules.</summary>
        public DetectedObject ToDomain() =>
            new(Index, Label, Category, new BoundingBox(X, Y, Width, Height), HexColor.Parse(Color));
    }
}

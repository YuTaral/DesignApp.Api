using System.Collections.ObjectModel;
using System.Text.Json;
using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DesignApp.Infrastructure.Persistence;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.OwnerId).HasMaxLength(128);
        builder.Property(r => r.PhotoKey).HasMaxLength(300);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.FailureReason).HasMaxLength(500);

        // The detected objects are always read and written together with their room,
        // so they live in one jsonb column instead of a separate table.
        builder.Property(r => r.Objects)
            .HasField("_objects")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnType("jsonb")
            .HasConversion(DetectedObjectsJson.Converter, DetectedObjectsJson.Comparer);

        builder.HasIndex(r => new { r.OwnerId, r.CreatedAt });
    }
}

/// <summary>
/// Stores <see cref="DetectedObject"/>s through a flat record, so renaming a domain property
/// can't silently change the stored JSON.
/// </summary>
internal static class DetectedObjectsJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static readonly ValueConverter<IReadOnlyList<DetectedObject>, string> Converter = new(
        objects => Serialize(objects),
        json => Deserialize(json));

    public static readonly ValueComparer<IReadOnlyList<DetectedObject>> Comparer = new(
        (a, b) => a!.SequenceEqual(b!),
        objects => objects.Aggregate(0, (hash, o) => HashCode.Combine(hash, o)),
        objects => objects.ToList().AsReadOnly());

    private static string Serialize(IReadOnlyList<DetectedObject> objects) =>
        JsonSerializer.Serialize(objects.Select(StoredObject.From), Options);

    private static ReadOnlyCollection<DetectedObject> Deserialize(string json) =>
        (JsonSerializer.Deserialize<List<StoredObject>>(json, Options) ?? []).Select(o => o.ToDomain()).ToList().AsReadOnly();

    private sealed record StoredObject(int Index, string Label, ObjectCategory Category, double X, double Y, double Width, double Height, string Color)
    {
        public static StoredObject From(DetectedObject o) =>
            new(o.Index, o.Label, o.Category, o.Box.X, o.Box.Y, o.Box.Width, o.Box.Height, o.DominantColor.Value);

        public DetectedObject ToDomain() =>
            new(Index, Label, Category, new BoundingBox(X, Y, Width, Height), HexColor.Parse(Color));
    }
}

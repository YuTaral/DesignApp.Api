using DesignApp.Domain.Entities;
using DesignApp.Domain.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesignApp.Infrastructure.Database;

/// <summary>How a built room is stored: one row, with its outline and its objects in jsonb columns.</summary>
internal sealed class RoomPlanConfiguration : IEntityTypeConfiguration<RoomPlan>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RoomPlan> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.OwnerId).HasMaxLength(Constants.Persistence.OwnerIdLength);
        builder.Property(p => p.Name).HasMaxLength(Domain.Constants.Plans.MaxNameLength);
        builder.Property(p => p.RoomType).HasConversion<string>().HasMaxLength(Constants.Persistence.EnumNameLength);
        builder.Property(p => p.AiMode).HasConversion<string>().HasMaxLength(Constants.Persistence.AiModeLength);
        builder.Property(p => p.WallColor).HasConversion(c => c.Value, v => HexColor.Parse(v)).HasMaxLength(Domain.Constants.Colors.Length);
        builder.Property(p => p.FloorColor).HasConversion(c => c.Value, v => HexColor.Parse(v)).HasMaxLength(Domain.Constants.Colors.Length);
        builder.Property(p => p.FloorType).HasConversion<string>().HasMaxLength(Constants.Persistence.EnumNameLength);
        builder.Property(p => p.View);
        builder.Property(p => p.Corner).HasConversion<string>().HasMaxLength(Constants.Persistence.EnumNameLength);
        builder.Property(p => p.Outline)
            .HasColumnType(Constants.Persistence.Json)
            .HasConversion(RoomOutlineJson.Converter, RoomOutlineJson.Comparer);
        builder.Property(p => p.Finish)
            .HasColumnType(Constants.Persistence.Json)
            .HasConversion(RoomFinishJson.Converter, RoomFinishJson.Comparer);
        builder.Property(p => p.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnType(Constants.Persistence.Json)
            .HasConversion(PlanItemsJson.Converter, PlanItemsJson.Comparer);
        builder.Property<uint>(Constants.Persistence.Version).IsRowVersion();
        builder.HasIndex(p => new { p.OwnerId, p.UpdatedAt });
    }
}

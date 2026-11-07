using DesignApp.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesignApp.Infrastructure.Database;

/// <summary>
/// Maps <see cref="AiSettings"/> to the one-row <c>ai_settings</c> table and seeds it with the
/// built-in defaults, so a fresh database behaves exactly as the code did before.
/// </summary>
internal sealed class AiSettingsConfiguration : IEntityTypeConfiguration<AiSettings>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AiSettings> builder)
    {
        builder.ToTable(Constants.Persistence.Tables.AiSettings, table =>
            table.HasCheckConstraint(Constants.Persistence.AiSettingsSingleRow, $"id = {Constants.Persistence.AiSettingsRowId}"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.InventoryModel).HasMaxLength(Constants.Persistence.ModelLength);
        builder.Property(s => s.InventoryEffort).HasConversion<string>().HasMaxLength(Constants.Persistence.EnumNameLength);
        builder.Property(s => s.SegmentationEndpoint).HasMaxLength(Constants.Persistence.UrlLength);
        builder.HasData(new AiSettings());
    }
}

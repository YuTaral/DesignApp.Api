using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoVersionsAndRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "photo_versions",
                table: "rooms",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "removal_endpoint",
                table: "ai_settings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "removal_mask_expansion",
                table: "ai_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "removal_quality",
                table: "ai_settings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "ai_settings",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "removal_endpoint", "removal_mask_expansion", "removal_quality" },
                values: new object[] { "https://fal.run/fal-ai/object-removal/mask", 15, "Best" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_versions",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "removal_endpoint",
                table: "ai_settings");

            migrationBuilder.DropColumn(
                name: "removal_mask_expansion",
                table: "ai_settings");

            migrationBuilder.DropColumn(
                name: "removal_quality",
                table: "ai_settings");
        }
    }
}

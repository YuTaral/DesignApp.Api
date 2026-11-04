using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "analysis_attempts",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "label_map_key",
                table: "rooms",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "rooms",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "ix_rooms_status",
                table: "rooms",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rooms_status",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "analysis_attempts",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "label_map_key",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "rooms");
        }
    }
}

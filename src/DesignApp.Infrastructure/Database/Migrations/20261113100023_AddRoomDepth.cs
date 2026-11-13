using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomDepth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "depth_fov_x",
                table: "rooms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "depth_fov_y",
                table: "rooms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "depth_key",
                table: "rooms",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "depth_fov_x",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "depth_fov_y",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "depth_key",
                table: "rooms");
        }
    }
}

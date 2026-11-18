using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanCameraSpot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "camera_spot",
                table: "room_plans",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "camera_spot",
                table: "room_plans");
        }
    }
}

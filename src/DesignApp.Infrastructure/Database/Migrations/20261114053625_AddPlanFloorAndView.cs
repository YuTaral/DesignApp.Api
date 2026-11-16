using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanFloorAndView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "floor_type",
                table: "room_plans",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Wood");

            migrationBuilder.AddColumn<string>(
                name: "view",
                table: "room_plans",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Back");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "floor_type",
                table: "room_plans");

            migrationBuilder.DropColumn(
                name: "view",
                table: "room_plans");
        }
    }
}

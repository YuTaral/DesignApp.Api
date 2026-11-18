using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanFromRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "from_room_id",
                table: "room_plans",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "from_room_id",
                table: "room_plans");
        }
    }
}

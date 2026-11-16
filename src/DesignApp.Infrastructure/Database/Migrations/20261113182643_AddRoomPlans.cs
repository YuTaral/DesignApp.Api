using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    room_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    width = table.Column<double>(type: "double precision", nullable: false),
                    length = table.Column<double>(type: "double precision", nullable: false),
                    height = table.Column<double>(type: "double precision", nullable: false),
                    wall_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    floor_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    items = table.Column<string>(type: "jsonb", nullable: false),
                    ai_mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_plans", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_room_plans_owner_id_updated_at",
                table: "room_plans",
                columns: new[] { "owner_id", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_plans");
        }
    }
}

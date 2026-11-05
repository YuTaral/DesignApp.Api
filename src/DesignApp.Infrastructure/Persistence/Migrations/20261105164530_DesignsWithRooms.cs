using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Designs hold one or more rooms (design_rooms, each with its own colors) and get a description;
    /// existing designs keep their room and colors.
    /// </summary>
    public partial class DesignsWithRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "designs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "designs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "design_rooms",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    design_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    edits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_design_rooms", x => new { x.design_id, x.room_id });
                    table.ForeignKey(
                        name: "fk_design_rooms_designs_design_id",
                        column: x => x.design_id,
                        principalTable: "designs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_design_rooms_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_design_rooms_room_id",
                table: "design_rooms",
                column: "room_id",
                unique: true);
            // Move each design's room and colors into design_rooms before the old columns go.
            // A room is in at most one design now: when two designs shared a room, the newer keeps it.
            migrationBuilder.Sql("""
                INSERT INTO design_rooms (design_id, room_id, position, edits)
                SELECT DISTINCT ON (room_id) id, room_id, 0, edits
                FROM designs
                ORDER BY room_id, updated_at DESC;
                UPDATE designs SET created_at = updated_at;
                DELETE FROM designs d WHERE NOT EXISTS (SELECT 1 FROM design_rooms r WHERE r.design_id = d.id);
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_designs_rooms_room_id",
                table: "designs");

            migrationBuilder.DropIndex(
                name: "ix_designs_room_id",
                table: "designs");

            migrationBuilder.DropColumn(
                name: "edits",
                table: "designs");

            migrationBuilder.DropColumn(
                name: "room_id",
                table: "designs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "design_rooms");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "designs");

            migrationBuilder.DropColumn(
                name: "description",
                table: "designs");

            migrationBuilder.AddColumn<string>(
                name: "edits",
                table: "designs",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "room_id",
                table: "designs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_designs_room_id",
                table: "designs",
                column: "room_id");

            migrationBuilder.AddForeignKey(
                name: "fk_designs_rooms_room_id",
                table: "designs",
                column: "room_id",
                principalTable: "rooms",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

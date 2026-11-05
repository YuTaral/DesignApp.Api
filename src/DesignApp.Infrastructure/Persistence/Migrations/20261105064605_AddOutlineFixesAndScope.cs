using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Persistence.Migrations
{
    /// <summary>Adds the analysis scope and the label map versions for outline fixes.</summary>
    public partial class AddOutlineFixesAndScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "label_map_revision",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "latest_label_map_revision",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "rooms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "WholeRoom"); // rooms uploaded before the choice existed looked for everything
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "label_map_revision",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "latest_label_map_revision",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "rooms");
        }
    }
}

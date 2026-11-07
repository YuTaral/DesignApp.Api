using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <summary>Adds which AI (fake or real) analyzes each room.</summary>
    public partial class AddAiMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_mode",
                table: "rooms",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Fake"); // rooms uploaded before the switch existed were analyzed in Development
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_mode",
                table: "rooms");
        }
    }
}

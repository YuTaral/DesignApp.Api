using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanFinish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "finish",
                table: "room_plans",
                type: "jsonb",
                nullable: false,
                // Plans saved before finishes get the default one: painted walls, a pendant light, at midday.
                defaultValue: "{\"walls\":\"Paint\",\"accentColor\":\"#8A9A84\",\"accentFinish\":\"Paint\",\"ceilingColor\":\"#FAFAF7\",\"trimColor\":\"#F4F1EA\",\"ceilingLight\":\"Pendant\",\"timeOfDay\":\"Midday\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "finish",
                table: "room_plans");
        }
    }
}

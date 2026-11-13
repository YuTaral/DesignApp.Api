using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPaintsAndSizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sizes",
                table: "design_rooms",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sizes",
                table: "design_rooms");
        }
    }
}

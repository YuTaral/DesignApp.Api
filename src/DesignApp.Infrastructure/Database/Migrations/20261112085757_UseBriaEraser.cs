using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class UseBriaEraser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ai_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "removal_endpoint",
                value: "https://queue.fal.run/fal-ai/bria/eraser");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ai_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "removal_endpoint",
                value: "https://queue.fal.run/fal-ai/object-removal/mask");
        }
    }
}

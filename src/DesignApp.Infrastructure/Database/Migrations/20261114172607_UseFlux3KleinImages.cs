using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class UseFlux3KleinImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ai_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "image_endpoint",
                value: "https://queue.fal.run/blackforestlabs/flux-3/klein/edit-image");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ai_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "image_endpoint",
                value: "https://queue.fal.run/fal-ai/flux-kontext/dev");
        }
    }
}

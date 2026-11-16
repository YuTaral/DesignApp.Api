using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Built rooms get an outline (their walls' corners, so walls can bend at right angles), and their walls are numbered
    /// instead of named: the view and each window's or door's wall go from Back, Right, Front, Left to 0, 1, 2, 3 (the
    /// numbers a rectangle's walls have), and every existing room's outline is its rectangle. A string can't be cast to a
    /// number, so the columns are converted by hand.
    /// </summary>
    public partial class AddPlanOutline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE room_plans ALTER COLUMN view DROP DEFAULT;
                ALTER TABLE room_plans ALTER COLUMN view TYPE integer
                  USING array_position(ARRAY['Back','Right','Front','Left'], view) - 1;
                UPDATE room_plans SET items = COALESCE((
                  SELECT jsonb_agg(CASE WHEN e ? 'wall'
                    THEN jsonb_set(e, '{wall}', to_jsonb(array_position(ARRAY['Back','Right','Front','Left'], e->>'wall') - 1))
                    ELSE e END ORDER BY o)
                  FROM jsonb_array_elements(items) WITH ORDINALITY AS t(e, o)), '[]'::jsonb);
                """);

            migrationBuilder.AddColumn<string>(
                name: "outline",
                table: "room_plans",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql(
                """
                UPDATE room_plans SET outline = jsonb_build_array(
                  jsonb_build_object('x', 0, 'y', 0),
                  jsonb_build_object('x', round(width * 100), 'y', 0),
                  jsonb_build_object('x', round(width * 100), 'y', round(length * 100)),
                  jsonb_build_object('x', 0, 'y', round(length * 100)));
                """);
        }

        /// <inheritdoc />
        /// <remarks>Only rooms that are rectangles survive: a fifth or later wall has no name to go back to.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "outline",
                table: "room_plans");

            migrationBuilder.Sql(
                """
                ALTER TABLE room_plans ALTER COLUMN view TYPE character varying(20)
                  USING (ARRAY['Back','Right','Front','Left'])[LEAST(view, 3) + 1];
                ALTER TABLE room_plans ALTER COLUMN view SET DEFAULT 'Back';
                UPDATE room_plans SET items = COALESCE((
                  SELECT jsonb_agg(CASE WHEN e ? 'wall'
                    THEN jsonb_set(e, '{wall}', to_jsonb((ARRAY['Back','Right','Front','Left'])[LEAST((e->>'wall')::int, 3) + 1]))
                    ELSE e END ORDER BY o)
                  FROM jsonb_array_elements(items) WITH ORDINALITY AS t(e, o)), '[]'::jsonb);
                """);
        }
    }
}

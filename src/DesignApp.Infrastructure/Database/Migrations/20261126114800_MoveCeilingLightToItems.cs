using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Ceiling lights became objects on the plan: each built room's one ceiling light (its finish's <c>ceilingLight</c>)
    /// becomes such an object in the room's middle, as the catalog's light of that kind (spotlights spread over the room,
    /// 70 cm in from its walls), reaching no lower than 180 cm above the floor; the finish keeps no ceiling light.
    /// </summary>
    public partial class MoveCeilingLightToItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE room_plans AS p
                SET items = p.items || jsonb_build_array(jsonb_build_object(
                        'label', l.label, 'shape', l.shape,
                        'x', round((p.width * 50)::numeric), 'y', round((p.length * 50)::numeric),
                        'width', CASE WHEN l.shape = 'Spotlight' THEN GREATEST(round((p.width * 100 - 140)::numeric), 10) ELSE l.w END,
                        'depth', CASE WHEN l.shape = 'Spotlight' THEN GREATEST(round((p.length * 100 - 140)::numeric), 10) ELSE l.d END,
                        'height', LEAST(l.h, GREATEST(round((p.height * 100 - 180)::numeric), 1)),
                        'rotation', 0, 'color', l.color, 'elevation', 0, 'material', l.material))
                FROM (VALUES
                    ('Pendant', 'dome pendant light', 'PendantLight', 40, 40, 70, '#F4F1EA', 'linen shade'),
                    ('Spotlights', 'grid of recessed spotlights', 'Spotlight', 120, 120, 3, '#FFFFFF', 'brushed steel'),
                    ('Chandelier', 'chandelier', 'Chandelier', 70, 70, 70, '#C9A86A', 'brass'),
                    ('FlushLight', 'flush ceiling lamp', 'CeilingLamp', 40, 40, 12, '#F4F1EA', 'ceramic'),
                    ('TrackLights', 'track light', 'TrackLight', 200, 6, 15, '#2A2A2A', 'powder-coated metal')
                ) AS l(kind, label, shape, w, d, h, color, material)
                WHERE p.finish ->> 'ceilingLight' = l.kind;

                UPDATE room_plans SET finish = finish - 'ceilingLight' WHERE finish ? 'ceilingLight';
                """);
        }

        /// <inheritdoc />
        /// <remarks>The lights stay objects: the finish can't hold them any more.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

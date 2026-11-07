using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DesignApp.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    inventory_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    inventory_effort = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    max_tokens = table.Column<int>(type: "integer", nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: false),
                    user_message = table.Column<string>(type: "text", nullable: false),
                    max_listed_objects = table.Column<int>(type: "integer", nullable: false),
                    max_points = table.Column<int>(type: "integer", nullable: false),
                    segmentation_endpoint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    segmentation_parallelism = table.Column<int>(type: "integer", nullable: false),
                    max_masks = table.Column<int>(type: "integer", nullable: false),
                    min_fit = table.Column<double>(type: "double precision", nullable: false),
                    min_share_inside_box = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_settings", x => x.id);
                    table.CheckConstraint("ck_ai_settings_single_row", "id = 1");
                });

            migrationBuilder.InsertData(
                table: "ai_settings",
                columns: new[] { "id", "instructions", "inventory_effort", "inventory_model", "max_listed_objects", "max_masks", "max_points", "max_tokens", "min_fit", "min_share_inside_box", "segmentation_endpoint", "segmentation_parallelism", "user_message" },
                values: new object[] { 1, "You prepare room photos for an interior-restyling app. The user will tap an object and\nrecolor it, so list the surfaces and objects a person would want to restyle: each wall\nseparately, the floor, the ceiling, furniture, rugs and other textiles, lamps, large decor,\nand fixtures such as doors, windows, cabinets and counters.\n\nRules:\n- Always list every wall that shows wall surface as its own entry (\"left wall\", \"back wall\",\n  \"right wall\"), even plain white walls and narrow strips beside windows, and the floor and\n  ceiling when visible. A tiled area or backsplash is an extra entry, not a replacement for its wall.\n- A wall is only its own surface (paint, plaster, wallpaper). Cabinets, shelves and appliances\n  built against it are separate entries, never part of the wall. The wall's box still spans the\n  whole wall from edge to edge, including the parts behind furniture, a TV or cabinets. Don't\n  list a wall that is completely hidden behind full-height cabinets.\n- Labels of walls, floors and ceilings name their finish (\"light grey painted back wall\",\n  \"light oak floor\"), not just \"wall\" or \"floor\".\n- Split large built-in units into one entry per section, so each entry is one continuous\n  surface: \"left tall cabinets\", \"upper cabinets\", \"right tall cabinets\", \"lower cabinets\",\n  \"island\". Every section of cabinetry needs an entry, including ones at the edges of the photo.\n- Doors and windows (glass doors included) have the category Opening; cabinet and wardrobe\n  doors belong to their cabinet or wardrobe.\n- One entry per physical object. Give similar objects distinct labels (\"left armchair\", \"right armchair\").\n- Labels are short, lower-case and concrete (\"oak dining table\", \"jute rug\"), usable as a segmentation prompt.\n- Skip things smaller than about 1% of the image, people, pets, and text.\n- The box encloses the object's whole visible extent, from its leftmost to its rightmost and\n  its top to its bottom visible pixel, as fractions of the image (0 to 1). Check each box\n  against the photo before answering; long objects such as islands and counters often span\n  most of the photo's width.\n- Give every wall, floor and ceiling 2 or {MaxPoints} points spread over its visible surface, each\n  clearly on the surface itself: on bare wall, floorboards or tiles, never on furniture, a\n  rug, a picture or anything else in front of it. Every other object gets no points.\n- Together the entries should cover nearly the whole photo: every large region of the photo\n  belongs to exactly one entry.\n- color is the object's main color as #RRGGBB, ignoring shadows and highlights.\n- At most {MaxListedObjects} objects, largest and most important first.\n- Set isRoom to false, with no objects, when the photo is not the interior of a room.", "Medium", "claude-sonnet-5-5", 30, 3, 3, 16000, 0.5, 0.80000000000000004, "https://fal.run/fal-ai/sam-3/image", 4, "List the objects in this room photo." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_settings");
        }
    }
}

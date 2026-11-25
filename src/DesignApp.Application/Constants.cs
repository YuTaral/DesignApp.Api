using static DesignApp.Domain.Constants;

namespace DesignApp.Application;

/// <summary>
/// Every fixed value of the use cases in one place: the limits of photos, the analysis and brush
/// strokes, how long unsaved uploads are kept, storage keys, and the messages shown to users.
/// </summary>
public static class Constants
{
    /// <summary>Limits for the AI analysis of a room photo.</summary>
    public static class Analysis
    {
        /// <summary>
        /// The AI works on a copy of the photo scaled to this long edge. More pixels cost more and are
        /// slower to send without finding objects any better; the label map is made at this size too.
        /// </summary>
        public const int MaxLongEdge = 2048;

        /// <summary>At most this many objects are kept per room, so the editor's object list stays usable.</summary>
        public const int MaxObjects = 40;

        /// <summary>Objects covering less of the photo than this (after overlaps) are dropped as noise.</summary>
        public const double MinAreaFraction = 0.002;
    }

    /// <summary>Limits for brush strokes on an outline.</summary>
    public static class Strokes
    {
        /// <summary>The most points one stroke may have.</summary>
        public const int MaxPoints = 10_000;

        /// <summary>The smallest brush radius, in label map pixels.</summary>
        public const double MinRadius = 0.5;

        /// <summary>The largest brush radius, in label map pixels.</summary>
        public const double MaxRadius = 400;

        /// <summary>How far left of or above the photo a point may lie, as a fraction of its size.</summary>
        public const double MinPoint = -1;

        /// <summary>How far right of or below the photo a point may lie, as a fraction of its size.</summary>
        public const double MaxPoint = 2;
    }

    /// <summary>Limits for removing objects from a photo.</summary>
    public static class Removals
    {
        /// <summary>The most objects one removal may take out together (a sofa with its cushions and throw).</summary>
        public const int MaxObjects = 20;
    }

    /// <summary>Limits of the room builder.</summary>
    public static class Builder
    {
        /// <summary>The longest description of an object the AI turns into one.</summary>
        public const int MaxDescriptionLength = 300;

        /// <summary>The largest 3D view the browser may send for a photo.</summary>
        public const int MaxGuideBytes = 10 * 1024 * 1024;

        /// <summary>The largest object masks the browser may send with a photo's 3D view.</summary>
        public const int MaxMasksBytes = 10 * 1024 * 1024;

        /// <summary>The shapes whose color in a photo is left as the image model made it: windows and doors aren't furniture.</summary>
        public static readonly IReadOnlySet<ItemShape> UncorrectedShapes = new HashSet<ItemShape> { ItemShape.Window, ItemShape.Door };

        /// <summary>
        /// The room's surfaces in the object masks the browser draws for a photo (<c>MASK_ID</c> in DesignApp.Web's
        /// <c>scene-3d.ts</c>, which must match); an object's id is 1 + its position in the plan, and 0 is nothing.
        /// </summary>
        public static class MaskIds
        {
            /// <summary>The floor, with its areas in other materials.</summary>
            public const int Floor = 250;

            /// <summary>The walls, with their areas in other finishes.</summary>
            public const int Wall = 251;

            /// <summary>The ceiling.</summary>
            public const int Ceiling = 252;
        }

        /// <summary>The longest extra instructions the user may add for the photo.</summary>
        public const int MaxInstructionsLength = 500;

        /// <summary>The start of an object's id in the layout sent to the image model ("object1", "object2"...).</summary>
        public const string BoxLabel = "object";

        /// <summary>
        /// The 3D view's camera, as the browser draws it (<c>scene-3d.ts</c> and <c>BUILDER.scene</c> in DesignApp.Web,
        /// which must match): the photo prompt describes only what this camera sees. Lengths in centimetres.
        /// </summary>
        public static class Camera
        {
            /// <summary>The camera's height above the floor.</summary>
            public const double EyeHeight = 145;
            /// <summary>The least room left between the camera and the ceiling.</summary>
            public const double CeilingGap = 30;
            /// <summary>How far in front of the near wall the camera stands.</summary>
            public const double Inset = 5;
            /// <summary>The least distance to the far wall used to fit the lens.</summary>
            public const double MinFitDistance = 50;
            /// <summary>
            /// Where the lens is fitted: the room's whole width shows from this share of the way to the far wall, so the
            /// side walls and what stands by them are in the picture (fitting only the far wall left them out).
            /// </summary>
            public const double FitDepthShare = 0.5;
            /// <summary>The narrowest horizontal view, in degrees.</summary>
            public const double MinFov = 80;
            /// <summary>The widest horizontal view, in degrees (a real-estate photographer's widest lens).</summary>
            public const double MaxFov = 100;
            /// <summary>How much taller the frame the picture is cut from is (its lower part is shown).</summary>
            public const double LensShift = 0.28;
            /// <summary>The picture's width over its height.</summary>
            public const double Aspect = 4.0 / 3.0;
            /// <summary>How many points are checked along each side of an object, to tell whether the camera sees it.</summary>
            public const int Samples = 5;
            /// <summary>
            /// The horizontal view from a near corner, in degrees: turned by <see cref="CornerTurn"/>, it reaches from the
            /// side wall the camera stands by to the near wall, so the whole room ahead is in the picture.
            /// </summary>
            public const double CornerFov = 90;
            /// <summary>How far a corner camera is turned from looking straight at the wall in view, in degrees (towards the room).</summary>
            public const double CornerTurn = 45;
        }

        /// <summary>
        /// The design rules a new room's starting layout follows (see <c>Helpers/StarterLayout</c>), in centimetres unless
        /// said otherwise.
        /// </summary>
        public static class Layout
        {
            /// <summary>How near its wall, in cm, a cabinet, sofa or bed read from a photo is pushed against it.</summary>
            public const double SnapReach = 40;
            /// <summary>How far, in cm, an object read from a photo is moved to clear what it overlaps before it is left out.</summary>
            public const double NudgeReach = 120;
            /// <summary>The side, in cm, of the floor kept clear where the camera of a photo read into a plan stands.</summary>
            public const double CameraClearance = 80;
            /// <summary>What a living room's, bedroom's, dining room's, office's or kids' room's windows start dressed with.</summary>
            public const string Curtains = "sheer curtains";
            /// <summary>What a kitchen's or bathroom's windows start dressed with.</summary>
            public const string Blind = "roller blind";
            /// <summary>The least room left between something hung on a wall and the ceiling, in centimetres.</summary>
            public const double CeilingGap = 10;
            /// <summary>The color of the wall behind a furnished bedroom's bed: a muted sage.</summary>
            public const string BedroomAccent = "#8A9A84";
            /// <summary>The color of the wall beside a furnished kids' room's bed: a pale sky blue.</summary>
            public const string KidsAccent = "#BFD5E3";
            /// <summary>A window's height above the floor in a living room, bedroom, dining room, office or kids' room.</summary>
            public const double Sill = 90;
            /// <summary>A window's height above the floor in a kitchen or bathroom (above the worktop and the bath).</summary>
            public const double WetSill = 110;
            /// <summary>The usual daylight rule: window glass of a tenth of the floor area.</summary>
            public const double GlassShare = 0.10;
            /// <summary>A bathroom's glass share, smaller for privacy.</summary>
            public const double BathroomGlassShare = 0.06;
            /// <summary>A window's usual height.</summary>
            public const double WindowHeight = 140;
            /// <summary>The wall kept above a window, up to the ceiling.</summary>
            public const double WindowHead = 30;
            /// <summary>The widest single window.</summary>
            public const double MaxWindowWidth = 160;
            /// <summary>The narrowest window.</summary>
            public const double MinWindowWidth = 60;
            /// <summary>The wall kept between windows and at the corners.</summary>
            public const double WindowGap = 40;
            /// <summary>How far the door is from the front corner.</summary>
            public const double DoorMargin = 15;
            /// <summary>A main walkway's width.</summary>
            public const double Walkway = 90;
            /// <summary>The space kept at the ends of a sofa or a run of furniture.</summary>
            public const double SideGap = 30;
            /// <summary>From a sofa's front to its coffee table.</summary>
            public const double SofaToTable = 45;
            /// <summary>The space beside a bed.</summary>
            public const double BedSide = 60;
            /// <summary>A kitchen aisle between the worktop and the island.</summary>
            public const double KitchenAisle = 110;
            /// <summary>The space behind a dining chair, to pull it out and walk past.</summary>
            public const double BehindChair = 45;
            /// <summary>The table edge each seat takes.</summary>
            public const double PlaceWidth = 60;
            /// <summary>The shortest run of cabinets worth placing.</summary>
            public const double MinRun = 40;
            /// <summary>The smallest rug side worth placing.</summary>
            public const double MinRug = 60;
            /// <summary>How far an object is moved at a time when its place along a wall is taken.</summary>
            public const double Step = 10;
            /// <summary>How near a wall an object must be to count as standing against it.</summary>
            public const double WallTouch = 5;
        }

        /// <summary>
        /// Named colors the photo prompt calls a color by (the nearest one, with its code): image models follow words far
        /// better than codes.
        /// </summary>
        public static readonly (string Name, string Hex)[] ColorNames =
        [
            ("white", "#FFFFFF"), ("off-white", "#F4F1EA"), ("cream", "#EFE6CF"), ("beige", "#D8C3A5"), ("sand", "#C9B48F"),
            ("light grey", "#C8C8C8"), ("grey", "#8A8A8A"), ("warm grey", "#8A8580"), ("charcoal", "#3A3A3A"), ("black", "#1E1E1E"),
            ("light oak", "#C9A27E"), ("oak", "#B8864B"), ("walnut", "#6B4A2E"), ("dark brown", "#4A3424"), ("tan", "#B89B7A"),
            ("terracotta", "#B5543C"), ("brick red", "#8C3B2E"), ("red", "#C0392B"), ("pink", "#E8B4B8"), ("blush", "#E9CFC7"),
            ("mustard", "#D9A93A"), ("yellow", "#F2D46B"), ("sage green", "#A8B89A"), ("olive", "#7D8B6A"), ("forest green", "#4F6B3A"),
            ("green", "#5B9A5B"), ("teal", "#3E7C7B"), ("light blue", "#9DB4C0"), ("blue", "#4A78A8"), ("slate blue", "#4A6274"),
            ("navy", "#2E4057"), ("lavender", "#B8A9C9"), ("plum", "#6E5A7E"),
        ];

        /// <summary>The wording of the photo prompt (see <c>Helpers/PlanPrompt</c>).</summary>
        public static class Prompt
        {
            /// <summary>
            /// What to make, and that nothing may be added. Said as what the photo holds rather than a list of things not to
            /// add, which FLUX Kontext ignored (it added curtains, a TV and a rug), and with the framing kept (it zoomed out).
            /// </summary>
            /// <param name="room">The room type in words.</param>
            public static string Intro(string room)
            {
                return $"Turn this simple 3D render into a realistic photograph of the same {room} interior, from exactly the same " +
                    "camera position, angle and framing, with level vertical lines; do not zoom out or show more of the room. " +
                    "Turn each plain shape into the real object it stands for, keeping its place, outline, size and exactly the " +
                    "color it has in the render.";
            }

            /// <summary>
            /// A change the user asked for to one of a room's photos: the image model edits that photo, and keeps the rest of
            /// it as it is, so the room stays the one built.
            /// </summary>
            /// <param name="change">The user's words.</param>
            public static string Change(string change)
            {
                return $"Edit this interior photograph: {change}. Keep everything else exactly as it is: the same room, camera " +
                    "position, angle and framing, the same furniture in the same places, and the same colors and light. The " +
                    "result must still look like a real photograph.";
            }

            /// <summary>
            /// The intro with a reference photo: the render is the first image, and the second is the run's first photo of the
            /// same room from another view, whose look every object keeps (the image model refers to images by their order).
            /// </summary>
            /// <param name="room">The room type in words.</param>
            public static string IntroWithReference(string room)
            {
                return $"Turn the first image, a simple 3D render, into a realistic photograph of the same {room} interior, from " +
                    "exactly the same camera position, angle and framing as the first image, with level vertical lines; do not zoom " +
                    "out or show more of the room. The second image is a photograph of this same room taken from another camera " +
                    "position: every object, its materials, colors, patterns and decorations, and the walls, floor and light look " +
                    "exactly as they do in the second image, now seen from the first image's camera. Take only their look from the " +
                    "second image, never its camera or framing. Turn each plain shape into the real object it stands for, keeping " +
                    "its place, outline, size and exactly the color it has in the render.";
            }

            /// <summary>Without decorations: only what is in the render.</summary>
            public const string Undecorated =
                "Everything in the photo is already in the render: keep the rest of the room empty, with bare walls and a bare floor.";

            /// <summary>
            /// Instead of <see cref="Undecorated"/> when an object in view has decorations: those, named with their
            /// objects, are added, and nothing else. FLUX 3 Image Klein draws them in the same step that makes the photo
            /// (FLUX Kontext dropped them; a second editing step drew them too, at twice the cost, and was compared in
            /// November 2026: one step got every decoration, and once pulled the camera back slightly).
            /// </summary>
            public const string WithDecorations =
                "Everything in the photo is already in the render, apart from the decorations named with their objects below, " +
                "each resting on or hanging at the one object it is named with and on no other object: keep the rest of the " +
                "room empty, with bare walls and a bare floor.";

            /// <summary>An object's decorations, after its description.</summary>
            /// <param name="names">Their names, such as "cushions and throw blanket".</param>
            public static string Decorated(string names)
            {
                return $", decorated with {names}";
            }

            /// <summary>The room's size as the photo sees it.</summary>
            /// <param name="across">From the left wall to the right wall, in cm.</param>
            /// <param name="deep">From the camera to the far wall, in cm.</param>
            /// <param name="height">From the floor to the ceiling, in cm.</param>
            public static string Room(string across, string deep, string height)
            {
                return $"The room is {across} cm wide from the left wall to the right wall, {deep} cm deep from the camera to the far wall, " +
                    $"and {height} cm from floor to ceiling.";
            }

            /// <summary>The walls (with those in their own finish, if any), the ceiling and its light, the trim, and the floor.</summary>
            /// <param name="walls">The walls' finish and color, such as "painted sage green (#8A9A84)" (see <see cref="FinishWords"/>).</param>
            /// <param name="ownWalls">
            /// The walls in their own finish, such as "the far wall, which is exposed brick in terracotta (#B5654A)", joined
            /// by <see cref="OwnWallsJoin"/>; empty for none.
            /// </param>
            /// <param name="ceiling">The ceiling's color.</param>
            /// <param name="light">The ceiling light in words (see <see cref="LightWords"/>).</param>
            /// <param name="trim">The skirting boards' color.</param>
            /// <param name="floor">The floor's material.</param>
            /// <param name="floorColor">The floor's color.</param>
            public static string Surfaces(string walls, string ownWalls, string ceiling, string light, string trim, string floor, string floorColor)
            {
                var except = ownWalls.Length == 0 ? "" : $", except {ownWalls}";

                return $"The walls are {walls}{except}. The ceiling is {ceiling} with {light}, the skirting boards are {trim}, " +
                    $"and the floor is {floor} in {floorColor}.";
            }

            /// <summary>A wall finish in words, said before its color: "painted", "exposed brick in"...</summary>
            /// <param name="finish">The finish.</param>
            public static string FinishWords(WallFinish finish)
            {
                return finish switch
                {
                    WallFinish.Wallpaper => "covered in patterned wallpaper in",
                    WallFinish.Plaster => "finished in softly mottled limewash plaster in",
                    WallFinish.Brick => "exposed brick in",
                    WallFinish.WoodPanelling => "clad in wooden wall panelling in",
                    WallFinish.Tiles => "tiled in",
                    WallFinish.Concrete => "bare concrete in",
                    _ => "painted",
                };
            }

            /// <summary>A wall in its own finish: which one, and its finish and color.</summary>
            /// <param name="wall">The wall as the photo sees it, such as "the far wall" (see <see cref="WallNames"/>).</param>
            /// <param name="look">Its finish and color, as <see cref="FinishWords"/> and a color.</param>
            public static string OwnWall(string wall, string look)
            {
                return $"{wall}, which is {look}";
            }

            /// <summary>What joins the walls in their own finishes: "except the far wall, which is..., and the left wall, which is...".</summary>
            public const string OwnWallsJoin = ", and ";

            /// <summary>The walls the photo sees, by name: the far wall, the left or right wall, or (rooms that aren't rectangles) a wall facing the camera.</summary>
            public static readonly (string Far, string Left, string Right, string Facing) WallNames =
                ("the far wall", "the left wall", "the right wall", "a wall facing the camera");

            /// <summary>A ceiling light in words.</summary>
            /// <param name="light">The light.</param>
            public static string LightWords(CeilingLight light)
            {
                return light switch
                {
                    CeilingLight.None => "no ceiling light",
                    CeilingLight.Spotlights => "recessed spotlights",
                    CeilingLight.Chandelier => "a chandelier hanging from its middle",
                    CeilingLight.FlushLight => "a flush round ceiling light",
                    CeilingLight.TrackLights => "black track lights",
                    _ => "a pendant lamp hanging from its middle",
                };
            }

            /// <summary>The photo's light and mood at a time of day.</summary>
            /// <param name="time">The time of day.</param>
            public static string Mood(TimeOfDay time)
            {
                return time switch
                {
                    TimeOfDay.Morning => "Soft, cool morning daylight comes in through the windows.",
                    TimeOfDay.GoldenHour => "Warm, low golden-hour sunlight streams in through the windows, casting long soft shadows.",
                    TimeOfDay.Evening => "It is evening: dark outside the windows, with the lamps and the ceiling light on, giving the room a warm, cosy glow.",
                    _ => "Bright, even daylight fills the room.",
                };
            }

            /// <summary>
            /// The style the photo is made in, as words for the image model: the details that make it (handles, legs,
            /// fronts, trims, hardware, upholstery), never colors (a style that named "light oak" turned a dark brown table
            /// light). Said only as materials and textures, most styles barely showed: every object kept its plain look.
            /// </summary>
            /// <param name="style">The style.</param>
            /// <returns>The sentence, or empty for no style.</returns>
            public static string Style(PhotoStyle style)
            {
                var words = style switch
                {
                    PhotoStyle.Modern => "modern: flat handleless fronts, sleek lacquered surfaces, thin metal legs and frames, crisp edges",
                    PhotoStyle.Scandinavian => "Scandinavian: rounded edges, solid wood legs and knobs, wool and linen upholstery, simple shapes",
                    PhotoStyle.Boho => "boho: rattan and cane panels, woven textures, macramé and fringes, turned wood",
                    PhotoStyle.Industrial => "industrial: steel frames and legs, exposed rivets and bolts, metal mesh, leather, reclaimed wood tops",
                    PhotoStyle.Japandi => "Japandi: low profiles, slatted wooden panels, paper and linen, rounded wooden handles, calm minimal details",
                    PhotoStyle.MidCentury => "mid-century modern: tapered splayed wooden legs, rounded corners, recessed finger-pull handles, tufted upholstery",
                    PhotoStyle.Farmhouse => "farmhouse: shaker panel fronts, cup pull handles, wrought iron hardware, distressed wood, turned legs",
                    PhotoStyle.Coastal => "coastal: rope and rattan details, louvred fronts, weathered wood, linen slipcovers",
                    PhotoStyle.Minimalist => "minimalist: perfectly flat handleless fronts, hidden legs, seamless surfaces, nothing extra",
                    PhotoStyle.Traditional => "traditional: raised-panel fronts, carved trims and mouldings, turned legs, ornate knobs, tufted upholstery",
                    PhotoStyle.ArtDeco => "Art Deco: fluted and ribbed panels, geometric inlays, polished metal handles and trims, curved velvet upholstery",
                    _ => "",
                };

                return words.Length == 0
                    ? ""
                    : $"Style every object as {words}. Give each object the handles, legs, fronts, trims and textures of this style, " +
                      "so the style is clearly visible, while it keeps its place, outline and size. Each object's own color comes " +
                      "first: where the style's usual materials or finishes would change it, the object keeps its color and takes " +
                      "only the style's shapes and details. Appliances are not styled.";
            }

            /// <summary>
            /// The words in an object's name that make it an appliance, which the style leaves alone (a style made a fridge
            /// rattan, louvred or riveted).
            /// </summary>
            public static readonly string[] ApplianceWords =
                ["fridge", "freezer", "cooker", "oven", "hob", "dishwasher", "washing machine", "dryer", "microwave", "extractor"];

            /// <summary>
            /// Right after the style: the appliances in view by name, which stay plain. A general rule ("appliances are not
            /// styled") left a fridge riveted in the industrial style; naming the room's own appliances is clearer.
            /// </summary>
            /// <param name="names">The appliances, such as "the fridge and the cooker".</param>
            /// <param name="count">How many there are.</param>
            public static string PlainAppliances(string names, int count)
            {
                return count == 1 ? $"Whatever the style, {names} stays a plain, modern appliance." : $"Whatever the style, {names} stay plain, modern appliances.";
            }

            /// <summary>
            /// Right after the style: each object in view with the color it keeps. A general rule ("each object's own color
            /// comes first") still let the style turn a white cabinet into metal or a light grey wardrobe into wood.
            /// </summary>
            /// <param name="list">Each object with its color, such as "the tall cabinet stays white, the island stays slate blue".</param>
            public static string KeptColors(string list)
            {
                return $"Whatever the style, {list}.";
            }

            /// <summary>
            /// A picture (a painting, mirror or wall TV) with its color, which is its frame's: "painting in a black (#2F2C2A)
            /// frame". Named "black painting", the image model painted the canvas black.
            /// </summary>
            /// <param name="label">Its name.</param>
            /// <param name="color">Its color.</param>
            public static string Framed(string label, string color)
            {
                return $"{label} in a {color} frame";
            }

            /// <summary>The part of a picture whose color it is, for <see cref="KeepsColor"/>: "painting's frame".</summary>
            /// <param name="label">The picture's name.</param>
            public static string FrameOf(string label)
            {
                return $"{label}'s frame";
            }

            /// <summary>One object keeping its color, for <see cref="KeptColors"/>.</summary>
            /// <param name="label">The object's name.</param>
            /// <param name="color">Its color in words.</param>
            public static string KeepsColor(string label, string color)
            {
                return $"the {label} stays {color}";
            }

            /// <summary>After an appliance's description: it stays a plain, modern appliance whatever the style.</summary>
            public const string Appliance = ", a plain modern appliance in this color, not styled";

            /// <summary>
            /// The user's own extra instructions (such as the light), right after the style: said at the end of the
            /// prompt, "warm evening light" was ignored.
            /// </summary>
            /// <param name="instructions">Their words.</param>
            public static string Extra(string instructions)
            {
                return $"The photo must also show: {instructions}.";
            }

            /// <summary>
            /// Said last, after the objects: every color stays as listed (a white bookcase came out in wood). It names no
            /// material: a version that said "even where the style suggests wood" turned a white wardrobe and a beige
            /// headboard into wood.
            /// </summary>
            public const string KeepColors =
                "Every object keeps exactly the color listed for it and shown in the render.";

            /// <summary>Where the camera is.</summary>
            public const string Camera = "The camera stands at the middle of the near wall at eye height, looking straight at the far wall.";

            /// <summary>Where the camera is when it stands at the spot the photo the plan was read from was taken.</summary>
            /// <param name="fromLeft">How far from the left wall, in cm.</param>
            /// <param name="fromFar">How far from the far wall, in cm.</param>
            public static string SpotCamera(string fromLeft, string fromFar)
            {
                return $"The camera stands {fromLeft} cm from the left wall and {fromFar} cm from the far wall at eye height, looking towards the middle of the far wall.";
            }

            /// <summary>
            /// Where the camera is when it stands in a near corner, and which wall shows on which side of the photo. The
            /// walls keep the names they have seen from the middle of the near wall, which every distance uses.
            /// </summary>
            /// <param name="left">Whether it stands in the near left corner (else the near right one).</param>
            public static string CornerCamera(bool left)
            {
                var (stands, looks, onLeft, onRight) = left
                    ? ("left", "right", "far wall", "right wall")
                    : ("right", "left", "left wall", "far wall");

                return $"The camera stands in the near {stands} corner at eye height, looking diagonally across the room towards " +
                    $"the far {looks} corner, so the {onLeft} fills the left of the photo and the {onRight} its right. The left, " +
                    "right, near and far walls below are named as seen from the middle of the near wall, not by the photo's sides.";
            }

            /// <summary>Before the objects on the floor.</summary>
            public const string ObjectsHeading =
                "In the photo (distances in cm to each object's middle; sizes in cm left to right × front to back × high): ";

            /// <summary>An object on the floor, its color first (the image model kept colors better so).</summary>
            /// <param name="label">Its name.</param>
            /// <param name="color">Its color.</param>
            /// <param name="fromLeft">Its middle's distance from the left wall.</param>
            /// <param name="fromFar">Its middle's distance from the far wall.</param>
            /// <param name="size">Its size.</param>
            public static string Standing(string label, string color, string fromLeft, string fromFar, string size)
            {
                return $"{color} {label}: middle {fromLeft} cm from the left wall and {fromFar} cm from the far wall, {size}";
            }

            /// <summary>An object or opening the camera sees only part of.</summary>
            /// <param name="edges">The photo's edges it crosses, such as "left" and "bottom".</param>
            public static string CutBy(IReadOnlyList<string> edges)
            {
                return $", partly cut off by the {string.Join(" and ", edges)} edge{(edges.Count > 1 ? "s" : "")} of the photo";
            }

            /// <summary>The photo's left edge.</summary>
            public const string LeftEdge = "left";
            /// <summary>The photo's right edge.</summary>
            public const string RightEdge = "right";
            /// <summary>The photo's bottom edge.</summary>
            public const string BottomEdge = "bottom";
            /// <summary>The photo's top edge.</summary>
            public const string TopEdge = "top";

            /// <summary>An object's size.</summary>
            /// <param name="across">Left to right.</param>
            /// <param name="deep">Front to back.</param>
            /// <param name="height">How high.</param>
            public static string Size(string across, string deep, string height)
            {
                return $"{across} × {deep} × {height} cm";
            }

            /// <summary>An L-shaped object's outline: its back run and its corner leg.</summary>
            /// <param name="run">How deep the back run is.</param>
            /// <param name="leg">How wide the corner leg is.</param>
            /// <param name="fromLeft">The leg's middle's distance from the left wall.</param>
            /// <param name="fromFar">The leg's middle's distance from the far wall.</param>
            public static string LShaped(string run, string leg, string fromLeft, string fromFar)
            {
                return $", L-shaped (its back run, across its whole width, is {run} cm deep; its corner leg is {leg} cm wide, its middle " +
                    $"{fromLeft} cm from the left wall and {fromFar} cm from the far wall)";
            }

            /// <summary>A P-shaped object's outline: an L whose leg widens into a chaise at its front end.</summary>
            /// <param name="run">How deep the back run is.</param>
            /// <param name="leg">How wide the corner leg is.</param>
            /// <param name="legLeft">The leg's middle's distance from the left wall.</param>
            /// <param name="legFar">The leg's middle's distance from the far wall.</param>
            /// <param name="chaiseWidth">How wide the chaise is.</param>
            /// <param name="chaiseDepth">How deep the chaise is.</param>
            /// <param name="chaiseLeft">The chaise's middle's distance from the left wall.</param>
            /// <param name="chaiseFar">The chaise's middle's distance from the far wall.</param>
            public static string PShaped(string run, string leg, string legLeft, string legFar, string chaiseWidth, string chaiseDepth, string chaiseLeft, string chaiseFar)
            {
                return $", P-shaped (its back run, across its whole width, is {run} cm deep; its corner leg is {leg} cm wide, its middle {legLeft} cm " +
                    $"from the left wall and {legFar} cm from the far wall; at its front end the leg widens into a chaise {chaiseWidth} × {chaiseDepth} cm, " +
                    $"its middle {chaiseLeft} cm from the left wall and {chaiseFar} cm from the far wall)";
            }

            /// <summary>A round object's outline.</summary>
            public const string RoundForm = ", round seen from above";

            /// <summary>An oval object's outline.</summary>
            public const string OvalForm = ", oval seen from above";

            /// <summary>A flat object's size (a rug).</summary>
            /// <param name="across">Left to right.</param>
            /// <param name="deep">Front to back.</param>
            public static string FlatSize(string across, string deep)
            {
                return $"{across} × {deep} cm, flat on the floor";
            }

            /// <summary>An object whose front faces the camera.</summary>
            public const string FacingCamera = "facing the camera";
            /// <summary>An object whose front faces the far wall.</summary>
            public const string FacingAway = "facing the far wall";
            /// <summary>An object whose front faces right.</summary>
            public const string FacingRight = "facing right";
            /// <summary>An object whose front faces left.</summary>
            public const string FacingLeft = "facing left";
            /// <summary>An object whose front faces the near wall (a corner view, where "the camera" is to one side).</summary>
            public const string FacingNearWall = "facing the near wall";
            /// <summary>An object whose front faces the right wall (a corner view).</summary>
            public const string FacingRightWall = "facing the right wall";
            /// <summary>An object whose front faces the left wall (a corner view).</summary>
            public const string FacingLeftWall = "facing the left wall";

            /// <summary>Before the windows and doors.</summary>
            public const string OpeningsHeading = "Windows and doors, and no others: ";

            /// <summary>When no window or door is in view.</summary>
            public const string NoOpenings = "There are no windows or doors in view.";

            /// <summary>Where a window or door on the far wall is.</summary>
            /// <param name="fromLeft">Its middle's distance from the left wall.</param>
            public static string OnFarWall(string fromLeft)
            {
                return $"on the far wall, its middle {fromLeft} cm from the left wall";
            }

            /// <summary>Where a window or door on a side wall is.</summary>
            /// <param name="left">Whether it is the left wall.</param>
            /// <param name="fromFar">Its middle's distance from the far wall.</param>
            public static string OnSideWall(bool left, string fromFar)
            {
                return $"on the {(left ? "left" : "right")} wall, its middle {fromFar} cm from the far wall";
            }

            /// <summary>A window or door.</summary>
            /// <param name="label">Its name.</param>
            /// <param name="color">Its color.</param>
            /// <param name="where">Which wall, and where along it.</param>
            /// <param name="width">Its width.</param>
            /// <param name="height">Its height.</param>
            /// <param name="bottom">Its bottom's height above the floor.</param>
            public static string Opening(string label, string color, string where, string width, string height, string bottom)
            {
                return $"{color} {label}, {where}, {width} cm wide and {height} cm high, its bottom {bottom} cm above the floor";
            }

            /// <summary>The list of things hung on the walls in view.</summary>
            public const string MountedHeading = "Hung on the walls, and nothing else: ";

            /// <summary>A thing hung on a wall: a painting, a mirror, a wall TV, an upper cabinet, a shelf or a hood.</summary>
            /// <param name="name">It in words, with its color (see <see cref="Framed"/> for pictures).</param>
            /// <param name="where">Which wall, and where along it.</param>
            /// <param name="width">Its width.</param>
            /// <param name="height">Its height.</param>
            /// <param name="bottom">Its bottom's height above the floor.</param>
            /// <param name="depth">How far it sticks out from the wall.</param>
            public static string Mounted(string name, string where, string width, string height, string bottom, string depth)
            {
                return $"{name}, {where}, {width} cm wide and {height} cm high, its bottom {bottom} cm above the floor, sticking out {depth} cm";
            }

            /// <summary>Where a window, door or hung thing is on a wall facing the camera, in front of the far wall (rooms that aren't rectangles).</summary>
            /// <param name="fromFar">The wall's distance in front of the far wall.</param>
            /// <param name="fromLeft">Its middle's distance from the left wall.</param>
            public static string OnStepWall(string fromFar, string fromLeft)
            {
                return $"on the wall facing the camera {fromFar} cm in front of the far wall, its middle {fromLeft} cm from the left wall";
            }

            /// <summary>Where a window, door or hung thing is on a side wall of a room that isn't a rectangle.</summary>
            /// <param name="left">Whether the wall is on the photo's left.</param>
            /// <param name="wallFromLeft">The wall's distance from the left wall.</param>
            /// <param name="fromFar">Its middle's distance from the far wall.</param>
            public static string OnSideWallAt(bool left, string wallFromLeft, string fromFar)
            {
                return $"on the {(left ? "left-hand" : "right-hand")} wall {wallFromLeft} cm from the left wall, its middle {fromFar} cm from the far wall";
            }

            /// <summary>The walls of a room that isn't a rectangle, besides the far wall, as the photo sees them.</summary>
            /// <param name="walls">Each wall in words.</param>
            public static string Shape(string walls)
            {
                return $"The room is not a plain rectangle; besides the far wall, the walls in view are: {walls}.";
            }

            /// <summary>A wall facing the camera in front of the far wall.</summary>
            /// <param name="fromFar">Its distance in front of the far wall.</param>
            /// <param name="from">Where it starts, from the left wall.</param>
            /// <param name="to">Where it ends, from the left wall.</param>
            public static string StepWall(string fromFar, string from, string to)
            {
                return $"a wall facing the camera {fromFar} cm in front of the far wall, from {from} to {to} cm from the left wall";
            }

            /// <summary>A side wall of a room that isn't a rectangle.</summary>
            /// <param name="left">Whether it faces right (it is on the photo's left side of the floor).</param>
            /// <param name="fromLeft">Its distance from the left wall.</param>
            /// <param name="from">Where it starts, from the far wall.</param>
            /// <param name="to">Where it ends, from the far wall.</param>
            public static string SideWall(bool left, string fromLeft, string from, string to)
            {
                return $"a {(left ? "left-hand" : "right-hand")} wall {fromLeft} cm from the left wall, from {from} to {to} cm from the far wall";
            }

            /// <summary>An area of the floor in its own material and color.</summary>
            /// <param name="floor">Its material in words (see <see cref="FloorWords"/>).</param>
            /// <param name="color">Its color in words.</param>
            /// <param name="across">How wide it is, as the photo sees it, in cm.</param>
            /// <param name="deep">How deep it is, in cm.</param>
            /// <param name="fromLeft">Its middle's distance from the left wall, in cm.</param>
            /// <param name="fromFar">Its middle's distance from the far wall, in cm.</param>
            public static string FloorZone(string floor, string color, string across, string deep, string fromLeft, string fromFar)
            {
                return $"Part of the floor is {floor} in {color}: an area {across} cm wide and {deep} cm deep, its middle {fromLeft} cm " +
                    $"from the left wall and {fromFar} cm from the far wall, flush with the rest of the floor.";
            }

            /// <summary>An area of a wall in its own finish and color.</summary>
            /// <param name="where">Where its middle is, such as "on the far wall, its middle 120 cm from the left wall".</param>
            /// <param name="look">Its finish and color, such as "exposed brick in terracotta (#B5654A)".</param>
            /// <param name="width">Its width, in cm.</param>
            /// <param name="bottom">How high its bottom is, in cm.</param>
            /// <param name="top">How high its top is, in cm.</param>
            public static string WallZone(string where, string look, string width, string bottom, string top)
            {
                return $"Part of a wall, {where}, is {look}: an area {width} cm wide from {bottom} cm to {top} cm above the floor, flat on the wall.";
            }

            /// <summary>The words for a thing a wall of the room partly hides from the camera.</summary>
            public const string PartlyHidden = ", partly hidden behind a corner of the room";

            /// <summary>A floor material in words.</summary>
            /// <param name="type">The floor type.</param>
            public static string FloorWords(FloorType type)
            {
                return type switch
                {
                    FloorType.Parquet => "herringbone wooden parquet",
                    FloorType.Tiles => "ceramic floor tiles",
                    FloorType.Stone => "large stone slabs",
                    FloorType.Concrete => "polished concrete",
                    FloorType.Carpet => "wall-to-wall carpet",
                    FloorType.Vinyl => "vinyl plank flooring",
                    _ => "wooden plank flooring",
                };
            }
        }
    }

    /// <summary>
    /// A room's 360-degree photo: one equirectangular picture from one spot, whose views are cut out of it, so every
    /// view of the room shows exactly the same objects and decorations.
    /// </summary>
    public static class Panoramas
    {
        /// <summary>The photo's width, twice its height: 4K, sharp enough for 1024-pixel views.</summary>
        public const int Width = 4096;

        /// <summary>The photo's height.</summary>
        public const int Height = 2048;

        /// <summary>The largest 360-degree view the browser may send (a 4096 x 2048 PNG).</summary>
        public const int MaxGuideBytes = 32 * 1024 * 1024;

        /// <summary>The width of a view cut from the photo.</summary>
        public const int ViewWidth = 1024;

        /// <summary>The height of a view cut from the photo.</summary>
        public const int ViewHeight = 768;

        /// <summary>The tilt of the first view, the room's photo: a little down, as a photographer holds the camera.</summary>
        public const double FrontPitch = -8;

        /// <summary>The lens of the first view, across its height, in degrees.</summary>
        public const double FrontFov = 60;

        /// <summary>The narrowest lens a saved view may have, across its height.</summary>
        public const double MinFov = 35;

        /// <summary>The widest lens a saved view may have.</summary>
        public const double MaxFov = 90;

        /// <summary>The furthest a saved view may turn either way from the photo's middle, before its own width.</summary>
        public const double MaxYaw = 180;

        /// <summary>How many degrees a view's edge keeps from the seam behind, where the photo's ends meet.</summary>
        public const double SeamMargin = 4;

        /// <summary>The furthest a saved view may tilt up or down (a wide lens tilts less; see PanoramaView.MaxTilt).</summary>
        public const double MaxPitch = 80;

        /// <summary>Straight up (and down), in degrees of tilt: where every point of the 360-degree photo's seam meets.</summary>
        public const double StraightUp = 90;

        /// <summary>
        /// How far past its limits a saved view may still be, in degrees: the browser works out the same limits, with
        /// its own rounding, and a view right at them must be accepted.
        /// </summary>
        public const double Rounding = 0.01;

        /// <summary>The note of the first view.</summary>
        public const string FrontNote = "360° view, straight ahead";

        /// <summary>The note of a saved view: which way it looks from the photo's middle.</summary>
        /// <param name="yaw">The turn, in degrees; right is positive.</param>
        public static string ViewNote(double yaw)
        {
            var turn = (int)Math.Round(yaw);

            return turn == 0 ? FrontNote : $"360° view, {Math.Abs(turn)}° {(turn > 0 ? "right" : "left")}";
        }

        /// <summary>The wording of the 360-degree photo's prompt (see <c>Helpers/PanoramaPrompt</c>).</summary>
        public static class Prompt
        {
            /// <summary>What to make: the same projection, every wall and object where the render has it.</summary>
            /// <param name="room">The room type in words.</param>
            public static string Intro(string room)
            {
                return $"Turn this 360-degree equirectangular panorama, a simple 3D render of a {room} seen from where a person " +
                    "stands in the room at eye height, into a realistic 360-degree equirectangular panoramic photograph of the same " +
                    "interior. Keep the equirectangular projection exactly as in the render: the full 360-degree horizontal sweep " +
                    "and 180-degree vertical sweep, the ceiling stretched across the top and the floor across the bottom, every wall " +
                    "and corner exactly where it is in the render. Turn each plain shape into the real object it stands for, keeping " +
                    "its place, outline, size and exactly the color it has in the render.";
            }

            /// <summary>The room's size.</summary>
            /// <param name="width">Its width, in cm.</param>
            /// <param name="length">Its length, in cm.</param>
            /// <param name="height">Its height, in cm.</param>
            public static string Room(string width, string length, string height)
            {
                return $"The room is {width} cm by {length} cm, and {height} cm from floor to ceiling.";
            }

            /// <summary>
            /// A wall in its own finish, named by where it is in the panorama (it has no near or far wall): its middle faces
            /// the back wall, its ends meet at the front wall.
            /// </summary>
            /// <param name="side">The side of the room the wall is on.</param>
            public static string OwnWall(PlanWall side)
            {
                return side switch
                {
                    PlanWall.Back => "the wall in the middle of the panorama",
                    PlanWall.Right => "the wall to the right of the middle",
                    PlanWall.Front => "the wall at both ends of the panorama",
                    _ => "the wall to the left of the middle",
                };
            }

            /// <summary>That nothing is added, and every color is kept.</summary>
            public const string Contents =
                "Every object keeps exactly the color it has in the render and the color named for it below. Everything in the " +
                "photo is already in the render, apart from the decorations named with their objects below, each resting on or " +
                "hanging at its own object: keep the rest of the room bare.";

            /// <summary>The objects, each with its color, size and decorations.</summary>
            /// <param name="objects">The objects, numbered.</param>
            public static string Objects(string objects)
            {
                return $"The objects: {objects}.";
            }

            /// <summary>The areas of the floor and walls in their own materials, drawn as such in the render.</summary>
            /// <param name="areas">The areas.</param>
            public static string Areas(string areas)
            {
                return $"Parts of the floor and walls have their own material, exactly where the render shows them, flat and flush with the rest: {areas}.";
            }

            /// <summary>An area of the floor.</summary>
            /// <param name="floor">Its material in words.</param>
            /// <param name="color">Its color in words.</param>
            /// <param name="width">Its width, in cm.</param>
            /// <param name="depth">Its depth, in cm.</param>
            public static string FloorArea(string floor, string color, string width, string depth)
            {
                return $"an area of the floor, {width} by {depth} cm, in {floor} in {color}";
            }

            /// <summary>An area of a wall.</summary>
            /// <param name="look">Its finish and color, such as "exposed brick in terracotta (#B5654A)".</param>
            /// <param name="width">Its width, in cm.</param>
            /// <param name="height">Its height, in cm.</param>
            public static string WallArea(string look, string width, string height)
            {
                return $"an area of a wall, {width} cm wide and {height} cm high, {look}";
            }

            /// <summary>One object's width, after its description.</summary>
            /// <param name="width">Its width, in cm.</param>
            public static string Wide(string width)
            {
                return $", {width} cm wide";
            }
        }
    }

    /// <summary>Limits for the parts the AI finds on an object.</summary>
    public static class Parts
    {
        /// <summary>The most parts kept for one object (each is a SAM 3 request with the real AI).</summary>
        public const int MaxParts = 6;

        /// <summary>The longest instructions a user may give the AI for finding parts.</summary>
        public const int MaxInstructionsLength = 500;
    }

    /// <summary>Limits for an object the user marks by hand.</summary>
    public static class Marks
    {
        /// <summary>The most strokes one mark may have.</summary>
        public const int MaxStrokes = 100;

        /// <summary>The longest name a hand-marked object may have.</summary>
        public const int MaxLabelLength = 60;
    }

    /// <summary>Limits that every uploaded photo must meet, and how it is stored.</summary>
    public static class Photos
    {
        /// <summary>One mebibyte, for limits given in MB.</summary>
        public const long Megabyte = 1024 * 1024;

        /// <summary>The largest upload in MB. Matches the frontend's limit; large enough for any phone photo.</summary>
        public const int MaxUploadMegabytes = 15;

        /// <summary>The largest upload in bytes.</summary>
        public const long MaxUploadBytes = MaxUploadMegabytes * Megabyte;

        /// <summary>Below this the AI cannot find objects reliably.</summary>
        public const int MinShortEdge = 256;

        /// <summary>Larger photos are scaled down: more pixels cost more to analyze without improving results.</summary>
        public const int MaxLongEdge = 4096;

        /// <summary>The most megapixels an upload may have; refused before decoding, so a small file can't exhaust memory.</summary>
        public const int MaxSourceMegapixels = 100;

        /// <summary>The most pixels an upload may have.</summary>
        public const long MaxSourcePixels = MaxSourceMegapixels * 1_000_000L;

        /// <summary>JPEG quality (0-100) for stored photos: visually lossless at a reasonable size.</summary>
        public const int JpegQuality = 90;
    }

    /// <summary>MIME types of the files the app stores and serves.</summary>
    public static class ContentTypes
    {
        /// <summary>Photos: processed photos are always JPEG.</summary>
        public const string Jpeg = "image/jpeg";

        /// <summary>Label maps.</summary>
        public const string Png = "image/png";
    }

    /// <summary>How rooms are stored and kept.</summary>
    public static class Rooms
    {
        /// <summary>How long an upload is kept without being saved to a design: long enough to edit it, reload, or sign in.</summary>
        public static readonly TimeSpan UnsavedLifetime = TimeSpan.FromHours(24);

        /// <summary>How many expired rooms one cleanup pass deletes, so a big backlog is cleared in steps.</summary>
        public const int CleanupBatchSize = 100;

        /// <summary>Storage key of a room's photo. One folder per room keeps its files together.</summary>
        /// <param name="roomId">The room's id.</param>
        /// <param name="version">The photo version: 0 is the uploaded photo, then one per object removed from it.</param>
        public static string PhotoKey(Guid roomId, int version = 0)
        {
            return version == 0 ? $"rooms/{roomId}/photo.jpg" : $"rooms/{roomId}/photo-{version}.jpg";
        }

        /// <summary>Storage key of a room's 360-degree photo, in the room's folder.</summary>
        /// <param name="roomId">The room's id.</param>
        public static string PanoramaKey(Guid roomId)
        {
            return $"rooms/{roomId}/panorama.jpg";
        }

        /// <summary>Storage key of one of a room's photos to choose from, in the room's folder.</summary>
        /// <param name="roomId">The room's id.</param>
        /// <param name="takeId">The photo's id.</param>
        public static string TakeKey(Guid roomId, Guid takeId)
        {
            return $"rooms/{roomId}/takes/{takeId}.jpg";
        }

        /// <summary>Storage key of a room's label map, as the analysis made it or after outline fixes.</summary>
        /// <param name="roomId">The room's id.</param>
        /// <param name="revision">The label map version: 0 from the analysis, then one per fix.</param>
        public static string LabelMapKey(Guid roomId, int revision = 0)
        {
            return revision == 0 ? $"rooms/{roomId}/labels.png" : $"rooms/{roomId}/labels-{revision}.png";
        }
    }

    /// <summary>The built-in library of ready-made designs.</summary>
    public static class Library
    {
        /// <summary>How a copied room names the library layout it came from, such as <c>minimalist/kitchen/2</c>.</summary>
        /// <param name="designId">The library design's id.</param>
        /// <param name="roomId">The library room's id.</param>
        /// <param name="layoutId">The layout's id.</param>
        public static string RoomName(string designId, string roomId, string layoutId)
        {
            return $"{designId}/{roomId}/{layoutId}";
        }
    }

    /// <summary>Owner ids of visitors who haven't signed in.</summary>
    public static class Guests
    {
        /// <summary>Starts every guest id, keeping them apart from account ids.</summary>
        public const string Prefix = "guest:";

        /// <summary>How a guest's UUID is written: 32 hex digits, no dashes.</summary>
        public const string TokenFormat = "N";
    }

    /// <summary>The messages the use cases show to users.</summary>
    public static class Messages
    {
        /// <summary>An edit names a paint no palette has.</summary>
        /// <param name="paint">The paint reference.</param>
        public static string UnknownPaint(string paint)
        {
            return $"There is no paint '{paint}'.";
        }

        /// <summary>An edit names a paint but has another color.</summary>
        /// <param name="code">The paint's code, such as RAL 9010.</param>
        public static string PaintColorMismatch(string code)
        {
            return $"The color doesn't match {code}. Pick the paint again.";
        }

        /// <summary>The analysis failed unexpectedly. The details are only logged.</summary>
        public const string UnexpectedFailure = "We couldn't analyze this photo. Try uploading it again in a moment.";

        /// <summary>The analysis found no objects worth restyling.</summary>
        public const string NothingFound = "We couldn't find any objects to restyle in this photo. Try a wider shot of the room.";

        /// <summary>The editor is on an outline version that is no longer current (another tab changed it).</summary>
        public const string UnknownRevision = "This room has changed since you opened it. Reload the page and try again.";

        /// <summary>A stroke on a room that isn't analyzed yet.</summary>
        public const string OutlinesNotReady = "The outlines can be fixed once the analysis is ready.";

        /// <summary>A stroke for an object the room doesn't have.</summary>
        public const string UnknownStrokeObject = "That object isn't in this room.";

        /// <summary>A stroke with too few or too many points.</summary>
        public static readonly string StrokePoints = $"A stroke needs between 1 and {Strokes.MaxPoints} points.";

        /// <summary>A brush that is too small or too large.</summary>
        public static readonly string BrushRadius = $"The brush radius must be between {Strokes.MinRadius} and {Strokes.MaxRadius} pixels.";

        /// <summary>A stroke far outside the photo.</summary>
        public const string StrokeOutside = "The stroke has points far outside the photo.";

        /// <summary>A box stroke without exactly two corners.</summary>
        public const string BoxPoints = "A box needs its two corners.";

        /// <summary>A lasso stroke with too few points to enclose anything.</summary>
        public const string LassoPoints = "A lasso needs at least 3 points.";

        /// <summary>A stroke with a shape that doesn't exist.</summary>
        public const string UnknownStrokeShape = "Pick the brush, the box or the lasso.";

        /// <summary>A new object with too few or too many strokes.</summary>
        public static readonly string MarkStrokes = $"Mark the object with between 1 and {Marks.MaxStrokes} strokes.";

        /// <summary>A new object without a usable name.</summary>
        public static readonly string ObjectName = $"Give the object a name of up to {Marks.MaxLabelLength} characters.";

        /// <summary>A new object with a type that doesn't exist.</summary>
        public const string UnknownObjectType = "Pick the object's type from the list.";

        /// <summary>A removal with no objects or too many.</summary>
        public static readonly string RemovalCount = $"Pick between 1 and {Removals.MaxObjects} objects to remove.";

        /// <summary>Removing an object that has no pixels in the outlines shown.</summary>
        public const string ObjectNotInPhoto = "That object isn't in this version of the photo.";

        /// <summary>Removing a wall, floor or ceiling, which can only be recolored.</summary>
        public const string SurfaceRemoval = "Walls, floors and ceilings can't be removed. Recolor them instead.";

        /// <summary>A part for an object the room doesn't have, or for a part (parts have no parts).</summary>
        public const string PartParent = "A part belongs to a whole object in this room, not to another part.";

        /// <summary>Removing a part on its own.</summary>
        /// <param name="label">The part.</param>
        /// <param name="parent">The object it is a part of.</param>
        public static string PartRemoval(string label, string parent)
        {
            return $"The {label} is a part of the {parent}. Merge it back, or remove the whole {parent}.";
        }

        /// <summary>A file the room needs isn't in storage (it was stored when the room was made or changed).</summary>
        /// <param name="key">Path of the file in storage.</param>
        public static string MissingFile(string key)
        {
            return $"The file {key} is missing from storage.";
        }

        /// <summary>A room's photo isn't in storage.</summary>
        /// <param name="roomId">The room.</param>
        public static string MissingPhoto(Guid roomId)
        {
            return $"The photo of room {roomId} is missing from storage.";
        }

        /// <summary>The AI found no parts on an object.</summary>
        /// <param name="label">The object.</param>
        public static string NoParts(string label)
        {
            return $"The AI found no separate parts on the {label}.";
        }

        /// <summary>Instructions for finding parts that are too long.</summary>
        public static readonly string PartsInstructions = $"Keep the instructions for the AI to {Parts.MaxInstructionsLength} characters.";

        /// <summary>Looking for parts of a part.</summary>
        /// <param name="parent">The object it is a part of.</param>
        public static string PartsOfPart(string parent)
        {
            return $"Look for parts on the whole {parent} instead.";
        }

        /// <summary>Starting the analysis of a room that isn't waiting for it.</summary>
        public const string NotWaiting = "This room's analysis has already started.";

        /// <summary>Merging an object that isn't a part.</summary>
        public const string NotAPart = "Only a part can be merged back into its object.";

        /// <summary>A description of an object that is empty or too long.</summary>
        public static readonly string ObjectDescription = $"Describe the object in up to {Builder.MaxDescriptionLength} characters.";

        /// <summary>Extra instructions for the photo that are too long.</summary>
        public static readonly string PhotoInstructions = $"Keep the extra instructions for the photo to {Builder.MaxInstructionsLength} characters.";

        /// <summary>Object boxes that aren't on the plan's objects or inside the picture.</summary>
        public const string PhotoBoxes = "Each object box must name one of the room's objects and lie inside the picture.";

        /// <summary>Object masks that are too large.</summary>
        public static readonly string PhotoMasks = $"The object masks must be a PNG of at most {Builder.MaxMasksBytes / (1024 * 1024)} MB.";

        /// <summary>A photo asked for from a wall the room doesn't have, or a camera spot that isn't one of the choices.</summary>
        public const string PhotoShot = "Choose one of the room's walls and where the photo is taken from.";

        /// <summary>A photo seed below zero.</summary>
        public const string PhotoSeed = "A photo's seed is a whole number from 0 up.";

        /// <summary>A photo style that isn't one of the choices.</summary>
        public const string UnknownPhotoStyle = "Choose one of the styles.";

        /// <summary>A 3D view for a photo that is missing or too large.</summary>
        public const string GuideImage = "The 3D view of the room didn't arrive. Try again.";

        /// <summary>A change to a photo without words, or with too many.</summary>
        public static readonly string ChangeWords = $"Describe the change in 1 to {Builder.MaxInstructionsLength} characters.";

        /// <summary>A note for a photo that is too long.</summary>
        public static readonly string PhotoNote = $"A photo's note is at most {Domain.Constants.Rooms.MaxTakeNoteLength} characters.";

        /// <summary>A photo added to a room that holds another plan's photos (or none).</summary>
        public const string NotThisPlansRoom = "That room doesn't hold this room plan's photos.";

        /// <summary>New photos meant to replace a room that is gone (replaced already, or deleted).</summary>
        public const string ReplacedRoomMissing = "The room these photos were to replace is gone. Open the room builder again from the design to choose.";

        /// <summary>A 360-degree view that isn't a PNG twice as wide as it is tall, or is too big.</summary>
        public static readonly string PanoramaGuide = $"The 360° view must be a PNG twice as wide as it is tall, at most {Panoramas.MaxGuideBytes / (1024 * 1024)} MB.";

        /// <summary>A 360-degree camera standing outside the room's walls.</summary>
        public const string PanoramaSpot = "The 360° camera must stand inside the room's walls.";

        /// <summary>A 360-degree photo whose first view would face the seam behind.</summary>
        public static readonly string PanoramaStart = $"The 360° view must start at most {Domain.Constants.Rooms.MaxPanoramaStartYaw:0}° from its middle.";

        /// <summary>A 360-degree photo the image model sent that can't be read.</summary>
        public const string PanoramaPicture = "The 360° photo the AI made couldn't be read. Try again.";

        /// <summary>A view of a room that has no 360-degree photo.</summary>
        public const string NoPanorama = "This room has no 360° photo.";

        /// <summary>A view turned, tilted or zoomed beyond what can be saved.</summary>
        public static readonly string PanoramaView =
            $"A saved view must keep clear of the seam behind and of straight up and down, and have a lens of {Panoramas.MinFov:0}° to {Panoramas.MaxFov:0}°.";

        /// <summary>A room saved to its plan's design that wasn't made from a plan, or whose plan is gone.</summary>
        public const string NotFromPlan = "This room wasn't made from a room plan that still exists.";

        /// <summary>A room built for a design that isn't one of the user's (or is gone).</summary>
        public const string PlanDesignMissing = "That design wasn't found: it may have been deleted. Open it from My designs and try again.";

        /// <summary>A room built for a design that already holds as many rooms as a design can.</summary>
        /// <param name="design">The design's name.</param>
        public static string PlanDesignAlreadyFull(string design)
        {
            return $"\"{design}\" is full. {Domain.Constants.Messages.DesignFull}";
        }

        /// <summary>The plan's design has no room left for the photos' room.</summary>
        /// <param name="design">The design's name.</param>
        public static string PlanDesignFull(string design)
        {
            return $"\"{design}\" is full, so these photos weren't added to it. {Domain.Constants.Messages.DesignFull}";
        }

        /// <summary>A photo asked for a built room without objects.</summary>
        public const string EmptyPlan = "Put at least one object in the room first.";

        /// <summary>A room that has used up its object numbers.</summary>
        public const string TooManyObjects = "This room can't hold any more objects.";

        /// <summary>A room that is in a design already (a room belongs to one design).</summary>
        public const string RoomInADesign = "This room is already in a design.";
    }

    /// <summary>Whether a brush stroke adds pixels to the object or takes them away.</summary>
    public enum OutlineBrushMode
    {
        /// <summary>Every covered pixel becomes the object's, even if another object had it.</summary>
        Paint,
        /// <summary>Covered pixels of the object become unassigned; other objects are untouched.</summary>
        Erase,
    }

    /// <summary>What a stroke covers: a round brush along its path, or the area its points enclose.</summary>
    public enum StrokeShape
    {
        /// <summary>A round brush stamped along the path (the radius sets its size).</summary>
        Brush,
        /// <summary>A rectangle between two opposite corners; the radius is ignored.</summary>
        Box,
        /// <summary>The inside of the path, closed from the last point back to the first (even-odd rule); the radius is ignored.</summary>
        Lasso,
    }
}

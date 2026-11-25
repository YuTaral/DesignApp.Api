namespace DesignApp.Domain;

/// <summary>
/// Every fixed value of the domain in one place: the rules' limits and the messages shown when a
/// rule is broken. Other layers read the limits from here too, so they never drift apart.
/// </summary>
public static class Constants
{
    /// <summary>Limits of a design.</summary>
    public static class Designs
    {
        /// <summary>The longest allowed design name, after trimming.</summary>
        public const int MaxNameLength = 80;

        /// <summary>The longest allowed description, after trimming.</summary>
        public const int MaxDescriptionLength = 300;

        /// <summary>The most rooms one design can hold.</summary>
        public const int MaxRooms = 20;
    }

    /// <summary>Limits of a room and its analysis.</summary>
    public static class Rooms
    {
        /// <summary>The label map stores one byte per pixel, and 0 means "no object", so indexes run from 1 to 255.</summary>
        public const int MaxObjectIndex = 255;

        /// <summary>The label map value of a pixel that belongs to no object.</summary>
        public const byte NoObject = 0;

        /// <summary>How often analysis may start before the room is failed, so a photo that crashes the worker can't loop forever.</summary>
        public const int MaxAnalysisAttempts = 3;

        /// <summary>The longest note of a photo to choose from: its view's name, or the change the user asked the AI for.</summary>
        public const int MaxTakeNoteLength = 500;

        /// <summary>
        /// The furthest a 360-degree photo's start may turn from its middle, in degrees: its first view (80 degrees wide)
        /// stays clear of the seam behind.
        /// </summary>
        public const double MaxPanoramaStartYaw = 120;
    }

    /// <summary>Boxes in normalized image coordinates.</summary>
    public static class Boxes
    {
        /// <summary>Allows for rounding when a box touches the right or bottom edge.</summary>
        public const double Tolerance = 1e-4;
    }

    /// <summary>Colors and recolors.</summary>
    public static class Colors
    {
        /// <summary>The first character of every color.</summary>
        public const char Prefix = '#';

        /// <summary>The length of a "#RRGGBB" color.</summary>
        public const int Length = 7;

        /// <summary>The color used when one can't be read: a neutral gray.</summary>
        public const string Fallback = "#808080";

        /// <summary>The weakest recolor strength: the original color.</summary>
        public const double MinStrength = 0;

        /// <summary>The strongest recolor strength: the new color only.</summary>
        public const double MaxStrength = 1;

        /// <summary>The longest paint reference, such as <c>ral-classic/9010</c>.</summary>
        public const int MaxPaintLength = 60;

        /// <summary>Separates a paint reference's palette from the paint (<c>ral-classic/9010</c>).</summary>
        public const char PaintSeparator = '/';
    }

    /// <summary>How an object can be split by color into tones (bricks and mortar), each recolored on its own.</summary>
    public static class Tones
    {
        /// <summary>The fewest tones of a split object.</summary>
        public const int Min = 2;

        /// <summary>The most tones of a split object.</summary>
        public const int Max = 4;

        /// <summary>How far the tones' shares may add up past 1, for rounding.</summary>
        public const double ShareTolerance = 0.001;
    }

    /// <summary>The sizes users give walls, floors and ceilings, for paint amounts.</summary>
    public static class Surfaces
    {
        /// <summary>The smallest width or height, in metres.</summary>
        public const double MinMetres = 0.1;

        /// <summary>The largest width or height, in metres.</summary>
        public const double MaxMetres = 50;
    }

    /// <summary>Limits of a room the user builds from scratch: its walls and the objects placed in it.</summary>
    public static class Plans
    {
        /// <summary>The longest plan name, after trimming.</summary>
        public const int MaxNameLength = 80;

        /// <summary>The shortest wall, in metres.</summary>
        public const double MinWallMetres = 1;

        /// <summary>The longest wall, in metres.</summary>
        public const double MaxWallMetres = 30;

        /// <summary>The lowest ceiling, in metres.</summary>
        public const double MinHeightMetres = 2;

        /// <summary>The highest ceiling, in metres.</summary>
        public const double MaxHeightMetres = 6;

        /// <summary>The most objects one room may hold.</summary>
        public const int MaxItems = 60;

        /// <summary>The longest object name.</summary>
        public const int MaxLabelLength = 60;

        /// <summary>The smallest object side, in centimetres.</summary>
        public const double MinItemCentimetres = 1;

        /// <summary>The largest object side, in centimetres.</summary>
        public const double MaxItemCentimetres = 3000;

        /// <summary>Objects turn in quarter turns, in degrees.</summary>
        public const int RotationStep = 90;

        /// <summary>How deep an L-shaped object's runs are, as a share of its smaller side.</summary>
        public const double LegShare = 0.5;

        /// <summary>How wide a P-shaped object's chaise is, as a multiple of how deep its runs are (at most its width).</summary>
        public const double ChaiseShare = 1.7;

        /// <summary>How deep a P-shaped object's runs are, as a share of its smaller side (thinner than an L's, so its leg has length).</summary>
        public const double PLegShare = 0.4;

        /// <summary>The smallest part of an L- or P-shaped object (its run, leg or chaise), in centimetres.</summary>
        public const double MinPartCentimetres = 20;

        /// <summary>The most decorations one object may have.</summary>
        public const int MaxDecorations = 8;

        /// <summary>The longest name of one decoration.</summary>
        public const int MaxDecorationLength = 60;

        /// <summary>The longest name of an object's material, such as "velvet".</summary>
        public const int MaxMaterialLength = 40;

        /// <summary>A built room's walls' color when none is known: a warm off-white.</summary>
        public const string DefaultWallColor = "#E8E2D6";

        /// <summary>A built room's floor color when none is known: a light oak.</summary>
        public const string DefaultFloorColor = "#B89B7A";

        /// <summary>A built room's ceiling color until the user picks one: a soft white.</summary>
        public const string CeilingColor = "#FAFAF7";

        /// <summary>A built room's skirting boards' color until the user picks one: a warm white.</summary>
        public const string TrimColor = "#F4F1EA";

        /// <summary>An accent wall's color until the user picks one: a muted sage.</summary>
        public const string AccentColor = "#8A9A84";

        /// <summary>How high a window's bottom usually is above the floor, in centimetres.</summary>
        public const double WindowSill = 90;

        /// <summary>How high a new object hung on a wall usually starts above the floor, in centimetres.</summary>
        public const double MountedElevation = 140;

        /// <summary>The most an object hung on a wall may stick out into the room, in centimetres.</summary>
        public const double MaxMountedDepth = 70;

        /// <summary>How far a window or door may reach past its wall's ends or the ceiling (rounding), in centimetres.</summary>
        public const double WallTolerance = 1;

        /// <summary>The most corners a room's outline may have.</summary>
        public const int MaxCorners = 20;

        /// <summary>The shortest wall of a room's outline, in centimetres.</summary>
        public const double MinWallSegmentCentimetres = 20;
    }

    /// <summary>The messages shown when a domain rule is broken.</summary>
    public static class Messages
    {
        /// <summary>A design name that is empty or too long.</summary>
        public static readonly string NameLength = $"Design name must be 1 to {Designs.MaxNameLength} characters.";

        /// <summary>A description that is too long.</summary>
        public static readonly string DescriptionLength = $"The description can be at most {Designs.MaxDescriptionLength} characters.";

        /// <summary>A room of another user.</summary>
        public const string NotYourRoom = "Designs can only hold your own rooms.";

        /// <summary>A room added twice to one design.</summary>
        public const string RoomAlreadyInThisDesign = "This room is already in the design.";

        /// <summary>A room added to a design that is full.</summary>
        public static readonly string DesignFull = $"A design can hold at most {Designs.MaxRooms} rooms.";

        /// <summary>A room that isn't in the design.</summary>
        public const string RoomNotInDesign = "This room isn't in the design.";

        /// <summary>Colors for a room that hasn't been analyzed yet.</summary>
        public const string ColorsNeedAnalysis = "Colors can only be saved for rooms that finished analysis.";

        /// <summary>Removing the last room of a design.</summary>
        public const string LastRoom = "A design needs at least one room. Delete the design instead.";

        /// <summary>A recolor strength outside 0..1.</summary>
        public static readonly string StrengthRange = $"Recolor strength must be between {Colors.MinStrength} and {Colors.MaxStrength}.";

        /// <summary>A paint reference that is empty, too long or has no palette.</summary>
        public static readonly string PaintReference = $"A paint must be written as palette{Colors.PaintSeparator}paint, in at most {Colors.MaxPaintLength} characters.";

        /// <summary>A split object with too few or too many tones.</summary>
        public static readonly string ToneCount = $"An object split by color has between {Tones.Min} and {Tones.Max} tones.";

        /// <summary>Two tones of one object with the same color.</summary>
        public const string DuplicateTones = "The tones of an object must have different colors.";

        /// <summary>A tone's share outside 0..1, or shares adding up to more than the whole object.</summary>
        public const string ToneShare = "Each tone's share must be between 0 and 1, and together at most the whole object.";

        /// <summary>A tone that names a paint but has no color.</summary>
        public const string TonePaintNeedsColor = "A tone with a paint needs that paint's color.";

        /// <summary>A built room's walls outside the allowed sizes.</summary>
        public static readonly string PlanWalls =
            $"Walls are {Plans.MinWallMetres} to {Plans.MaxWallMetres} m long, and the ceiling {Plans.MinHeightMetres} to {Plans.MaxHeightMetres} m high.";

        /// <summary>A built room's name that is too long.</summary>
        public static readonly string PlanName = $"A room's name has at most {Plans.MaxNameLength} characters.";

        /// <summary>Too many objects in a built room.</summary>
        public static readonly string PlanItems = $"A room holds at most {Plans.MaxItems} objects.";

        /// <summary>An object without a usable name.</summary>
        public static readonly string PlanItemLabel = $"Give each object a name of up to {Plans.MaxLabelLength} characters.";

        /// <summary>An object with a side outside the allowed range.</summary>
        public static readonly string PlanItemSize = $"An object's sides are {Plans.MinItemCentimetres} to {Plans.MaxItemCentimetres} cm.";

        /// <summary>An object that isn't turned in quarter turns, or has no valid position or shape.</summary>
        public const string PlanItemPlacement = "An object has a position, a known shape and a turn of 0, 90, 180 or 270 degrees.";

        /// <summary>A window, door or hung object that isn't on a wall, or a floor object that is.</summary>
        public const string PlanWallPlacement = "Windows, doors and things hung on walls go on one of the room's walls, and other objects stand on the floor.";

        /// <summary>A hung object that sticks out too far.</summary>
        public static readonly string PlanMountedDepth = $"Something hung on a wall sticks out at most {Plans.MaxMountedDepth:0} cm.";

        /// <summary>A room outline that isn't a valid room.</summary>
        public static readonly string PlanOutline =
            $"A room's walls must run straight across or down the plan and turn at right angles, each at least {Plans.MinWallSegmentCentimetres:0} cm " +
            $"long and none crossing another, with at most {Plans.MaxCorners} corners and the room {Plans.MinWallMetres} to {Plans.MaxWallMetres} m across.";

        /// <summary>An object's material is named too long.</summary>
        public static readonly string PlanItemMaterial = $"Name an object's material in up to {Plans.MaxMaterialLength} characters.";

        /// <summary>A floor or wall area whose material isn't a floor type or wall finish, or that has decorations.</summary>
        public const string PlanZoneMaterial = "A floor area needs a floor material (such as Tiles), and a wall area a wall finish (such as Brick); neither has decorations.";

        /// <summary>An object's decorations are too many, too long, empty or repeated.</summary>
        public static readonly string PlanItemDecorations =
            $"An object may have up to {Plans.MaxDecorations} different decorations, each named in up to {Plans.MaxDecorationLength} characters.";

        /// <summary>A corner object's parts don't fit it.</summary>
        public static readonly string PlanItemParts =
            $"The parts of a corner object must each be at least {Plans.MinPartCentimetres:0} cm and fit inside it, and a chaise at least as wide as the leg.";

        /// <summary>An object's outline doesn't suit its shape.</summary>
        public const string PlanItemForm = "Only sofas, tables and cabinets can be L-shaped, only sofas P-shaped, and only tables and rugs round.";

        /// <summary>A window, door or hung object that doesn't fit on its wall.</summary>
        /// <param name="label">Its name.</param>
        public static string PlanWallItem(string label)
        {
            return $"The {label} doesn't fit on its wall: keep it between the wall's ends and under the ceiling.";
        }

        /// <summary>A floor type or view that isn't one of the choices.</summary>
        public const string PlanLook = "Choose one of the floor types and one of the room's walls to look at.";

        /// <summary>A built room's wall finish, ceiling light or time of day isn't one of the choices.</summary>
        public const string PlanFinish = "Choose one of the wall finishes, ceiling lights and times of day.";

        /// <summary>A built room's accent wall isn't one of its walls.</summary>
        public const string PlanAccentWall = "The accent wall must be one of the room's walls.";

        /// <summary>A built room type or shape that doesn't exist.</summary>
        public const string UnknownRoomType = "Pick the room's type from the list.";

        /// <summary>A surface width or height outside the allowed range.</summary>
        public static readonly string SurfaceSize = $"Sizes must be between {Surfaces.MinMetres} and {Surfaces.MaxMetres} metres.";

        /// <summary>A size for an object the room doesn't have.</summary>
        /// <param name="objectIndex">The object.</param>
        public static string UnknownSizeObject(int objectIndex)
        {
            return $"Object {objectIndex} isn't in this room, so it can't have a size.";
        }

        /// <summary>Two sizes for one object.</summary>
        /// <param name="objectIndex">The object.</param>
        public static string DuplicateSize(int objectIndex)
        {
            return $"Object {objectIndex} has more than one size.";
        }

        /// <summary>A room without an id.</summary>
        public const string RoomNeedsId = "A room needs an id.";

        /// <summary>A photo with a zero or negative size.</summary>
        public const string PhotoSize = "Photo dimensions must be positive.";

        /// <summary>A ready room without objects.</summary>
        public const string ReadyNeedsObjects = "A ready room needs at least one detected object.";

        /// <summary>Two objects with the same index.</summary>
        public const string DuplicateObjectIndexes = "Detected object indexes must be unique.";

        /// <summary>An object index outside 1..255.</summary>
        public static readonly string ObjectIndexRange = $"Detected object indexes must be between 1 and {Rooms.MaxObjectIndex}.";

        /// <summary>A removal without objects.</summary>
        public const string NothingToRemove = "A removal needs at least one object.";

        /// <summary>A hand-marked object whose index isn't the room's next one.</summary>
        /// <param name="index">The index given.</param>
        /// <param name="next">The room's next index.</param>
        public static string NotNextObjectIndex(int index, int next)
        {
            return $"A new object must get index {next}, not {index}.";
        }

        /// <summary>A part whose parent isn't in the room, or is itself a part.</summary>
        /// <param name="index">The part's index.</param>
        public static string BadParent(int index)
        {
            return $"Object {index} is a part of an object that isn't in the room or is a part itself.";
        }

        /// <summary>Merging an object that isn't a part.</summary>
        /// <param name="index">The object's index.</param>
        public static string NotAPart(int index)
        {
            return $"Object {index} isn't a part of another object.";
        }

        /// <summary>An edit for an object the room doesn't have.</summary>
        /// <param name="index">The object index.</param>
        public static string UnknownObject(int index)
        {
            return $"Object {index} does not exist in this room.";
        }

        /// <summary>Two edits for one object.</summary>
        /// <param name="index">The object index.</param>
        public static string DuplicateEdit(int index)
        {
            return $"Object {index} has more than one edit.";
        }

        /// <summary>Text that isn't a #RRGGBB color.</summary>
        /// <param name="input">The text.</param>
        public static string NotAColor(string? input)
        {
            return $"'{input}' is not a #RRGGBB color.";
        }

        /// <summary>A box that reaches outside the image.</summary>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        public static string BoxOutside(double x, double y, double width, double height)
        {
            return $"Bounding box ({x}, {y}, {width}, {height}) is outside the image.";
        }

        /// <summary>An analysis scope that doesn't exist.</summary>
        /// <param name="scope">The scope.</param>
        public static string UnknownScope(AnalysisScope scope)
        {
            return $"Unknown analysis scope {scope}.";
        }

        /// <summary>An AI mode that doesn't exist.</summary>
        /// <param name="mode">The mode.</param>
        public static string UnknownAiMode(AiMode mode)
        {
            return $"Unknown AI mode {mode}.";
        }

        /// <summary>An analysis started more often than allowed.</summary>
        public static readonly string TooManyAttempts = $"Analysis has already been tried {Rooms.MaxAnalysisAttempts} times.";

        /// <summary>Restarting the analysis of a room that isn't analyzing.</summary>
        /// <param name="status">The room's status.</param>
        public static string CannotRestart(RoomStatus status)
        {
            return $"Cannot restart the analysis of a room that is {status}.";
        }

        /// <summary>A photo to choose from with no storage key, size or note within its limit.</summary>
        public static readonly string TakeFields = $"A photo needs a storage key, a size, and a note of at most {Rooms.MaxTakeNoteLength} characters.";

        /// <summary>Choosing a photo of another room.</summary>
        public const string TakeOfOtherRoom = "That photo belongs to another room.";

        /// <summary>A 360-degree photo with no storage key, or not twice as wide as it is tall.</summary>
        public const string PanoramaFields = "A 360° photo needs a storage key, must be twice as wide as it is tall, and must start facing away from its seam.";

        /// <summary>A 360-degree photo for a room that holds no plan's photos.</summary>
        public const string PanoramaNeedsPlan = "Only a room made in the room builder can have a 360° photo.";

        /// <summary>Choosing another photo while the room's photo is being analyzed.</summary>
        /// <param name="status">The room's status.</param>
        public static string CannotChooseTake(RoomStatus status)
        {
            return $"The room's photo can't change while it is {status}; wait until its analysis is done.";
        }

        /// <summary>Fixing the outlines of a room that isn't ready.</summary>
        /// <param name="status">The room's status.</param>
        public static string CannotFixOutlines(RoomStatus status)
        {
            return $"Cannot fix the outlines of a room that is {status}.";
        }

        /// <summary>Switching the outlines of a room that isn't ready.</summary>
        /// <param name="status">The room's status.</param>
        public static string CannotChangeOutlines(RoomStatus status)
        {
            return $"Cannot change the outlines of a room that is {status}.";
        }

        /// <summary>A label map version that doesn't exist.</summary>
        /// <param name="revision">The version.</param>
        public static string NoLabelMapRevision(int revision)
        {
            return $"The room has no label map version {revision}.";
        }

        /// <summary>Failing a room that already finished.</summary>
        /// <param name="status">The room's status.</param>
        public static string CannotFail(RoomStatus status)
        {
            return $"Cannot fail a room that is already {status}.";
        }

        /// <summary>A status change that isn't allowed.</summary>
        /// <param name="from">The current status.</param>
        /// <param name="to">The requested status.</param>
        public static string CannotMove(RoomStatus from, RoomStatus to)
        {
            return $"Cannot move a room from {from} to {to}.";
        }
    }

    /// <summary>The steps a room goes through, from upload to editable.</summary>
    public enum RoomStatus
    {
        /// <summary>The photo is stored, and the analysis (which costs money with the real AI) waits until the user starts it.</summary>
        Waiting,
        /// <summary>The photo is stored; analysis hasn't started.</summary>
        Uploaded,
        /// <summary>The AI is listing the objects in the photo.</summary>
        Analyzing,
        /// <summary>The objects are being outlined into masks.</summary>
        Segmenting,
        /// <summary>Analysis finished; the room can be edited.</summary>
        Ready,
        /// <summary>Analysis stopped; see <see cref="DesignApp.Domain.Entities.Room.FailureReason"/>.</summary>
        Failed,
    }

    /// <summary>Which AI analyzes a room photo.</summary>
    public enum AiMode
    {
        /// <summary>The built-in fakes: free, offline and instant, with hand-traced or recorded outlines for the sample photos.</summary>
        Fake,
        /// <summary>Claude and SAM 3: real outlines for any photo, at a few cents per analysis.</summary>
        Real,
    }

    /// <summary>The kind of a detected object. The editor groups objects by it.</summary>
    public enum ObjectCategory
    {
        /// <summary>A wall surface.</summary>
        Wall,
        /// <summary>The floor.</summary>
        Floor,
        /// <summary>The ceiling.</summary>
        Ceiling,
        /// <summary>Sofas, tables, chairs, beds, cabinets.</summary>
        Furniture,
        /// <summary>Rugs, curtains, cushions, bedding.</summary>
        Textile,
        /// <summary>Plants, art, vases and other decoration.</summary>
        Decor,
        /// <summary>Lamps and light fittings.</summary>
        Lighting,
        /// <summary>Built-in items such as counters, cabinets, appliances and sanitary ware.</summary>
        Fixture,
        /// <summary>Anything that fits no other category.</summary>
        Other,
        /// <summary>Doors and windows (including glass doors), but not cabinet doors.</summary>
        Opening,
    }

    /// <summary>Which objects the analysis looks for, chosen by the user at upload.</summary>
    public enum AnalysisScope
    {
        /// <summary>Everything worth restyling: walls, floor, ceiling, doors, windows, furniture and decor.</summary>
        WholeRoom,
        /// <summary>Everything except doors and windows (glass and white door panels outline poorly and are rarely recolored).</summary>
        NoDoorsOrWindows,
        /// <summary>Only objects: furniture, cabinets, textiles, lamps and decor; no walls, floor, ceiling, doors or windows.</summary>
        ObjectsOnly,
    }

    /// <summary>The kinds of room the user can build, each with its own list of common objects.</summary>
    public enum RoomType
    {
        /// <summary>A living room.</summary>
        LivingRoom,
        /// <summary>A bedroom.</summary>
        Bedroom,
        /// <summary>A kitchen.</summary>
        Kitchen,
        /// <summary>A bathroom.</summary>
        Bathroom,
        /// <summary>A dining room.</summary>
        DiningRoom,
        /// <summary>A home office.</summary>
        HomeOffice,
        /// <summary>A child's room.</summary>
        KidsRoom,
    }

    /// <summary>How a built room's object is drawn in 3D: a few simple shapes cover everyday furniture.</summary>
    public enum ItemShape
    {
        /// <summary>A plain block: cabinets, wardrobes, shelves, appliances.</summary>
        Box,
        /// <summary>A block with soft edges: ottomans, mattresses, cushions.</summary>
        Rounded,
        /// <summary>A round shape: round tables, stools, bins, pots.</summary>
        Cylinder,
        /// <summary>A seat with a back and arms: sofas and armchairs.</summary>
        Sofa,
        /// <summary>A bed with a headboard.</summary>
        Bed,
        /// <summary>A top on four legs: tables and desks.</summary>
        Table,
        /// <summary>A seat with a back on four legs.</summary>
        Chair,
        /// <summary>A flat piece on the floor: rugs and mats.</summary>
        Rug,
        /// <summary>A plant in a pot.</summary>
        Plant,
        /// <summary>A lamp: a stand with a shade.</summary>
        Lamp,
        /// <summary>A window in a wall: a frame with glass, at a height above the floor.</summary>
        Window,
        /// <summary>A door in a wall, standing on the floor.</summary>
        Door,
        /// <summary>A flat thing hung on a wall: paintings, mirrors, a wall TV.</summary>
        Picture,
        /// <summary>A block hung on a wall: upper kitchen cabinets, wall shelves, a cooker hood, a mirror cabinet.</summary>
        WallBox,
        /// <summary>
        /// An area of the floor in its own material (a <see cref="FloorType"/> name) and color, such as tiles by the
        /// kitchen units: a surface, not furniture.
        /// </summary>
        FloorZone,
        /// <summary>
        /// An area of a wall in its own finish (a <see cref="WallFinish"/> name) and color, such as brick behind the sofa:
        /// a surface, not furniture.
        /// </summary>
        WallZone,
    }

    /// <summary>
    /// The form of a built room's object within its width × depth: most are plain boxes of their shape, but a corner sofa
    /// or desk is L-shaped seen from above, a round table or rug is an ellipse, a window may have a round top, and a box
    /// may be a shower's glass.
    /// </summary>
    public enum ItemForm
    {
        /// <summary>The object fills its width × depth.</summary>
        Standard,
        /// <summary>
        /// An L: a run along its whole back, <see cref="Plans.LegShare"/> of its smaller side deep, and a leg of the same
        /// width running forward from the back run at its right end as seen from above before turning (sofas, tables, boxes).
        /// </summary>
        LShaped,
        /// <summary>
        /// A P: an L with thinner runs (<see cref="Plans.PLegShare"/> of its smaller side deep) whose leg widens at its
        /// front end into a chaise, <see cref="Plans.ChaiseShare"/> times as wide as the runs are deep and as deep (corner sofas).
        /// </summary>
        PShaped,
        /// <summary>An ellipse in its width × depth: round when they are equal (tables and rugs).</summary>
        Round,
        /// <summary>A window with a round top: a half circle as wide as the window.</summary>
        Arched,
        /// <summary>A shower's glass screen (boxes): a low tray and a clear pane along part of its front.</summary>
        GlassScreen,
        /// <summary>A glass shower cubicle (boxes): a low tray and clear panes on its front and right side.</summary>
        GlassCubicle,
        /// <summary>A swivel office chair (chairs): a five-star base on wheels, a gas column, a tall back and armrests.</summary>
        Swivel,
    }

    /// <summary>
    /// The sides of a built room's floor plan seen from above: Back at the top, Front at the bottom, Left and Right at the
    /// sides. Each wall of a room's outline is on one of them (see <see cref="Objects.OutlineWall.Side"/>); a rectangle's
    /// walls are numbered in this order, so <c>(int)PlanWall.Left</c> is its left wall.
    /// </summary>
    public enum PlanWall
    {
        /// <summary>The wall at the top of the plan.</summary>
        Back,
        /// <summary>The wall on the right of the plan.</summary>
        Right,
        /// <summary>The wall at the bottom of the plan.</summary>
        Front,
        /// <summary>The wall on the left of the plan.</summary>
        Left,
    }

    /// <summary>
    /// Where the 3D view's camera stands to look at the wall in view: straight across from its middle, or in one of the
    /// near corners, turned to look diagonally across the room (as most room photos are taken).
    /// </summary>
    public enum ViewCorner
    {
        /// <summary>Straight across the room from the middle of the wall in view, looking straight at it.</summary>
        None,
        /// <summary>In the near left corner, looking towards the far right corner.</summary>
        Left,
        /// <summary>In the near right corner, looking towards the far left corner.</summary>
        Right,
        /// <summary>At the plan's camera spot (where the photo it was read from was taken), looking at the middle of the wall in view.</summary>
        Spot,
    }

    /// <summary>The interior style a built room's photo is made in: its materials and finishes, not its layout.</summary>
    public enum PhotoStyle
    {
        /// <summary>No style: the materials the objects' names and colors suggest.</summary>
        AsBuilt,
        /// <summary>Clean lines, smooth surfaces, a few bold accents.</summary>
        Modern,
        /// <summary>Light wood, white, wool and linen, cosy and simple.</summary>
        Scandinavian,
        /// <summary>Rattan, macramé, layered patterned textiles, warm earthy tones.</summary>
        Boho,
        /// <summary>Exposed brick and concrete, black metal, leather and reclaimed wood.</summary>
        Industrial,
        /// <summary>Japanese calm with Scandinavian warmth: low pale wood, paper and stone.</summary>
        Japandi,
        /// <summary>Tapered wooden legs, walnut, curved shapes, 1950s and 60s details.</summary>
        MidCentury,
        /// <summary>Rustic wood, shiplap, linen and wrought iron.</summary>
        Farmhouse,
        /// <summary>Breezy whites and blues, light wood, rope and linen.</summary>
        Coastal,
        /// <summary>Very few things, plain surfaces, no clutter.</summary>
        Minimalist,
        /// <summary>Classic moulding, turned wood, rich fabrics and patterns.</summary>
        Traditional,
        /// <summary>Geometric patterns, brass, velvet and lacquer.</summary>
        ArtDeco,
    }

    /// <summary>What a built room's walls are finished in.</summary>
    public enum WallFinish
    {
        /// <summary>Smooth painted walls.</summary>
        Paint,
        /// <summary>Patterned wallpaper.</summary>
        Wallpaper,
        /// <summary>Limewash or Venetian plaster, softly mottled.</summary>
        Plaster,
        /// <summary>Exposed brick.</summary>
        Brick,
        /// <summary>Wooden wall panelling.</summary>
        WoodPanelling,
        /// <summary>Wall tiles.</summary>
        Tiles,
        /// <summary>Raw or polished concrete.</summary>
        Concrete,
    }

    /// <summary>The light on a built room's ceiling.</summary>
    public enum CeilingLight
    {
        /// <summary>No ceiling light.</summary>
        None,
        /// <summary>A pendant lamp hanging from the middle.</summary>
        Pendant,
        /// <summary>Recessed spotlights.</summary>
        Spotlights,
        /// <summary>A chandelier.</summary>
        Chandelier,
        /// <summary>A flush ceiling light.</summary>
        FlushLight,
        /// <summary>Track lights.</summary>
        TrackLights,
    }

    /// <summary>When a built room's photo is taken, which sets its light and mood.</summary>
    public enum TimeOfDay
    {
        /// <summary>Soft, cool morning light.</summary>
        Morning,
        /// <summary>Bright, even daylight.</summary>
        Midday,
        /// <summary>Warm, low sunlight with long shadows.</summary>
        GoldenHour,
        /// <summary>After dark: the lamps on, warm and cosy.</summary>
        Evening,
    }

    /// <summary>What a built room's floor is made of.</summary>
    public enum FloorType
    {
        /// <summary>Wooden planks.</summary>
        Wood,
        /// <summary>Wooden parquet in a herringbone pattern.</summary>
        Parquet,
        /// <summary>Ceramic tiles.</summary>
        Tiles,
        /// <summary>Large stone or marble slabs.</summary>
        Stone,
        /// <summary>Polished concrete.</summary>
        Concrete,
        /// <summary>Wall-to-wall carpet.</summary>
        Carpet,
        /// <summary>Vinyl planks.</summary>
        Vinyl,
    }
}

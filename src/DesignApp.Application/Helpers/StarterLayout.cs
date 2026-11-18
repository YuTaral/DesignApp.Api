using DesignApp.Application.Objects;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;
using SL = DesignApp.Application.Constants.Builder.Layout;

namespace DesignApp.Application.Helpers;

/// <summary>
/// The furniture, windows and door a new built room starts with, sized and placed for its type and size by common
/// design rules: a door near the front of the left wall (with room for it to swing), windows on the back wall with glass
/// of about a tenth of the floor area (the usual daylight rule) at the usual sill height, and the room type's main
/// furniture against the walls with the usual clearances (90 cm walkways, 45 cm from a sofa to its coffee table, 60 cm
/// beside a bed, room behind dining chairs). Objects that don't fit a small room are left out; nothing overlaps, and no
/// tall object stands in front of a window or door.
/// </summary>
/// <remarks>
/// Positions are in centimetres on the floor plan (x across from the left wall, y down from the back wall); the photo
/// looks at the back wall from the front, so the back wall holds the windows and the furniture faces into the room.
/// </remarks>
public static class StarterLayout
{
    /// <summary>The starting objects of a room.</summary>
    /// <param name="type">The room type.</param>
    /// <param name="width">The room's width, in metres.</param>
    /// <param name="length">The room's length, in metres.</param>
    /// <param name="height">The ceiling height, in metres.</param>
    /// <param name="catalog">The room type's objects (with the windows and doors), for their names, sizes and colors.</param>
    /// <returns>The objects, the door and windows first.</returns>
    public static IReadOnlyList<PlanItem> For(RoomType type, double width, double length, double height, IReadOnlyList<CatalogObject> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var room = new Room(width * 100, length * 100, height * 100, catalog);
        var wet = type is RoomType.Kitchen or RoomType.Bathroom;

        room.Door();
        room.Windows(wet ? SL.WetSill : SL.Sill, type is RoomType.Bathroom ? SL.BathroomGlassShare : SL.GlassShare, wet ? SL.Blind : SL.Curtains);

        switch (type)
        {
            case RoomType.LivingRoom:
                LivingRoom(room);
                break;
            case RoomType.Bedroom:
                Bedroom(room);
                break;
            case RoomType.Kitchen:
                Kitchen(room);
                break;
            case RoomType.Bathroom:
                Bathroom(room);
                break;
            case RoomType.DiningRoom:
                DiningRoom(room);
                break;
            case RoomType.HomeOffice:
                HomeOffice(room);
                break;
            case RoomType.KidsRoom:
                KidsRoom(room);
                break;
        }

        return room.Items;
    }

    /// <summary>
    /// The finish a furnished room of a type starts with: tiled walls and spotlights in a bathroom, spotlights in a
    /// kitchen, an accent wall behind the bed in a bedroom (the right wall) and a kids' room, the usual pendant elsewhere.
    /// </summary>
    /// <param name="type">The room type.</param>
    public static RoomFinish FinishFor(RoomType type)
    {
        var plain = RoomFinish.Default;
        var spotlights = new RoomFinish(plain.Walls, null, plain.AccentColor, plain.AccentFinish, plain.CeilingColor, plain.TrimColor, CeilingLight.Spotlights, plain.TimeOfDay);

        return type switch
        {
            RoomType.Bathroom => new RoomFinish(WallFinish.Tiles, null, plain.AccentColor, plain.AccentFinish, plain.CeilingColor, plain.TrimColor, CeilingLight.Spotlights,
                plain.TimeOfDay),
            RoomType.Kitchen => spotlights,
            RoomType.Bedroom => Accent(plain, HexColor.Parse(SL.BedroomAccent)),
            RoomType.KidsRoom => Accent(plain, HexColor.Parse(SL.KidsAccent)),
            _ => plain,
        };
    }

    /// <summary>A finish with the right wall (behind or beside the bed) painted in its own color.</summary>
    /// <param name="plain">The finish to start from.</param>
    /// <param name="color">The accent wall's color.</param>
    private static RoomFinish Accent(RoomFinish plain, HexColor color)
    {
        return new RoomFinish(plain.Walls, (int)PlanWall.Right, color, WallFinish.Paint, plain.CeilingColor, plain.TrimColor, plain.CeilingLight, plain.TimeOfDay);
    }

    /// <summary>
    /// A sofa along the left wall with art above it, facing a TV stand on the right with the TV hung above it, a coffee
    /// table, ottoman and rug between them, an armchair, lamp, side table, plant and bookshelf, and a radiator under the window.
    /// </summary>
    /// <param name="room">The room.</param>
    private static void LivingRoom(Room room)
    {
        var (w, l) = (room.Width, room.Length);
        var free = room.FreeAlongLeft;
        var sofa = room.Size("sofa", Math.Clamp(free - 2 * SL.SideGap, 140, 220));
        var sofaY = Math.Max(sofa.Width / 2 + SL.SideGap, free / 2);
        var sofaPlaced = room.Against("sofa", PlanWall.Left, sofaY, sofa.Width);
        var depth = room.Get("sofa")?.Depth ?? 95;

        if (sofaPlaced is { } s)
        {
            var tableLength = Math.Clamp(sofa.Width * 0.6, 80, 120);
            var tableX = depth + SL.SofaToTable + 30;
            var tvDepth = room.Get("tv-stand")?.Depth ?? 40;

            // The table only goes in when a walkway is left between it and the TV.
            if (w - (tableX + 30) - tvDepth >= SL.Walkway)
            {
                room.Place("coffee-table", tableX, s.Y, 90, width: tableLength);
                room.Place("ottoman", tableX, s.Y + tableLength / 2 + 40, 0);
            }

            room.Rug("rug", (depth - 20 + w - tvDepth - 30) / 2, s.Y, Math.Min(w - depth - tvDepth - 10, 200), Math.Min(sofa.Width + 60, l - 60));

            if (room.Against("tv-stand", PlanWall.Right, s.Y, Math.Clamp(sofa.Width * 0.7, 100, 180)) is { } tv)
            {
                room.Hang("wall-tv", PlanWall.Right, tv.Y);
            }

            room.Hang("art-set", PlanWall.Left, s.Y);
            room.Against("side-table", PlanWall.Left, s.Y - sofa.Width / 2 - 30);
            room.Corner("floor-lamp", PlanWall.Back, PlanWall.Left);
        }

        room.Against("armchair", PlanWall.Back, w - 100);
        room.Corner("plant", PlanWall.Back, PlanWall.Right);
        room.Against("bookshelf", PlanWall.Right, l - 70);
        room.UnderWindow("radiator");
    }

    /// <summary>
    /// The bed's headboard on the right wall with art above it, a nightstand with a lamp each side and a bench at its foot,
    /// a wardrobe on the left wall, a rug, a dresser with a mirror above it, an armchair with a floor lamp, a plant, and a
    /// radiator under the window.
    /// </summary>
    /// <param name="room">The room.</param>
    private static void Bedroom(Room room)
    {
        var (w, l) = (room.Width, room.Length);
        var nightstand = room.Get("nightstand")?.Width ?? 45;
        var wardrobeDepth = room.Get("wardrobe")?.Depth ?? 60;
        // A double bed when it leaves room beside it; otherwise a smaller one.
        var bedWidth = l >= 160 + 2 * (nightstand + 10) + 40 ? 160 : l >= 120 + 2 * SL.BedSide ? 120 : 90;
        var bedLength = w - 210 >= SL.Walkway + 60 ? 210 : 200;
        var bedY = Math.Max(bedWidth / 2 + nightstand + 10, Math.Min(l * 0.42, room.FreeAlongLeft - bedWidth / 2));
        var bed = room.Against("double-bed", PlanWall.Right, bedY, bedWidth, bedLength, bedWidth == 160 ? null : bedWidth == 120 ? "small double bed" : "single bed");

        if (bed is { } b)
        {
            room.Against("nightstand", PlanWall.Right, b.Y - bedWidth / 2 - 5 - nightstand / 2);
            room.Against("nightstand", PlanWall.Right, b.Y + bedWidth / 2 + 5 + nightstand / 2);
            room.Rug("rug", w - bedLength * 0.55, b.Y, Math.Min(230, w - 100), Math.Min(bedWidth + 90, l - 60));
            room.Hang("art-set", PlanWall.Right, b.Y);

            // A bench across the bed's foot, when a walkway is left between it and the wardrobe.
            if (w - bedLength - 45 - wardrobeDepth >= SL.Walkway)
            {
                room.Place("bed-bench", w - bedLength - 25, b.Y, 90, width: Math.Min(130, bedWidth - 20));
            }
        }

        room.Against("wardrobe", PlanWall.Left, 30 + 75, Math.Clamp(room.FreeAlongLeft - 60, 80, 150));

        if (room.Against("dresser", PlanWall.Back, w * 0.4) is { } dresser)
        {
            room.Hang("mirror", PlanWall.Back, dresser.X);
        }

        room.Against("armchair", PlanWall.Back, 110);
        room.Corner("floor-lamp", PlanWall.Back, PlanWall.Left);
        room.Corner("plant", PlanWall.Back, PlanWall.Right);
        room.UnderWindow("radiator");
    }

    /// <summary>
    /// The back wall's worktop: the sink under the window with the dishwasher beside it, the cooker with its hood, the
    /// fridge at the end, base cabinets between them and wall cabinets above them; a tall cabinet and bin on the left,
    /// open shelves on the right, and an island with stools or a table with chairs.
    /// </summary>
    /// <param name="room">The room.</param>
    private static void Kitchen(Room room)
    {
        var (w, l) = (room.Width, room.Length);
        var islandY = 60 + SL.KitchenAisle + 45;
        var islandLength = Math.Clamp(w - 2 * SL.KitchenAisle, 120, 180);
        var tableLength = Math.Clamp(w - 2 * 100, 80, 140);
        var tableY = (60 + SL.KitchenAisle + l) / 2;
        var cookerWidth = room.Get("cooker")?.Width ?? 60;
        var cookerX = Within(w * 0.4, cookerWidth / 2 + 40, w - 70 - cookerWidth);
        var fridge = room.Against("fridge", PlanWall.Back, w - 32);
        var cooker = room.Against("cooker", PlanWall.Back, cookerX);
        var sink = room.Against("sink-unit", PlanWall.Back, room.BackWindow()?.X ?? w * 0.7);

        if (sink is { } k)
        {
            room.Against("dishwasher", PlanWall.Back, k.X + k.Width / 2 + 30);
        }

        if (cooker is { } c)
        {
            room.Hang("cooker-hood", PlanWall.Back, c.X);
        }

        // The worktop fills the rest of the back wall, with wall cabinets above it where there is no window.
        foreach (var run in room.FillBack("base-cabinets"))
        {
            room.Hang("upper-cabinets", PlanWall.Back, run.X, width: run.Width);
        }

        room.Against("tall-cabinets", PlanWall.Left, 60 + 30);
        room.Against("bin", PlanWall.Left, 60 + 60 + 25);
        room.Hang("open-shelves", PlanWall.Right, (fridge?.Depth ?? 65) + 80);

        if (w >= islandLength + 2 * SL.KitchenAisle && l >= islandY + 45 + 50 + SL.Walkway && room.Place("island", w / 2, islandY, 0, width: islandLength) is not null)
        {
            var stools = (int)(islandLength / 60);

            for (var i = 0; i < stools; i++)
            {
                room.Place("stool", w / 2 - islandLength / 2 + islandLength / stools * (i + 0.5), islandY + 45 + 25, 0);
            }

            return;
        }

        if (room.Place("dining-table", w / 2, tableY, 0, width: tableLength) is not null)
        {
            room.Chairs("chair", w / 2, tableY, tableLength, room.Get("dining-table")?.Depth ?? 80, 0);
        }
    }

    /// <summary>
    /// A bathtub (or a shower in a small room) with a shelf above it and a vanity with a mirror cabinet above it on the
    /// back wall, the toilet on the right wall, a tall cabinet, a towel rail with towels, a bath mat, a laundry basket and a plant.
    /// </summary>
    /// <param name="room">The room.</param>
    private static void Bathroom(Room room)
    {
        var (w, l) = (room.Width, room.Length);
        var bath = w >= 170 + 80 + 20 ? room.Against("bathtub", PlanWall.Back, Math.Clamp(w - 80 - 20, 150, 170) / 2, Math.Clamp(w - 80 - 20, 150, 170)) : null;
        var wet = bath ?? room.Corner("shower", PlanWall.Back, PlanWall.Left);

        if (wet is { } t)
        {
            room.Rug("bath-mat", t.X, t.Y + t.Depth / 2 + 30, 80, 50);
            room.Hang("bath-shelf", PlanWall.Back, t.X);
        }

        if (room.Against("vanity", PlanWall.Back, w - 45) is { } vanity)
        {
            room.Hang("mirror-cabinet", PlanWall.Back, vanity.X);
        }

        room.Against("toilet", PlanWall.Right, Math.Max(l * 0.5, 100));
        room.Against("tall-cabinet", PlanWall.Right, l - 40);
        room.Against("towel-rail", PlanWall.Left, Math.Min(l * 0.45, room.FreeAlongLeft - 40));
        room.Against("laundry-basket", PlanWall.Left, room.FreeAlongLeft - 30);
        room.Against("plant", PlanWall.Left, 30);
    }

    /// <summary>
    /// A dining table in the middle along the longer side with chairs all round, a rug under it, a sideboard with art
    /// above it (or on the left wall), a display cabinet, a lamp, a plant, and a radiator under the window.
    /// </summary>
    /// <param name="room">The room.</param>
    private static void DiningRoom(Room room)
    {
        var (w, l) = (room.Width, room.Length);
        var chairDepth = room.Get("chair")?.Depth ?? 52;
        var along = l > w;
        var (longSide, shortSide) = along ? (l, w) : (w, l);
        var tableLength = Math.Clamp(longSide - 2 * (chairDepth + SL.BehindChair), 0, 180);
        var tableDepth = room.Get("dining-table")?.Depth ?? 90;
        var rotation = along ? 90 : 0;
        var fits = tableLength >= 120 && shortSide >= tableDepth + 2 * (chairDepth + SL.BehindChair);
        var middle = (x: w / 2, y: (l + (room.Get("sideboard")?.Depth ?? 45)) / 2);

        if (fits && room.Place("dining-table", middle.x, middle.y, rotation, width: tableLength) is not null)
        {
            var (spanX, spanY) = along ? (tableDepth, tableLength) : (tableLength, tableDepth);

            room.Rug("rug", middle.x, middle.y, Math.Min(spanX + 2 * 60, w - 40), Math.Min(spanY + 2 * 60, l - 40));
            room.Chairs("chair", middle.x, middle.y, tableLength, tableDepth, rotation);
        }
        else if (room.Place("round-table", middle.x, middle.y, 0) is not null)
        {
            var diameter = room.Get("round-table")?.Width ?? 120;

            room.Rug("rug", middle.x, middle.y, Math.Min(diameter + 120, w - 40), Math.Min(diameter + 120, l - 40));
            room.Chairs("chair", middle.x, middle.y, diameter, diameter, 0);
        }

        var sideboard = room.Against("sideboard", PlanWall.Back, w / 2);

        room.HangAny("art-set", (PlanWall.Back, sideboard?.X ?? w / 2), (PlanWall.Left, room.FreeAlongLeft / 2));
        room.Against("display-cabinet", PlanWall.Right, 70);
        room.Corner("plant", PlanWall.Back, PlanWall.Left);
        room.Corner("floor-lamp", PlanWall.Back, PlanWall.Right);
        room.UnderWindow("radiator");
    }

    /// <summary>
    /// A desk under the back window with its chair and a pinboard above it (or on the left wall), a filing cabinet, a
    /// bookshelf on the right wall, a sofa bed (or an armchair) on the left, a rug, a lamp, a plant, and a radiator under the window.
    /// </summary>
    /// <param name="room">The room.</param>
    private static void HomeOffice(Room room)
    {
        var (w, l) = (room.Width, room.Length);
        var deskWidth = Math.Clamp(w * 0.45, 100, 160);
        var deskDepth = room.Get("desk")?.Depth ?? 70;
        var desk = room.Against("desk", PlanWall.Back, w / 2, deskWidth);

        if (desk is { } d)
        {
            room.Place("office-chair", d.X, deskDepth + 35, 180);
            room.Against("filing-cabinet", PlanWall.Back, d.X + deskWidth / 2 + 30);
            room.Rug("rug", d.X, deskDepth + 80, Math.Min(deskWidth + 40, w - 60), Math.Min(160, l - deskDepth - 60));
        }

        room.Against("bookshelf", PlanWall.Right, 60);
        var sofaFits = room.FreeAlongLeft >= 190 + 2 * SL.SideGap && w >= 90 + SL.Walkway + deskDepth;

        if (!sofaFits || room.Against("sofa-bed", PlanWall.Left, room.FreeAlongLeft / 2) is null)
        {
            room.Against("armchair", PlanWall.Left, room.FreeAlongLeft / 2);
        }

        room.HangAny("pinboard", (PlanWall.Back, desk?.X ?? w / 2), (PlanWall.Left, room.FreeAlongLeft / 2));
        room.Corner("plant", PlanWall.Back, PlanWall.Left);
        room.Against("floor-lamp", PlanWall.Right, 60 + 40 + 30);
        room.UnderWindow("radiator");
    }

    /// <summary>
    /// A single bed along the right wall with its head at the back and wall stickers above it, a nightstand, a wardrobe and
    /// toy box on the left, a desk with its chair, a bookcase, a rug, a beanbag, a play tent when there is room, and a lamp.
    /// </summary>
    /// <param name="room">The room.</param>
    private static void KidsRoom(Room room)
    {
        var (w, l) = (room.Width, room.Length);
        var bedLength = room.Get("single-bed")?.Depth ?? 200;
        var bed = room.Place("single-bed", w - 45, bedLength / 2, 0);

        room.Against("nightstand", PlanWall.Back, w - 90 - 5 - 20);
        room.Against("wardrobe", PlanWall.Left, 60);
        room.Against("toy-box", PlanWall.Left, 60 + 50 + 60);

        if (room.Against("desk", PlanWall.Back, Math.Max(150, w * 0.4)) is { } desk)
        {
            room.Place("chair", desk.X, (room.Get("desk")?.Depth ?? 55) + 30, 180);
        }

        if (bed is { } b)
        {
            room.Hang("wall-stickers", PlanWall.Right, b.Y, elevation: b.Height + 20);
        }

        room.Against("bookcase", PlanWall.Right, bedLength + 10 + 50);
        room.Rug("rug", w / 2 - 20, l * 0.55, Math.Min(140, w - 200), Math.Min(200, l - 160));
        room.Place("beanbag", 100, l - 160, 0);
        room.Corner("play-tent", PlanWall.Front, PlanWall.Right);
        room.Corner("floor-lamp", PlanWall.Back, PlanWall.Left);
    }

    /// <summary>
    /// A value kept within a range, like <see cref="Math.Clamp(double, double, double)"/>, but the range's middle when the
    /// room is too small for the range to exist (a lowest value above the highest), rather than an error: whatever is
    /// placed there is then turned down for not fitting.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="low">The lowest value wanted.</param>
    /// <param name="high">The highest value wanted.</param>
    private static double Within(double value, double low, double high)
    {
        return low > high ? (low + high) / 2 : Math.Clamp(value, low, high);
    }

    /// <summary>
    /// The room being furnished: what is placed so far, the floor it takes, and the windows and doors each wall has, so a
    /// new object can be checked before it goes in.
    /// </summary>
    /// <param name="Width">The width, in centimetres.</param>
    /// <param name="Length">The length, in centimetres.</param>
    /// <param name="Height">The ceiling height, in centimetres.</param>
    /// <param name="Catalog">The objects that can be placed.</param>
    private sealed record Room(double Width, double Length, double Height, IReadOnlyList<CatalogObject> Catalog)
    {
        private readonly List<(double X1, double Y1, double X2, double Y2, double Top)> _taken = [];
        private readonly List<(PlanWall Wall, double From, double To, double Bottom, double Top)> _openings = [];
        private readonly List<(PlanWall Wall, double From, double To, double Bottom, double Top)> _hung = [];

        /// <summary>The objects placed so far.</summary>
        public List<PlanItem> Items { get; } = [];

        /// <summary>How much of the left wall, from the back, is free of the door and its swing.</summary>
        public double FreeAlongLeft { get; private set; }

        /// <summary>A catalog object by its key.</summary>
        /// <param name="key">The key.</param>
        public CatalogObject? Get(string key)
        {
            return Catalog.FirstOrDefault(o => o.Key == key);
        }

        /// <summary>An object's size with another width.</summary>
        /// <param name="key">The key.</param>
        /// <param name="width">The width wanted.</param>
        public (double Width, double Depth) Size(string key, double width)
        {
            return (Math.Round(width), Get(key)?.Depth ?? 0);
        }

        /// <summary>The door: near the front of the left wall, with the floor it swings over kept free.</summary>
        public void Door()
        {
            var door = Catalog.FirstOrDefault(o => o.Shape == ItemShape.Door);

            FreeAlongLeft = Length;

            if (door is null || Length < door.Width + 2 * SL.DoorMargin)
            {
                return;
            }

            var height = Math.Min(door.Height, Height - 20);
            // Along the left wall, its left end (facing it from inside) is the front corner.
            var along = SL.DoorMargin + door.Width / 2;
            var (from, to) = (Length - SL.DoorMargin - door.Width, Length - SL.DoorMargin);

            Items.Add(new PlanItem(door.Label, door.Shape, along, 0, door.Width, door.Depth, height, 0, door.Color, (int)PlanWall.Left, material: door.Material));
            _openings.Add((PlanWall.Left, from, to, 0, height));
            _taken.Add((0, from, door.Width, to, height));
            FreeAlongLeft = from - SL.SideGap;
        }

        /// <summary>
        /// The windows: on the back wall, evenly spaced, with glass of a share of the floor area; what the back wall can't
        /// hold goes in one window on the right wall.
        /// </summary>
        /// <param name="sill">How high their bottom is above the floor, in centimetres.</param>
        /// <param name="share">The glass wanted, as a share of the floor area.</param>
        /// <param name="dressing">What they are dressed with, such as "sheer curtains".</param>
        public void Windows(double sill, double share, string dressing)
        {
            var window = Catalog.FirstOrDefault(o => o.Key == "window") ?? Catalog.FirstOrDefault(o => o.Shape == ItemShape.Window);

            if (window is null)
            {
                return;
            }

            var height = Math.Min(SL.WindowHeight, Height - sill - SL.WindowHead);

            if (height < 60)
            {
                return;
            }

            var wanted = Width * Length * share / height;
            var count = Math.Max(1, (int)Math.Ceiling(wanted / SL.MaxWindowWidth));
            var each = Math.Clamp(wanted / count, SL.MinWindowWidth, SL.MaxWindowWidth);

            while (count > 1 && count * each + (count + 1) * SL.WindowGap > Width)
            {
                count--;
            }

            each = Math.Min(each, Width - 2 * SL.WindowGap);

            if (each < SL.MinWindowWidth)
            {
                return;
            }

            each = Math.Round(each / 10) * 10;
            var step = Width / count;

            for (var i = 0; i < count; i++)
            {
                AddWindow(window, PlanWall.Back, step * (i + 0.5), each, height, sill, dressing);
            }

            var missing = wanted - count * each;

            if (missing >= SL.MinWindowWidth && Length >= missing + 2 * SL.WindowGap)
            {
                AddWindow(window, PlanWall.Right, Length * 0.35, Math.Round(Math.Min(missing, SL.MaxWindowWidth) / 10) * 10, height, sill, dressing);
            }
        }

        /// <summary>Adds a window.</summary>
        /// <param name="window">The catalog window.</param>
        /// <param name="wall">Its wall.</param>
        /// <param name="along">Its middle along the wall, from its left end facing it (the back wall's is the left corner, the right wall's the back corner).</param>
        /// <param name="width">Its width.</param>
        /// <param name="height">Its height.</param>
        /// <param name="sill">Its height above the floor.</param>
        /// <param name="dressing">What it is dressed with.</param>
        private void AddWindow(CatalogObject window, PlanWall wall, double along, double width, double height, double sill, string dressing)
        {
            Items.Add(new PlanItem(window.Label, window.Shape, Math.Round(along), 0, width, window.Depth, Math.Round(height), 0, window.Color, (int)wall, sill,
                decorations: new ItemDecorations([dressing]), material: window.Material));
            _openings.Add((wall, along - width / 2, along + width / 2, sill, sill + height));
        }

        /// <summary>The first window on the back wall, if any.</summary>
        public PlanItem? BackWindow()
        {
            return Items.FirstOrDefault(i => i.Shape == ItemShape.Window && i.Wall == (int)PlanWall.Back);
        }

        /// <summary>Something hung under the first back window, such as a radiator, when it fits there.</summary>
        /// <param name="key">The object.</param>
        public PlanItem? UnderWindow(string key)
        {
            var window = BackWindow();

            return window is null ? null : Hang(key, PlanWall.Back, window.X);
        }

        /// <summary>
        /// Something hung on a wall (a picture, a TV, wall cabinets), above what stands against it: clear of the wall's
        /// windows and doors, of other hung things and of furniture taller than its bottom, inside the wall and under the ceiling.
        /// </summary>
        /// <param name="key">The object.</param>
        /// <param name="wall">The wall.</param>
        /// <param name="at">Its middle: across the plan for the back and front walls, down it for the sides.</param>
        /// <param name="width">Its width, when not the usual.</param>
        /// <param name="elevation">Its bottom above the floor, when not the usual.</param>
        /// <returns>The object hung, or <c>null</c> when it doesn't fit there.</returns>
        public PlanItem? Hang(string key, PlanWall wall, double at, double? width = null, double? elevation = null)
        {
            var o = Get(key);

            if (o is null)
            {
                return null;
            }

            var w = Math.Round(width ?? o.Width);
            var length = wall is PlanWall.Left or PlanWall.Right ? Length : Width;
            var bottom = Math.Round(Math.Min(elevation ?? o.Elevation, Height - o.Height - SL.CeilingGap));
            var (from, to, top) = (at - w / 2, at + w / 2, bottom + o.Height);
            // A wall's own "along" runs from its left end seen from inside: the front wall's from the right, the left wall's from the front.
            var along = wall switch
            {
                PlanWall.Front => Width - at,
                PlanWall.Left => Length - at,
                _ => at,
            };
            var inside = from >= 0 && to <= length && bottom >= 0;
            var crossed = _openings.Concat(_hung).Any(x => x.Wall == wall && from < x.To && x.From < to && bottom < x.Top && x.Bottom < top);
            var standing = _taken.Any(t => Touches(t, wall) && from < Span(t, wall).To && Span(t, wall).From < to && t.Top > bottom);

            if (!inside || crossed || standing)
            {
                return null;
            }

            var item = new PlanItem(o.Label, o.Shape, Math.Round(along), 0, w, o.Depth, o.Height, 0, o.Color, (int)wall, bottom, decorations: Dress(o), material: o.Material);

            Items.Add(item);
            _hung.Add((wall, from, to, bottom, top));

            return item;
        }

        /// <summary>Something hung at the first of some spots where it fits (art above a sideboard, else on another wall).</summary>
        /// <param name="key">The object.</param>
        /// <param name="spots">The walls and points to try, in order.</param>
        /// <returns>The object hung, or <c>null</c> when it fits at none.</returns>
        public PlanItem? HangAny(string key, params (PlanWall Wall, double At)[] spots)
        {
            foreach (var (wall, at) in spots)
            {
                if (Hang(key, wall, at) is { } hung)
                {
                    return hung;
                }
            }

            return null;
        }

        /// <summary>
        /// Runs of an object filling the gaps along the back wall between what already stands against it (base cabinets
        /// between the sink, cooker and fridge), each gap at least <see cref="SL.MinRun"/> wide.
        /// </summary>
        /// <param name="key">The object.</param>
        /// <returns>The runs placed.</returns>
        public List<PlanItem> FillBack(string key)
        {
            var against = _taken.Where(t => Touches(t, PlanWall.Back)).OrderBy(t => t.X1).ToList();
            var runs = new List<PlanItem>();
            var cursor = 0.0;

            foreach (var t in against.Append((Width, 0, Width, 0, 0)))
            {
                if (Run(key, PlanWall.Back, cursor, t.X1) is { } run)
                {
                    runs.Add(run);
                }

                cursor = Math.Max(cursor, t.X2);
            }

            return runs;
        }

        /// <summary>An object against a wall, facing into the room, as near a point along the wall as it fits.</summary>
        /// <param name="key">The object.</param>
        /// <param name="wall">The wall.</param>
        /// <param name="at">Where its middle should be: across the plan for the back and front walls, down it for the sides.</param>
        /// <param name="width">Its width, when not the usual.</param>
        /// <param name="depth">Its depth, when not the usual.</param>
        /// <param name="label">Its name, when not the usual.</param>
        /// <returns>The object placed, or <c>null</c> when it fits nowhere along the wall.</returns>
        public PlanItem? Against(string key, PlanWall wall, double at, double? width = null, double? depth = null, string? label = null)
        {
            var o = Get(key);

            if (o is null)
            {
                return null;
            }

            var (w, d) = (width ?? o.Width, depth ?? o.Depth);
            var sideways = wall is PlanWall.Left or PlanWall.Right;
            var length = sideways ? Length : Width;

            // Wider than the wall: it fits nowhere along it.
            if (w > length)
            {
                return null;
            }

            // Try the point first, then further and further either side of it.
            for (var shift = 0.0; shift <= length; shift += SL.Step)
            {
                foreach (var sign in shift == 0 ? new[] { 1 } : new[] { 1, -1 })
                {
                    var point = Math.Clamp(at + sign * shift, w / 2, length - w / 2);
                    var (x, y, turn) = wall switch
                    {
                        PlanWall.Back => (point, d / 2, 0),
                        PlanWall.Front => (point, Length - d / 2, 180),
                        PlanWall.Left => (d / 2, point, 270),
                        _ => (Width - d / 2, point, 90),
                    };

                    if (Place(key, x, y, turn, w, d, label) is { } placed)
                    {
                        return placed;
                    }
                }
            }

            return null;
        }

        /// <summary>An object in a corner, against both walls.</summary>
        /// <param name="key">The object.</param>
        /// <param name="back">The back or front wall.</param>
        /// <param name="side">The left or right wall.</param>
        /// <returns>The object placed, or <c>null</c> when the corner is taken.</returns>
        public PlanItem? Corner(string key, PlanWall back, PlanWall side)
        {
            var o = Get(key);

            if (o is null)
            {
                return null;
            }

            var x = side == PlanWall.Left ? o.Width / 2 + 5 : Width - o.Width / 2 - 5;
            var y = back == PlanWall.Back ? o.Depth / 2 + 5 : Length - o.Depth / 2 - 5;

            return Place(key, x, y, back == PlanWall.Back ? 0 : 180);
        }

        /// <summary>A run of an object along a wall between two points, as wide as the gap (cabinets filling a worktop).</summary>
        /// <param name="key">The object.</param>
        /// <param name="wall">The back wall.</param>
        /// <param name="from">Where the run starts, across the plan.</param>
        /// <param name="to">Where it ends.</param>
        /// <returns>The run placed, or <c>null</c> when the gap is too small or taken.</returns>
        public PlanItem? Run(string key, PlanWall wall, double from, double to)
        {
            return to - from >= SL.MinRun ? Against(key, wall, (from + to) / 2, Math.Round(to - from)) : null;
        }

        /// <summary>A rug: flat under the furniture, kept inside the room.</summary>
        /// <param name="key">The rug.</param>
        /// <param name="x">Its middle across the plan.</param>
        /// <param name="y">Its middle down the plan.</param>
        /// <param name="across">Its size across.</param>
        /// <param name="down">Its size down.</param>
        public void Rug(string key, double x, double y, double across, double down)
        {
            if (across < SL.MinRug || down < SL.MinRug)
            {
                return;
            }

            Place(key, Within(x, across / 2, Width - across / 2), Within(y, down / 2, Length - down / 2), 0, Math.Round(across), Math.Round(down));
        }

        /// <summary>Chairs round a table, facing it: along both long sides, and one at each end when there is room.</summary>
        /// <param name="key">The chair.</param>
        /// <param name="x">The table's middle across the plan.</param>
        /// <param name="y">Its middle down the plan.</param>
        /// <param name="length">Its length.</param>
        /// <param name="depth">Its depth.</param>
        /// <param name="rotation">Its turn: 0 when its length runs across the plan, 90 when down.</param>
        public void Chairs(string key, double x, double y, double length, double depth, int rotation)
        {
            var chair = Get(key);

            if (chair is null)
            {
                return;
            }

            var perSide = Math.Max(1, (int)(length / SL.PlaceWidth));
            var reach = depth / 2 + chair.Depth / 2;
            var end = length / 2 + chair.Depth / 2;

            for (var i = 0; i < perSide; i++)
            {
                var offset = -length / 2 + length / perSide * (i + 0.5);

                if (rotation == 0)
                {
                    Place(key, x + offset, y - reach, 0);
                    Place(key, x + offset, y + reach, 180);
                }
                else
                {
                    Place(key, x - reach, y + offset, 270);
                    Place(key, x + reach, y + offset, 90);
                }
            }

            if (rotation == 0)
            {
                Place(key, x - end, y, 270);
                Place(key, x + end, y, 90);
            }
            else
            {
                Place(key, x, y - end, 0);
                Place(key, x, y + end, 180);
            }
        }

        /// <summary>
        /// An object at a point, if it fits: inside the walls, on free floor (rugs may lie under things), and not in front
        /// of a door, or of a window it would stand taller than.
        /// </summary>
        /// <param name="key">The object.</param>
        /// <param name="x">Its middle across the plan.</param>
        /// <param name="y">Its middle down the plan.</param>
        /// <param name="rotation">Its turn: 0 faces the front, 90 the left, 180 the back, 270 the right.</param>
        /// <param name="width">Its width, when not the usual.</param>
        /// <param name="depth">Its depth, when not the usual.</param>
        /// <param name="label">Its name, when not the usual.</param>
        /// <returns>The object placed, or <c>null</c> when it doesn't fit there.</returns>
        public PlanItem? Place(string key, double x, double y, int rotation, double? width = null, double? depth = null, string? label = null)
        {
            var o = Get(key);

            if (o is null)
            {
                return null;
            }

            var (w, d) = (Math.Round(width ?? o.Width), Math.Round(depth ?? o.Depth));
            var h = Math.Min(o.Height, Height - 5);
            var turned = rotation % 180 != 0;
            var (across, down) = turned ? (d, w) : (w, d);
            var box = (X1: x - across / 2, Y1: y - down / 2, X2: x + across / 2, Y2: y + down / 2, Top: h);
            const double edge = 0.5;

            if (box.X1 < -edge || box.Y1 < -edge || box.X2 > Width + edge || box.Y2 > Length + edge)
            {
                return null;
            }

            var rug = o.Shape == ItemShape.Rug;

            if (!rug && _taken.Any(t => box.X1 < t.X2 - edge && t.X1 < box.X2 - edge && box.Y1 < t.Y2 - edge && t.Y1 < box.Y2 - edge))
            {
                return null;
            }

            if (!rug && _openings.Concat(_hung).Any(opening => Blocks(box, opening)))
            {
                return null;
            }

            var item = new PlanItem(label ?? o.Label, o.Shape, Math.Round(x), Math.Round(y), w, d, h, rotation, o.Color, decorations: Dress(o), material: o.Material);

            Items.Add(item);

            if (!rug)
            {
                _taken.Add(box);
            }

            return item;
        }

        /// <summary>
        /// Whether an object against a wall stands in front of one of its windows or doors, or of something hung on it, and
        /// is taller than its bottom.
        /// </summary>
        /// <param name="box">The object's footprint and height.</param>
        /// <param name="opening">The window, door or hung thing: its wall, its span along it, its bottom and top.</param>
        private bool Blocks((double X1, double Y1, double X2, double Y2, double Top) box, (PlanWall Wall, double From, double To, double Bottom, double Top) opening)
        {
            var (from, to) = Span(box, opening.Wall);

            return Touches(box, opening.Wall) && from < opening.To && opening.From < to && box.Top > opening.Bottom;
        }

        /// <summary>Whether a footprint stands against a wall.</summary>
        /// <param name="box">The footprint.</param>
        /// <param name="wall">The wall.</param>
        private bool Touches((double X1, double Y1, double X2, double Y2, double Top) box, PlanWall wall)
        {
            return wall switch
            {
                PlanWall.Back => box.Y1 < SL.WallTouch,
                PlanWall.Front => box.Y2 > Length - SL.WallTouch,
                PlanWall.Left => box.X1 < SL.WallTouch,
                _ => box.X2 > Width - SL.WallTouch,
            };
        }

        /// <summary>How far along a wall a footprint reaches, in plan coordinates: across for the back and front walls, down for the sides.</summary>
        /// <param name="box">The footprint.</param>
        /// <param name="wall">The wall.</param>
        private static (double From, double To) Span((double X1, double Y1, double X2, double Y2, double Top) box, PlanWall wall)
        {
            return wall is PlanWall.Back or PlanWall.Front ? (box.X1, box.X2) : (box.Y1, box.Y2);
        }

        /// <summary>The decorations an object starts with in a furnished room, if any.</summary>
        /// <param name="o">The catalog object.</param>
        private static ItemDecorations? Dress(CatalogObject o)
        {
            return o.Dressing is { Count: > 0 } names ? new ItemDecorations(names) : null;
        }
    }
}

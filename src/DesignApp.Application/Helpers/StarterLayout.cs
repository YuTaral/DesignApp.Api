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
        var room = new LayoutRoom(width * 100, length * 100, height * 100, catalog);
        var wet = type is RoomType.Kitchen or RoomType.Bathroom;

        room.Door();
        var dressed = wet
            ? new WindowCovering(CoveringKind.RollerBlind, HexColor.Parse(SL.CoveringColor), SL.BlindFabric, CurtainLength.Sill)
            : new WindowCovering(CoveringKind.Sheers, HexColor.Parse(SL.CoveringColor), SL.SheerFabric, CurtainLength.Floor);

        room.Windows(wet ? SL.WetSill : SL.Sill, type is RoomType.Bathroom ? SL.BathroomGlassShare : SL.GlassShare, dressed);

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

        CeilingLights(type, room);

        return room.Items;
    }

    /// <summary>
    /// The ceiling lights a furnished room starts with: a grid of spotlights in a kitchen or bathroom (and two pendants
    /// over a kitchen island), a chandelier over a dining table, a pendant in a living room, and a flush ceiling lamp
    /// elsewhere, in the room's middle unless over what they light.
    /// </summary>
    /// <param name="type">The room type.</param>
    /// <param name="room">The room, furnished.</param>
    private static void CeilingLights(RoomType type, LayoutRoom room)
    {
        var (w, l) = (room.Width, room.Length);
        var table = room.Items.FirstOrDefault(i => i.Shape is ItemShape.Table);
        var island = room.Items.FirstOrDefault(i => i.Label.Contains(SL.IslandWord, StringComparison.Ordinal));

        switch (type)
        {
            case RoomType.Kitchen or RoomType.Bathroom:
                room.Light(SL.CeilingLightKeys[CeilingLight.Spotlights], w / 2, l / 2, width: w - 2 * SL.SpotInset, depth: l - 2 * SL.SpotInset);

                if (island is { } i)
                {
                    room.Light(SL.CeilingLightKeys[CeilingLight.Pendant], i.X - i.Width / 4, i.Y);
                    room.Light(SL.CeilingLightKeys[CeilingLight.Pendant], i.X + i.Width / 4, i.Y);
                }

                break;
            case RoomType.DiningRoom:
                room.Light(SL.CeilingLightKeys[CeilingLight.Chandelier], table?.X ?? w / 2, table?.Y ?? l / 2);
                break;
            case RoomType.LivingRoom:
                room.Light(SL.CeilingLightKeys[CeilingLight.Pendant], w / 2, l / 2);
                break;
            default:
                room.Light(SL.CeilingLightKeys[CeilingLight.FlushLight], w / 2, l / 2);
                break;
        }
    }

    /// <summary>
    /// The finish a furnished room of a type starts with: tiled walls in a bathroom, an accent wall behind the bed in a
    /// bedroom (the right wall) and a kids' room, the plain finish elsewhere. Its ceiling lights are objects (see
    /// <see cref="CeilingLights"/>).
    /// </summary>
    /// <param name="type">The room type.</param>
    public static RoomFinish FinishFor(RoomType type)
    {
        var plain = RoomFinish.Default;

        return type switch
        {
            RoomType.Bathroom => new RoomFinish(WallFinish.Tiles, [], plain.CeilingColor, plain.TrimColor, plain.TimeOfDay),
            RoomType.Bedroom => Accent(plain, HexColor.Parse(SL.BedroomAccent)),
            RoomType.KidsRoom => Accent(plain, HexColor.Parse(SL.KidsAccent)),
            _ => plain,
        };
    }

    /// <summary>A finish with the right wall (behind or beside the bed) painted in its own color.</summary>
    /// <param name="plain">The finish to start from.</param>
    /// <param name="color">The wall's color.</param>
    private static RoomFinish Accent(RoomFinish plain, HexColor color)
    {
        return new RoomFinish(plain.Walls, [new WallLook((int)PlanWall.Right, WallFinish.Paint, color)], plain.CeilingColor, plain.TrimColor, plain.TimeOfDay);
    }

    /// <summary>
    /// A sofa along the left wall with art above it, facing a TV stand on the right with the TV hung above it, a coffee
    /// table, ottoman and rug between them, an armchair, lamp, side table, plant and bookshelf, and a radiator under the window.
    /// </summary>
    /// <param name="room">The room.</param>
    private static void LivingRoom(LayoutRoom room)
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
    private static void Bedroom(LayoutRoom room)
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
    private static void Kitchen(LayoutRoom room)
    {
        var (w, l) = (room.Width, room.Length);
        var islandY = 60 + SL.KitchenAisle + 45;
        var islandLength = Math.Clamp(w - 2 * SL.KitchenAisle, 120, 180);
        var tableLength = Math.Clamp(w - 2 * 100, 80, 140);
        var tableY = (60 + SL.KitchenAisle + l) / 2;
        var cookerWidth = room.Get("cooker")?.Width ?? 60;
        var cookerX = LayoutRoom.Within(w * 0.4, cookerWidth / 2 + 40, w - 70 - cookerWidth);
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
    private static void Bathroom(LayoutRoom room)
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
    private static void DiningRoom(LayoutRoom room)
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
    private static void HomeOffice(LayoutRoom room)
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
    private static void KidsRoom(LayoutRoom room)
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
}

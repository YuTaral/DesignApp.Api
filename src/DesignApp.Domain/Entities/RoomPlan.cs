using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Domain.Entities;

/// <summary>
/// A room the user builds from scratch: its type, its walls (an outline on the floor plan whose walls bend at right
/// angles, a rectangle to start with, and the ceiling height) and their colors, and the objects placed on its floor
/// plan. Generating a photo of it makes an ordinary room.
/// </summary>
public sealed class RoomPlan
{
    private IReadOnlyList<PlanItem> _items = [];

    /// <summary>The plan's id.</summary>
    public Guid Id { get; private set; }

    /// <summary>The account or guest who owns it.</summary>
    public string OwnerId { get; private set; } = "";

    /// <summary>Its name.</summary>
    public string Name { get; private set; } = "";

    /// <summary>What kind of room it is, which decides the common objects offered.</summary>
    public RoomType RoomType { get; private set; }

    /// <summary>The room's overall width (its outline's extent across the plan), in metres.</summary>
    public double Width { get; private set; }

    /// <summary>The room's overall length (its outline's extent down the plan), in metres.</summary>
    public double Length { get; private set; }

    /// <summary>The room's walls on the floor plan, numbered clockwise from the top-left corner.</summary>
    public RoomOutline Outline { get; private set; } = RoomOutline.Rectangle(Constants.Plans.MinWallMetres, Constants.Plans.MinWallMetres);

    /// <summary>The ceiling height, in metres.</summary>
    public double Height { get; private set; }

    /// <summary>The walls' color.</summary>
    public HexColor WallColor { get; private set; }

    /// <summary>The floor's color.</summary>
    public HexColor FloorColor { get; private set; }

    /// <summary>What the floor is made of.</summary>
    public FloorType FloorType { get; private set; }

    /// <summary>The number of the wall the 3D view and the photo look at; the camera stands across the room from it.</summary>
    public int View { get; private set; }

    /// <summary>Where the camera stands to look at <see cref="View"/>: across from its middle, or in a near corner.</summary>
    public ViewCorner Corner { get; private set; }

    /// <summary>The objects placed in the room.</summary>
    public IReadOnlyList<PlanItem> Items => _items;

    /// <summary>The AI its photos are made with (and the rooms made from them use).</summary>
    public AiMode AiMode { get; private set; }

    /// <summary>When it was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When it was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The plan's own design, which the rooms of its generated photos go into; <c>null</c> until there is one.</summary>
    public Guid? DesignId { get; private set; }

    private RoomPlan() { } // for EF Core

    /// <summary>Starts a room with its walls and no objects.</summary>
    /// <param name="id">The new plan's id.</param>
    /// <param name="ownerId">The account or guest who builds it.</param>
    /// <param name="name">Its name.</param>
    /// <param name="roomType">What kind of room it is.</param>
    /// <param name="width">The width, in metres.</param>
    /// <param name="length">The length, in metres.</param>
    /// <param name="height">The ceiling height, in metres.</param>
    /// <param name="wallColor">The walls' color.</param>
    /// <param name="floorColor">The floor's color.</param>
    /// <param name="floorType">What the floor is made of.</param>
    /// <param name="aiMode">The AI its photos are made with.</param>
    /// <param name="now">The current time.</param>
    /// <exception cref="DomainException">The name, type, walls, floor or AI are invalid.</exception>
    public static RoomPlan Create(Guid id, string ownerId, string name, RoomType roomType, double width, double length, double height,
        HexColor wallColor, HexColor floorColor, FloorType floorType, AiMode aiMode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        if (!Enum.IsDefined(roomType))
        {
            throw new DomainException(Constants.Messages.UnknownRoomType);
        }

        var plan = new RoomPlan
        {
            Id = id,
            OwnerId = ownerId,
            RoomType = roomType,
            AiMode = Enum.IsDefined(aiMode) ? aiMode : throw new DomainException(Constants.Messages.UnknownAiMode(aiMode)),
            CreatedAt = now,
        };

        plan.Update(name, RectangleOutline(width, length), height, wallColor, floorColor, floorType, (int)PlanWall.Back, [], now);

        return plan;
    }

    /// <summary>Saves the room as the user left it: its name, walls, colors, floor, view and objects.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="outline">Its walls on the floor plan.</param>
    /// <param name="height">The ceiling height, in metres.</param>
    /// <param name="wallColor">The walls' color.</param>
    /// <param name="floorColor">The floor's color.</param>
    /// <param name="floorType">What the floor is made of.</param>
    /// <param name="view">The number of the wall the 3D view and the photo look at.</param>
    /// <param name="items">The objects.</param>
    /// <param name="now">The current time.</param>
    /// <param name="corner">Where the camera stands to look at the wall in view.</param>
    /// <exception cref="DomainException">
    /// The name, ceiling, floor, view or number of objects are invalid, or an object on a wall is on no wall of the room
    /// or doesn't fit on its wall.
    /// </exception>
    public void Update(string name, RoomOutline outline, double height, HexColor wallColor, HexColor floorColor,
        FloorType floorType, int view, IReadOnlyList<PlanItem> items, DateTimeOffset now, ViewCorner corner = ViewCorner.None)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(items);
        var trimmed = (name ?? "").Trim();

        if (trimmed.Length > Constants.Plans.MaxNameLength)
        {
            throw new DomainException(Constants.Messages.PlanName);
        }

        if (height is < Constants.Plans.MinHeightMetres or > Constants.Plans.MaxHeightMetres or double.NaN)
        {
            throw new DomainException(Constants.Messages.PlanWalls);
        }

        if (items.Count > Constants.Plans.MaxItems)
        {
            throw new DomainException(Constants.Messages.PlanItems);
        }

        if (!Enum.IsDefined(floorType) || view < 0 || view >= outline.Walls.Count || !Enum.IsDefined(corner))
        {
            throw new DomainException(Constants.Messages.PlanLook);
        }

        foreach (var item in items)
        {
            if (item.Wall is not { } wall)
            {
                continue;
            }

            if (wall >= outline.Walls.Count)
            {
                throw new DomainException(Constants.Messages.PlanWallPlacement);
            }

            var (along, ceiling, slack) = (outline.WallLength(wall), height * 100, Constants.Plans.WallTolerance);

            if (item.X - item.Width / 2 < -slack || item.X + item.Width / 2 > along + slack || item.Elevation + item.Height > ceiling + slack)
            {
                throw new DomainException(Constants.Messages.PlanWallItem(item.Label));
            }
        }

        (Name, Outline, Width, Length, Height, WallColor, FloorColor) = (trimmed, outline, outline.WidthMetres, outline.LengthMetres, height, wallColor, floorColor);
        (FloorType, View, Corner) = (floorType, view, corner);
        _items = [.. items];
        UpdatedAt = now;
    }

    /// <summary>Saves a rectangular room (as <see cref="Update(string, RoomOutline, double, HexColor, HexColor, FloorType, int, IReadOnlyList{PlanItem}, DateTimeOffset, ViewCorner)"/>).</summary>
    /// <param name="name">Its name.</param>
    /// <param name="width">The width, in metres.</param>
    /// <param name="length">The length, in metres.</param>
    /// <param name="height">The ceiling height, in metres.</param>
    /// <param name="wallColor">The walls' color.</param>
    /// <param name="floorColor">The floor's color.</param>
    /// <param name="floorType">What the floor is made of.</param>
    /// <param name="view">The wall the 3D view and the photo look at.</param>
    /// <param name="items">The objects.</param>
    /// <param name="now">The current time.</param>
    /// <param name="corner">Where the camera stands to look at the wall in view.</param>
    /// <exception cref="DomainException">The room is too small or too big, or see the other overload.</exception>
    public void Update(string name, double width, double length, double height, HexColor wallColor, HexColor floorColor,
        FloorType floorType, PlanWall view, IReadOnlyList<PlanItem> items, DateTimeOffset now, ViewCorner corner = ViewCorner.None)
    {
        if (!double.IsFinite(width) || !double.IsFinite(length))
        {
            throw new DomainException(Constants.Messages.PlanWalls);
        }

        if (!Enum.IsDefined(view))
        {
            throw new DomainException(Constants.Messages.PlanLook);
        }

        Update(name, RectangleOutline(width, length), height, wallColor, floorColor, floorType, (int)view, items, now, corner);
    }

    /// <summary>How long a wall is, in centimetres.</summary>
    /// <param name="wall">The wall's number.</param>
    public double WallCentimetres(int wall)
    {
        return Outline.WallLength(wall);
    }

    /// <summary>Makes a design the plan's own: the rooms of its generated photos go into it.</summary>
    /// <param name="designId">The design.</param>
    public void UseDesign(Guid designId)
    {
        DesignId = designId;
    }

    /// <summary>Gives the plan to another user: a guest who signs in keeps the plans made before, under the account.</summary>
    /// <param name="newOwnerId">The account's user id.</param>
    public void TransferTo(string newOwnerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newOwnerId);
        OwnerId = newOwnerId;
    }

    /// <summary>A rectangular room's outline, with the walls' message when its size is out of bounds.</summary>
    /// <param name="width">The width, in metres.</param>
    /// <param name="length">The length, in metres.</param>
    /// <exception cref="DomainException">The room is too small or too big.</exception>
    public static RoomOutline RectangleOutline(double width, double length)
    {
        if (width is < Constants.Plans.MinWallMetres or > Constants.Plans.MaxWallMetres || length is < Constants.Plans.MinWallMetres or > Constants.Plans.MaxWallMetres)
        {
            throw new DomainException(Constants.Messages.PlanWalls);
        }

        return RoomOutline.Rectangle(width, length);
    }
}

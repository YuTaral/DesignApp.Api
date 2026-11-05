namespace DesignApp.Domain.Rooms;

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

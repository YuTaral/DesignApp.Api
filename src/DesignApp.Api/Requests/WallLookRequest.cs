using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Requests;

/// <summary>A wall of a built room in its own finish and color, as sent and as returned.</summary>
/// <param name="Wall">The wall's number.</param>
/// <param name="Finish">What it is finished in.</param>
/// <param name="Color">Its color as #RRGGBB.</param>
public sealed record WallLookRequest(int Wall, WallFinish Finish, string? Color)
{
    /// <summary>The wall's look as the domain keeps it.</summary>
    /// <exception cref="Domain.Exceptions.DomainException">The wall, finish or color is invalid.</exception>
    public WallLook ToDomain()
    {
        return new WallLook(Wall, Finish, HexColor.Parse(Color ?? ""));
    }

    /// <summary>A wall's look as the API sends it.</summary>
    /// <param name="look">The look.</param>
    public static WallLookRequest From(WallLook look)
    {
        ArgumentNullException.ThrowIfNull(look);

        return new(look.Wall, look.Finish, look.Color.Value);
    }
}

using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.Api.Requests;

/// <summary>A kitchen unit's worktop and the backsplash behind it, as sent and as returned.</summary>
/// <param name="Material">What the worktop is made of, such as "quartz".</param>
/// <param name="Color">Its color as #RRGGBB.</param>
/// <param name="Backsplash">What covers the wall behind it.</param>
/// <param name="BacksplashColor">The tiles' or glass's color as #RRGGBB.</param>
public sealed record WorktopRequest(string? Material, string? Color, BacksplashKind Backsplash, string? BacksplashColor)
{
    /// <summary>The worktop as the domain keeps it.</summary>
    /// <exception cref="Domain.Exceptions.DomainException">The material, a color or the backsplash is invalid.</exception>
    public Worktop ToDomain()
    {
        return new Worktop(Material, HexColor.Parse(Color ?? ""), Backsplash, HexColor.Parse(BacksplashColor ?? ""));
    }

    /// <summary>A worktop as the API sends it.</summary>
    /// <param name="worktop">The worktop; <c>null</c> for none.</param>
    public static WorktopRequest? From(Worktop? worktop)
    {
        return worktop is null ? null : new(worktop.Material, worktop.Color.Value, worktop.Backsplash, worktop.BacksplashColor.Value);
    }
}

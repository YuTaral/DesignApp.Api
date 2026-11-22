namespace DesignApp.Application.Objects;

/// <summary>A room as seen looking at one of its walls (the far wall), in centimetres from the photo's left wall.</summary>
/// <param name="Across">From the photo's left wall to its right wall.</param>
/// <param name="Deep">From the far wall to the wall facing it (the near wall).</param>
/// <param name="Middle">The far wall's middle.</param>
/// <param name="NearLeft">The near wall's left end (the left wall when there is no near wall).</param>
/// <param name="NearRight">The near wall's right end.</param>
/// <param name="Spot">The plan's camera spot: from the left wall and from the far wall; <c>null</c> when there is none.</param>
public sealed record ViewGeometry(double Across, double Deep, double Middle, double NearLeft, double NearRight, (double FromLeft, double FromFar)? Spot);

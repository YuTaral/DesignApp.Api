namespace DesignApp.Infrastructure.Library;

/// <summary>A layout's depth map's camera, in its <c>.objects.json</c> (the map itself is <c>&lt;layout&gt;.depth.png</c>).</summary>
/// <param name="FovX">The horizontal field of view, in degrees.</param>
/// <param name="FovY">The vertical field of view, in degrees.</param>
internal sealed record LibraryDepthFile(double FovX, double FovY);

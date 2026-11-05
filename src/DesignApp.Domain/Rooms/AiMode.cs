namespace DesignApp.Domain.Rooms;

/// <summary>Which AI analyzes a room photo.</summary>
public enum AiMode
{
    /// <summary>The built-in fakes: free, offline and instant, with hand-traced or recorded outlines for the sample photos.</summary>
    Fake,
    /// <summary>Claude and SAM 3: real outlines for any photo, at a few cents per analysis.</summary>
    Real,
}

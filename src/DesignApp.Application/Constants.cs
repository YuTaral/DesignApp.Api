namespace DesignApp.Application;

/// <summary>
/// Every fixed value of the use cases in one place: the limits of photos, the analysis and brush
/// strokes, how long unsaved uploads are kept, storage keys, and the messages shown to users.
/// </summary>
public static class Constants
{
    /// <summary>Limits for the AI analysis of a room photo.</summary>
    public static class Analysis
    {
        /// <summary>
        /// The AI works on a copy of the photo scaled to this long edge. More pixels cost more and are
        /// slower to send without finding objects any better; the label map is made at this size too.
        /// </summary>
        public const int MaxLongEdge = 2048;

        /// <summary>At most this many objects are kept per room, so the editor's object list stays usable.</summary>
        public const int MaxObjects = 40;

        /// <summary>Objects covering less of the photo than this (after overlaps) are dropped as noise.</summary>
        public const double MinAreaFraction = 0.002;
    }

    /// <summary>Limits for brush strokes on an outline.</summary>
    public static class Strokes
    {
        /// <summary>The most points one stroke may have.</summary>
        public const int MaxPoints = 10_000;

        /// <summary>The smallest brush radius, in label map pixels.</summary>
        public const double MinRadius = 0.5;

        /// <summary>The largest brush radius, in label map pixels.</summary>
        public const double MaxRadius = 400;

        /// <summary>How far left of or above the photo a point may lie, as a fraction of its size.</summary>
        public const double MinPoint = -1;

        /// <summary>How far right of or below the photo a point may lie, as a fraction of its size.</summary>
        public const double MaxPoint = 2;
    }

    /// <summary>Limits for removing objects from a photo.</summary>
    public static class Removals
    {
        /// <summary>The most objects one removal may take out together (a sofa with its cushions and throw).</summary>
        public const int MaxObjects = 20;
    }

    /// <summary>Limits for an object the user marks by hand.</summary>
    public static class Marks
    {
        /// <summary>The most strokes one mark may have.</summary>
        public const int MaxStrokes = 100;

        /// <summary>The longest name a hand-marked object may have.</summary>
        public const int MaxLabelLength = 60;
    }

    /// <summary>Limits that every uploaded photo must meet, and how it is stored.</summary>
    public static class Photos
    {
        /// <summary>One mebibyte, for limits given in MB.</summary>
        public const long Megabyte = 1024 * 1024;

        /// <summary>The largest upload in MB. Matches the frontend's limit; large enough for any phone photo.</summary>
        public const int MaxUploadMegabytes = 15;

        /// <summary>The largest upload in bytes.</summary>
        public const long MaxUploadBytes = MaxUploadMegabytes * Megabyte;

        /// <summary>Below this the AI cannot find objects reliably.</summary>
        public const int MinShortEdge = 256;

        /// <summary>Larger photos are scaled down: more pixels cost more to analyze without improving results.</summary>
        public const int MaxLongEdge = 4096;

        /// <summary>The most megapixels an upload may have; refused before decoding, so a small file can't exhaust memory.</summary>
        public const int MaxSourceMegapixels = 100;

        /// <summary>The most pixels an upload may have.</summary>
        public const long MaxSourcePixels = MaxSourceMegapixels * 1_000_000L;

        /// <summary>JPEG quality (0-100) for stored photos: visually lossless at a reasonable size.</summary>
        public const int JpegQuality = 90;
    }

    /// <summary>MIME types of the files the app stores and serves.</summary>
    public static class ContentTypes
    {
        /// <summary>Photos: processed photos are always JPEG.</summary>
        public const string Jpeg = "image/jpeg";

        /// <summary>Label maps.</summary>
        public const string Png = "image/png";
    }

    /// <summary>How rooms are stored and kept.</summary>
    public static class Rooms
    {
        /// <summary>How long an upload is kept without being saved to a design: long enough to edit it, reload, or sign in.</summary>
        public static readonly TimeSpan UnsavedLifetime = TimeSpan.FromHours(24);

        /// <summary>How many expired rooms one cleanup pass deletes, so a big backlog is cleared in steps.</summary>
        public const int CleanupBatchSize = 100;

        /// <summary>Storage key of a room's photo. One folder per room keeps its files together.</summary>
        /// <param name="roomId">The room's id.</param>
        /// <param name="version">The photo version: 0 is the uploaded photo, then one per object removed from it.</param>
        public static string PhotoKey(Guid roomId, int version = 0)
        {
            return version == 0 ? $"rooms/{roomId}/photo.jpg" : $"rooms/{roomId}/photo-{version}.jpg";
        }

        /// <summary>Storage key of a room's label map, as the analysis made it or after outline fixes.</summary>
        /// <param name="roomId">The room's id.</param>
        /// <param name="revision">The label map version: 0 from the analysis, then one per fix.</param>
        public static string LabelMapKey(Guid roomId, int revision = 0)
        {
            return revision == 0 ? $"rooms/{roomId}/labels.png" : $"rooms/{roomId}/labels-{revision}.png";
        }
    }

    /// <summary>The built-in library of ready-made designs.</summary>
    public static class Library
    {
        /// <summary>How a copied room names the library layout it came from, such as <c>minimalist/kitchen/2</c>.</summary>
        /// <param name="designId">The library design's id.</param>
        /// <param name="roomId">The library room's id.</param>
        /// <param name="layoutId">The layout's id.</param>
        public static string RoomName(string designId, string roomId, string layoutId)
        {
            return $"{designId}/{roomId}/{layoutId}";
        }
    }

    /// <summary>Owner ids of visitors who haven't signed in.</summary>
    public static class Guests
    {
        /// <summary>Starts every guest id, keeping them apart from account ids.</summary>
        public const string Prefix = "guest:";

        /// <summary>How a guest's UUID is written: 32 hex digits, no dashes.</summary>
        public const string TokenFormat = "N";
    }

    /// <summary>The messages the use cases show to users.</summary>
    public static class Messages
    {
        /// <summary>The analysis failed unexpectedly. The details are only logged.</summary>
        public const string UnexpectedFailure = "We couldn't analyze this photo. Try uploading it again in a moment.";

        /// <summary>The analysis found no objects worth restyling.</summary>
        public const string NothingFound = "We couldn't find any objects to restyle in this photo. Try a wider shot of the room.";

        /// <summary>The editor is on an outline version that is no longer current (another tab changed it).</summary>
        public const string UnknownRevision = "This room has changed since you opened it. Reload the page and try again.";

        /// <summary>A stroke on a room that isn't analyzed yet.</summary>
        public const string OutlinesNotReady = "The outlines can be fixed once the analysis is ready.";

        /// <summary>A stroke for an object the room doesn't have.</summary>
        public const string UnknownStrokeObject = "That object isn't in this room.";

        /// <summary>A stroke with too few or too many points.</summary>
        public static readonly string StrokePoints = $"A stroke needs between 1 and {Strokes.MaxPoints} points.";

        /// <summary>A brush that is too small or too large.</summary>
        public static readonly string BrushRadius = $"The brush radius must be between {Strokes.MinRadius} and {Strokes.MaxRadius} pixels.";

        /// <summary>A stroke far outside the photo.</summary>
        public const string StrokeOutside = "The stroke has points far outside the photo.";

        /// <summary>A box stroke without exactly two corners.</summary>
        public const string BoxPoints = "A box needs its two corners.";

        /// <summary>A lasso stroke with too few points to enclose anything.</summary>
        public const string LassoPoints = "A lasso needs at least 3 points.";

        /// <summary>A stroke with a shape that doesn't exist.</summary>
        public const string UnknownStrokeShape = "Pick the brush, the box or the lasso.";

        /// <summary>A new object with too few or too many strokes.</summary>
        public static readonly string MarkStrokes = $"Mark the object with between 1 and {Marks.MaxStrokes} strokes.";

        /// <summary>A new object without a usable name.</summary>
        public static readonly string ObjectName = $"Give the object a name of up to {Marks.MaxLabelLength} characters.";

        /// <summary>A new object with a type that doesn't exist.</summary>
        public const string UnknownObjectType = "Pick the object's type from the list.";

        /// <summary>A removal with no objects or too many.</summary>
        public static readonly string RemovalCount = $"Pick between 1 and {Removals.MaxObjects} objects to remove.";

        /// <summary>Removing an object that has no pixels in the outlines shown.</summary>
        public const string ObjectNotInPhoto = "That object isn't in this version of the photo.";

        /// <summary>Removing a wall, floor or ceiling, which can only be recolored.</summary>
        public const string SurfaceRemoval = "Walls, floors and ceilings can't be removed. Recolor them instead.";

        /// <summary>A room that has used up its object numbers.</summary>
        public const string TooManyObjects = "This room can't hold any more objects.";

        /// <summary>A room that is in a design already (a room belongs to one design).</summary>
        public const string RoomInADesign = "This room is already in a design.";
    }

    /// <summary>Whether a brush stroke adds pixels to the object or takes them away.</summary>
    public enum OutlineBrushMode
    {
        /// <summary>Every covered pixel becomes the object's, even if another object had it.</summary>
        Paint,
        /// <summary>Covered pixels of the object become unassigned; other objects are untouched.</summary>
        Erase,
    }

    /// <summary>What a stroke covers: a round brush along its path, or the area its points enclose.</summary>
    public enum StrokeShape
    {
        /// <summary>A round brush stamped along the path (the radius sets its size).</summary>
        Brush,
        /// <summary>A rectangle between two opposite corners; the radius is ignored.</summary>
        Box,
        /// <summary>The inside of the path, closed from the last point back to the first (even-odd rule); the radius is ignored.</summary>
        Lasso,
    }
}

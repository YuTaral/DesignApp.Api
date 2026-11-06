using DesignApp.Domain.Rooms;

namespace DesignApp.Domain;

/// <summary>
/// Every fixed value of the domain in one place: the rules' limits and the messages shown when a
/// rule is broken. Other layers read the limits from here too, so they never drift apart.
/// </summary>
public static class Constants
{
    /// <summary>Limits of a design.</summary>
    public static class Designs
    {
        /// <summary>The longest allowed design name, after trimming.</summary>
        public const int MaxNameLength = 80;

        /// <summary>The longest allowed description, after trimming.</summary>
        public const int MaxDescriptionLength = 300;

        /// <summary>The most rooms one design can hold.</summary>
        public const int MaxRooms = 20;
    }

    /// <summary>Limits of a room and its analysis.</summary>
    public static class Rooms
    {
        /// <summary>The label map stores one byte per pixel, and 0 means "no object", so indexes run from 1 to 255.</summary>
        public const int MaxObjectIndex = 255;

        /// <summary>The label map value of a pixel that belongs to no object.</summary>
        public const byte NoObject = 0;

        /// <summary>How often analysis may start before the room is failed, so a photo that crashes the worker can't loop forever.</summary>
        public const int MaxAnalysisAttempts = 3;
    }

    /// <summary>Boxes in normalized image coordinates.</summary>
    public static class Boxes
    {
        /// <summary>Allows for rounding when a box touches the right or bottom edge.</summary>
        public const double Tolerance = 1e-4;
    }

    /// <summary>Colors and recolors.</summary>
    public static class Colors
    {
        /// <summary>The first character of every color.</summary>
        public const char Prefix = '#';

        /// <summary>The length of a "#RRGGBB" color.</summary>
        public const int Length = 7;

        /// <summary>The color used when one can't be read: a neutral gray.</summary>
        public const string Fallback = "#808080";

        /// <summary>The weakest recolor strength: the original color.</summary>
        public const double MinStrength = 0;

        /// <summary>The strongest recolor strength: the new color only.</summary>
        public const double MaxStrength = 1;
    }

    /// <summary>The messages shown when a domain rule is broken.</summary>
    public static class Messages
    {
        /// <summary>A design name that is empty or too long.</summary>
        public static readonly string NameLength = $"Design name must be 1 to {Designs.MaxNameLength} characters.";

        /// <summary>A description that is too long.</summary>
        public static readonly string DescriptionLength = $"The description can be at most {Designs.MaxDescriptionLength} characters.";

        /// <summary>A room of another user.</summary>
        public const string NotYourRoom = "Designs can only hold your own rooms.";

        /// <summary>A room added twice to one design.</summary>
        public const string RoomAlreadyInThisDesign = "This room is already in the design.";

        /// <summary>A room added to a design that is full.</summary>
        public static readonly string DesignFull = $"A design can hold at most {Designs.MaxRooms} rooms.";

        /// <summary>A room that isn't in the design.</summary>
        public const string RoomNotInDesign = "This room isn't in the design.";

        /// <summary>Colors for a room that hasn't been analyzed yet.</summary>
        public const string ColorsNeedAnalysis = "Colors can only be saved for rooms that finished analysis.";

        /// <summary>Removing the last room of a design.</summary>
        public const string LastRoom = "A design needs at least one room. Delete the design instead.";

        /// <summary>A recolor strength outside 0..1.</summary>
        public static readonly string StrengthRange = $"Recolor strength must be between {Colors.MinStrength} and {Colors.MaxStrength}.";

        /// <summary>A room without an id.</summary>
        public const string RoomNeedsId = "A room needs an id.";

        /// <summary>A photo with a zero or negative size.</summary>
        public const string PhotoSize = "Photo dimensions must be positive.";

        /// <summary>A ready room without objects.</summary>
        public const string ReadyNeedsObjects = "A ready room needs at least one detected object.";

        /// <summary>Two objects with the same index.</summary>
        public const string DuplicateObjectIndexes = "Detected object indexes must be unique.";

        /// <summary>An object index outside 1..255.</summary>
        public static readonly string ObjectIndexRange = $"Detected object indexes must be between 1 and {Rooms.MaxObjectIndex}.";

        /// <summary>An edit for an object the room doesn't have.</summary>
        /// <param name="index">The object index.</param>
        public static string UnknownObject(int index) => $"Object {index} does not exist in this room.";

        /// <summary>Two edits for one object.</summary>
        /// <param name="index">The object index.</param>
        public static string DuplicateEdit(int index) => $"Object {index} has more than one edit.";

        /// <summary>Text that isn't a #RRGGBB color.</summary>
        /// <param name="input">The text.</param>
        public static string NotAColor(string? input) => $"'{input}' is not a #RRGGBB color.";

        /// <summary>A box that reaches outside the image.</summary>
        /// <param name="x">Left edge.</param>
        /// <param name="y">Top edge.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        public static string BoxOutside(double x, double y, double width, double height) =>
            $"Bounding box ({x}, {y}, {width}, {height}) is outside the image.";

        /// <summary>An analysis scope that doesn't exist.</summary>
        /// <param name="scope">The scope.</param>
        public static string UnknownScope(AnalysisScope scope) => $"Unknown analysis scope {scope}.";

        /// <summary>An AI mode that doesn't exist.</summary>
        /// <param name="mode">The mode.</param>
        public static string UnknownAiMode(AiMode mode) => $"Unknown AI mode {mode}.";

        /// <summary>An analysis started more often than allowed.</summary>
        public static readonly string TooManyAttempts = $"Analysis has already been tried {Rooms.MaxAnalysisAttempts} times.";

        /// <summary>Restarting the analysis of a room that isn't analyzing.</summary>
        /// <param name="status">The room's status.</param>
        public static string CannotRestart(RoomStatus status) => $"Cannot restart the analysis of a room that is {status}.";

        /// <summary>Fixing the outlines of a room that isn't ready.</summary>
        /// <param name="status">The room's status.</param>
        public static string CannotFixOutlines(RoomStatus status) => $"Cannot fix the outlines of a room that is {status}.";

        /// <summary>Switching the outlines of a room that isn't ready.</summary>
        /// <param name="status">The room's status.</param>
        public static string CannotChangeOutlines(RoomStatus status) => $"Cannot change the outlines of a room that is {status}.";

        /// <summary>A label map version that doesn't exist.</summary>
        /// <param name="revision">The version.</param>
        public static string NoLabelMapRevision(int revision) => $"The room has no label map version {revision}.";

        /// <summary>Failing a room that already finished.</summary>
        /// <param name="status">The room's status.</param>
        public static string CannotFail(RoomStatus status) => $"Cannot fail a room that is already {status}.";

        /// <summary>A status change that isn't allowed.</summary>
        /// <param name="from">The current status.</param>
        /// <param name="to">The requested status.</param>
        public static string CannotMove(RoomStatus from, RoomStatus to) => $"Cannot move a room from {from} to {to}.";
    }
}

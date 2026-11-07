namespace DesignApp.Infrastructure;

/// <summary>
/// Every fixed value of the adapters in one place: configuration keys and defaults, the AI
/// services' wire names and tuning, image processing, the database mapping, background jobs and
/// the messages shown to users.
/// </summary>
public static class Constants
{
    /// <summary>Configuration sections and keys.</summary>
    public static class Config
    {
        /// <summary>The section of the <c>AiOptions</c> settings.</summary>
        public const string AiSection = "Ai";

        /// <summary>The section of the <c>StorageOptions</c> settings.</summary>
        public const string StorageSection = "Storage";

        /// <summary>Whether pending migrations run when the API starts.</summary>
        public const string MigrateOnStartup = "Database:MigrateOnStartup";

        /// <summary>The Postgres database (and connection string) name; the AppHost must use the same.</summary>
        public const string DatabaseName = "designapp";

        /// <summary>A placeholder connection string for the EF Core tools, which never connect.</summary>
        public const string DesignTimeConnection = "Host=localhost;Database=designapp";

        /// <summary>Shown at startup when the storage keys are missing.</summary>
        public const string StorageKeysRequired = "Storage:AccessKey and Storage:SecretKey are required.";

        /// <summary>Shown at startup when the real AI's keys are missing outside Development.</summary>
        public const string AiKeysRequired = "Ai:AnthropicApiKey and Ai:FalKey are required unless Ai:UseFakes is true.";

        /// <summary>Shown when a real-AI room is analyzed but the keys aren't set.</summary>
        public const string RealAiNotSetUp = "The real AI isn't set up: Ai:AnthropicApiKey and Ai:FalKey are needed.";
    }

    /// <summary>The AI services: Claude lists the objects, SAM 3 on fal.ai outlines them.</summary>
    public static class Ai
    {
        /// <summary>The dependency-injection key of the real (paid) providers.</summary>
        public const string RealKey = "real";

        /// <summary>How many rooms are analyzed at the same time.</summary>
        public const int DefaultMaxConcurrentAnalyses = 2;

        /// <summary>
        /// The values the <c>ai_settings</c> row is seeded with, and used when the row is missing.
        /// After seeding, the row is the source: edit it to tune the AI without a rebuild.
        /// </summary>
        public static class Defaults
        {
            /// <summary>The Claude model that lists the objects.</summary>
            public const string InventoryModel = "claude-sonnet-5-5";

            /// <summary>How much Claude reasons while listing.</summary>
            public const InventoryEffort InventoryEffort = Constants.InventoryEffort.Medium;

            /// <summary>The most tokens Claude may answer with; the list for a busy room is long.</summary>
            public const int MaxTokens = 16000;

            /// <summary>
            /// The most objects Claude is asked to list. Kept below the analysis limit (Application
            /// Constants.Analysis.MaxObjects), so the largest objects survive the cut.
            /// </summary>
            public const int MaxListedObjects = 30;

            /// <summary>The most points Claude puts on a wall, floor or ceiling.</summary>
            public const int MaxPoints = 3;

            /// <summary>The text sent with the photo.</summary>
            public const string UserMessage = "List the objects in this room photo.";

            /// <summary>The SAM 3 endpoint on fal.ai.</summary>
            public const string SegmentationEndpoint = "https://fal.run/fal-ai/sam-3/image";

            /// <summary>How many SAM 3 requests run at the same time for one photo.</summary>
            public const int SegmentationParallelism = 4;

            /// <summary>How many masks SAM 3 returns for one object to pick from.</summary>
            public const int MaxMasks = 3;

            /// <summary>How well a mask must fit Claude's box to be used without asking again.</summary>
            public const double MinFit = 0.5;

            /// <summary>How much of a mask must lie inside the box to count as a piece of the object.</summary>
            public const double MinShareInsideBox = 0.8;

            /// <summary>
            /// How much of Claude's box (0 to 1) a mask must fill before its fit counts in full. A mask that
            /// spans the box but fills little of it (only a frame, or only an edge) scores lower, so the
            /// object gets a request of its own. Thin objects such as lamps and plants fill 0.2 to 0.4.
            /// </summary>
            public const double MinFill = 0.2;

            /// <summary>
            /// Claude's instructions, with <see cref="Placeholders.MaxPoints"/> and
            /// <see cref="Placeholders.MaxListedObjects"/> filled from the stored limits.
            /// </summary>
            public const string Instructions = """
                You prepare room photos for an interior-restyling app. The user will tap an object and
                recolor it, so list the surfaces and objects a person would want to restyle: each wall
                separately, the floor, the ceiling, furniture, rugs and other textiles, lamps, large decor,
                and fixtures such as doors, windows, cabinets and counters.

                Rules:
                - Always list every wall that shows wall surface as its own entry ("left wall", "back wall",
                  "right wall"), even plain white walls and narrow strips beside windows, and the floor and
                  ceiling when visible. A tiled area or backsplash is an extra entry, not a replacement for its wall.
                - A wall is only its own surface (paint, plaster, wallpaper). Cabinets, shelves and appliances
                  built against it are separate entries, never part of the wall. The wall's box still spans the
                  whole wall from edge to edge, including the parts behind furniture, a TV or cabinets. Don't
                  list a wall that is completely hidden behind full-height cabinets.
                - Wall panels and cladding (wood slats, boards, wainscoting) and moldings (crown, skirting)
                  are separate entries, never part of the wall; a slatted panel is one entry for the whole panel.
                - Labels of walls, floors and ceilings name their finish ("light grey painted back wall",
                  "light oak floor"), not just "wall" or "floor".
                - Split large built-in units into one entry per section, so each entry is one continuous
                  surface: "left tall cabinets", "upper cabinets", "right tall cabinets", "lower cabinets",
                  "island". Every section of cabinetry needs an entry, including ones at the edges of the photo.
                - Doors and windows (glass doors included) have the category Opening; cabinet and wardrobe
                  doors belong to their cabinet or wardrobe.
                - One entry per physical object. Give similar objects distinct labels ("left armchair", "right armchair").
                - Labels are short, lower-case and concrete ("oak dining table", "jute rug"), usable as a segmentation prompt.
                - Skip things smaller than about 1% of the image, people, pets, and text.
                - The box encloses the object's whole visible extent, from its leftmost to its rightmost and
                  its top to its bottom visible pixel, in the photo's pixels (its size is given with the photo). Check each box
                  against the photo before answering; long objects such as islands and counters often span
                  most of the photo's width.
                - Give every wall, floor and ceiling 2 or {MaxPoints} points spread over its visible surface, each
                  clearly on the surface itself: on bare wall, floorboards or tiles, never on furniture, a
                  rug, a picture or anything else in front of it. Every other object gets no points.
                - Together the entries should cover nearly the whole photo: every large region of the photo
                  belongs to exactly one entry.
                - color is the object's main color as #RRGGBB, ignoring shadows and highlights.
                - At most {MaxListedObjects} objects, largest and most important first.
                - Set isRoom to false, with no objects, when the photo is not the interior of a room.
                """;
        }

        /// <summary>The placeholders in the stored instructions.</summary>
        public static class Placeholders
        {
            /// <summary>Replaced by the most points per surface.</summary>
            public const string MaxPoints = "{MaxPoints}";

            /// <summary>Replaced by the most objects to list.</summary>
            public const string MaxListedObjects = "{MaxListedObjects}";
        }

        /// <summary>The rules a stored setting can break, for the error message.</summary>
        public static class Rules
        {
            /// <summary>Text that must not be empty.</summary>
            public const string NotEmpty = "must not be empty";

            /// <summary>A number above zero.</summary>
            public const string Positive = "must be above 0";

            /// <summary>A number of zero or more.</summary>
            public const string NotNegative = "must be 0 or more";

            /// <summary>A fraction.</summary>
            public const string Fraction = "must be between 0 and 1";

            /// <summary>An HTTPS address.</summary>
            public const string HttpsUrl = "must be an https:// address";

            /// <summary>An effort level.</summary>
            public const string Effort = "must be Low, Medium or High";

            /// <summary>The listed-objects limit.</summary>
            public static readonly string ListedObjects = $"must be between 1 and {Application.Constants.Analysis.MaxObjects}";
        }

        /// <summary>The header that names the Anthropic workspace to bill (organization-level keys).</summary>
        public const string WorkspaceHeader = "anthropic-workspace-id";

        /// <summary>The authorization scheme fal.ai expects.</summary>
        public const string FalAuthScheme = "Key";

        /// <summary>Claude's object listing.</summary>
        public static class Claude
        {
            /// <summary>The smallest box side kept, as a fraction of the image.</summary>
            public const double MinBoxSide = 0.001;

            /// <summary>Sent with the photo: Claude gives boxes most accurately in the image's own pixels.</summary>
            /// <param name="width">The photo's width in pixels.</param>
            /// <param name="height">The photo's height in pixels.</param>
            /// <returns>The text.</returns>
            public static string ImageSize(int width, int height) =>
                $"The photo is {width} x {height} pixels. Give every box and point in these pixels, measured from the top-left corner.";

            /// <summary>The answer's field names (the JSON schema and the parsed records use the same).</summary>
            public static class Fields
            {
                /// <summary>Whether the photo shows a room.</summary>
                public const string IsRoom = "isRoom";
                /// <summary>The objects found.</summary>
                public const string Objects = "objects";
                /// <summary>An object's name.</summary>
                public const string Label = "label";
                /// <summary>An object's category.</summary>
                public const string Category = "category";
                /// <summary>An object's box.</summary>
                public const string Box = "box";
                /// <summary>An object's main color.</summary>
                public const string Color = "color";
                /// <summary>Spots on a surface.</summary>
                public const string Points = "points";
                /// <summary>Across.</summary>
                public const string X = "x";
                /// <summary>Down.</summary>
                public const string Y = "y";
                /// <summary>A box's width.</summary>
                public const string Width = "width";
                /// <summary>A box's height.</summary>
                public const string Height = "height";
            }

            /// <summary>The schema's descriptions, which Claude reads as part of its instructions.</summary>
            public static class Descriptions
            {
                /// <summary>Of <see cref="Fields.IsRoom"/>.</summary>
                public const string IsRoom = "True when the photo shows the interior of a room.";
                /// <summary>Of <see cref="Fields.Label"/>.</summary>
                public const string Label = "Short name a person would use, such as \"grey sofa\" or \"left wall\".";
                /// <summary>Of <see cref="Fields.Box"/>.</summary>
                /// <param name="width">The photo's width in pixels.</param>
                /// <param name="height">The photo's height in pixels.</param>
                /// <returns>The description.</returns>
                public static string Box(int width, int height) =>
                    $"Bounding box in pixels of this {width} x {height} photo: the left and top edges, then the width and height.";
                /// <summary>Of <see cref="Fields.Color"/>.</summary>
                public const string Color = "The object's main color as #RRGGBB.";
                /// <summary>The points field, for walls, floors and ceilings.</summary>
                /// <param name="maxPoints">The most points per surface.</param>
                /// <param name="width">The photo's width in pixels.</param>
                /// <param name="height">The photo's height in pixels.</param>
                /// <returns>The description.</returns>
                public static string Points(int maxPoints, int width, int height) =>
                    $"For walls, floors and ceilings: 2 or {maxPoints} spots on the visible surface itself, in pixels of the {width} x {height} photo. Empty for every other object.";
            }
        }

        /// <summary>SAM 3 on fal.ai.</summary>
        public static class Fal
        {
            /// <summary>The fewest objects worth one shared request; a single object gets its own request.</summary>
            public const int MinShared = 2;

            /// <summary>The point label meaning "this spot is part of the object".</summary>
            public const int ForegroundPoint = 1;

            /// <summary>How the photo is sent: a JPEG data URI.</summary>
            public const string JpegDataUri = "data:image/jpeg;base64,";

            /// <summary>Masks returned inline start with this.</summary>
            public const string DataUriScheme = "data:";

            /// <summary>The mask image format asked for: PNG keeps the edges exact.</summary>
            public const string OutputFormat = "png";

            /// <summary>The request and response field names.</summary>
            public static class Json
            {
                /// <summary>The image.</summary>
                public const string ImageUrl = "image_url";
                /// <summary>The text prompt.</summary>
                public const string Prompt = "prompt";
                /// <summary>Box prompts.</summary>
                public const string BoxPrompts = "box_prompts";
                /// <summary>Point prompts.</summary>
                public const string PointPrompts = "point_prompts";
                /// <summary>Whether to draw the mask on the photo.</summary>
                public const string ApplyMask = "apply_mask";
                /// <summary>Whether to return masks inline.</summary>
                public const string SyncMode = "sync_mode";
                /// <summary>The mask format.</summary>
                public const string OutputFormat = "output_format";
                /// <summary>Whether to return several masks.</summary>
                public const string ReturnMultipleMasks = "return_multiple_masks";
                /// <summary>How many masks to return.</summary>
                public const string MaxMasks = "max_masks";
                /// <summary>Across.</summary>
                public const string X = "x";
                /// <summary>Down.</summary>
                public const string Y = "y";
                /// <summary>A point's label.</summary>
                public const string Label = "label";
                /// <summary>Which object a box or point belongs to.</summary>
                public const string ObjectId = "object_id";
                /// <summary>A box's left edge.</summary>
                public const string XMin = "x_min";
                /// <summary>A box's top edge.</summary>
                public const string YMin = "y_min";
                /// <summary>A box's right edge.</summary>
                public const string XMax = "x_max";
                /// <summary>A box's bottom edge.</summary>
                public const string YMax = "y_max";
                /// <summary>The returned masks.</summary>
                public const string Masks = "masks";
                /// <summary>Per mask, which box it answers.</summary>
                public const string Metadata = "metadata";
                /// <summary>A mask's box position.</summary>
                public const string Index = "index";
                /// <summary>A mask image's URL.</summary>
                public const string Url = "url";
            }
        }

        /// <summary>How long SAM 3 requests may take: one photo can take well over the default 10 s per attempt.</summary>
        public static class Resilience
        {
            /// <summary>One attempt.</summary>
            public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(60);

            /// <summary>The circuit breaker's sampling window (at least twice the attempt timeout).</summary>
            public static readonly TimeSpan SamplingDuration = TimeSpan.FromSeconds(120);

            /// <summary>All attempts together.</summary>
            public static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(180);
        }
    }

    /// <summary>The fakes' recorded sample photos.</summary>
    public static class Samples
    {
        /// <summary>Fingerprints are the mean brightness of a grid of this many cells per side.</summary>
        public const int Grid = 16;

        /// <summary>
        /// The largest mean brightness difference per cell (0-255) still counted as the same photo.
        /// Re-encoding and scaling change a photo by 1-3; different rooms differ by far more.
        /// </summary>
        public const double MaxDifference = 10;

        /// <summary>The largest relative difference in aspect ratio still counted as the same photo.</summary>
        public const double MaxAspectDifference = 0.02;

        /// <summary>Where the samples are embedded in the assembly.</summary>
        public const string ResourceFolder = ".Ai.Fakes.Samples.";

        /// <summary>The samples' file extension.</summary>
        public const string Extension = ".json";

        /// <summary>A recorded SAM 3 analysis of a sample.</summary>
        public const string RecordedSuffix = ".sam3.json";

        /// <summary>The SAM 3 request count of a recording.</summary>
        public const string RequestsSuffix = ".sam3-usage.json";

        /// <summary>The Claude token usage of a recording.</summary>
        public const string ClaudeUsageSuffix = ".claude-usage.json";
    }

    /// <summary>Pixel work: masks, label maps and encoding.</summary>
    public static class Imaging
    {
        /// <summary>A mask pixel at or above this coverage (0-255) belongs to the object.</summary>
        public const byte CoverageThreshold = 128;

        /// <summary>Full coverage of a mask pixel.</summary>
        public const byte FullCoverage = 255;

        /// <summary>The number of values a color channel or label byte can hold.</summary>
        public const int ByteValues = 256;

        /// <summary>PNG encoding quality: lossless.</summary>
        public const int PngQuality = 100;
    }

    /// <summary>Tuning of the label map cleanup after SAM 3.</summary>
    public static class Refiner
    {
        /// <summary>A piece of an object smaller than this fraction of the photo, apart from its largest piece, is a speckle.</summary>
        public const double FragmentFraction = 0.0003;

        /// <summary>A speckle is never smaller than this many pixels, so tiny photos keep their small pieces.</summary>
        public const int MinFragmentPixels = 20;

        /// <summary>An enclosed gap is filled only up to this fraction of the photo (larger ones may be an unlisted object).</summary>
        public const double MaxHoleFraction = 0.02;

        /// <summary>
        /// Growing crosses from a pixel to its neighbor only when their colors differ by at most this
        /// much (RGB distance, 0-441). JPEG noise and soft shading stay below it; object edges don't.
        /// </summary>
        public const double MaxStepDistance = 24;

        /// <summary>How far (in pixels) an object that isn't a surface may grow past its mask.</summary>
        public const int MaxObjectGrowth = 6;

        /// <summary>How far a surface may grow past its mask, as a fraction of the photo's long side.</summary>
        public const double MaxSurfaceGrowthFraction = 0.05;

        /// <summary>
        /// A gap still left after growing goes to the neighbor whose mean color is closest, when the two
        /// means differ by at most this much (RGB distance, 0-441); a gap unlike every neighbor stays empty.
        /// </summary>
        public const double MaxGapColorDistance = 60;
    }

    /// <summary>The database mapping.</summary>
    public static class Persistence
    {
        /// <summary>The longest owner id (an Identity user id or a guest id).</summary>
        public const int OwnerIdLength = 128;

        /// <summary>The longest storage key.</summary>
        public const int StorageKeyLength = 300;

        /// <summary>The longest failure reason.</summary>
        public const int FailureReasonLength = 500;

        /// <summary>The longest status or scope name stored as text.</summary>
        public const int EnumNameLength = 20;

        /// <summary>The longest AI mode name stored as text.</summary>
        public const int AiModeLength = 10;

        /// <summary>The column type for objects and edits kept with their row.</summary>
        public const string Json = "jsonb";

        /// <summary>The shadow property mapped to Postgres's xmin row version.</summary>
        public const string Version = "Version";

        /// <summary>How often a save is tried when other writers keep changing the same rooms.</summary>
        public const int MaxSaveAttempts = 3;

        /// <summary>The id of the only AI settings row.</summary>
        public const int AiSettingsRowId = 1;

        /// <summary>The check that keeps the AI settings to that one row.</summary>
        public const string AiSettingsSingleRow = "ck_ai_settings_single_row";

        /// <summary>The longest AI model name.</summary>
        public const int ModelLength = 100;

        /// <summary>The longest stored URL.</summary>
        public const int UrlLength = 300;

        /// <summary>The stored type of a recolor edit.</summary>
        public const string RecolorEdit = "recolor";

        /// <summary>Table names, snake_case like the rest (Identity would name them "AspNetUsers").</summary>
        public static class Tables
        {
            /// <summary>Accounts.</summary>
            public const string Users = "users";
            /// <summary>Roles.</summary>
            public const string Roles = "roles";
            /// <summary>Users' claims.</summary>
            public const string UserClaims = "user_claims";
            /// <summary>External logins.</summary>
            public const string UserLogins = "user_logins";
            /// <summary>Users' roles.</summary>
            public const string UserRoles = "user_roles";
            /// <summary>Users' tokens.</summary>
            public const string UserTokens = "user_tokens";
            /// <summary>Roles' claims.</summary>
            public const string RoleClaims = "role_claims";
            /// <summary>A design's rooms.</summary>
            public const string DesignRooms = "design_rooms";

            /// <summary>The one-row AI settings table.</summary>
            public const string AiSettings = "ai_settings";
        }
    }

    /// <summary>File storage defaults.</summary>
    public static class Storage
    {
        /// <summary>The S3 region; RustFS accepts any.</summary>
        public const string DefaultRegion = "us-east-1";

        /// <summary>The bucket.</summary>
        public const string DefaultBucket = "designapp";
    }

    /// <summary>Background jobs.</summary>
    public static class Workers
    {
        /// <summary>How often the unsaved-upload cleanup runs.</summary>
        public static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

        /// <summary>How long after startup the first cleanup waits, so the API is up first.</summary>
        public static readonly TimeSpan CleanupFirstRunDelay = TimeSpan.FromMinutes(1);
    }

    /// <summary>The messages the adapters show to users.</summary>
    public static class Messages
    {
        /// <summary>Claude says the photo isn't of a room.</summary>
        public const string NotARoom = "This doesn't look like a photo of a room. Upload a photo of a room's interior.";

        /// <summary>A file that isn't a readable photo.</summary>
        public const string UnsupportedPhoto = "This file isn't a photo we can read. Use a JPEG, PNG or WebP image.";

        /// <summary>An empty upload.</summary>
        public const string EmptyFile = "The file is empty.";

        /// <summary>A photo with too many pixels.</summary>
        public static readonly string TooManyPixels =
            $"This photo has too many pixels. Use one under {Application.Constants.Photos.MaxSourceMegapixels} megapixels.";

        /// <summary>A photo that is too small.</summary>
        public static readonly string PhotoTooSmall =
            $"This photo is too small. Use one at least {Application.Constants.Photos.MinShortEdge} pixels on each side.";

        /// <summary>A photo that can't be decoded.</summary>
        public const string DamagedPhoto = "This photo is damaged and can't be read.";

        /// <summary>Painting where the object already is.</summary>
        /// <param name="label">The object.</param>
        public static string AlreadyThere(string label) => $"That part is already the {label}.";

        /// <summary>Erasing where the object isn't.</summary>
        /// <param name="label">The object.</param>
        public static string NothingToErase(string label) => $"There's no {label} there to erase.";

        /// <summary>Erasing the whole object.</summary>
        /// <param name="label">The object.</param>
        public static string WouldEraseAll(string label) => $"That would erase the whole {label}. Use Reset to remove its color instead.";

        /// <summary>A stored AI setting that can't be used (logged when an analysis fails).</summary>
        /// <param name="field">The setting.</param>
        /// <param name="rule">The rule it breaks.</param>
        /// <returns>The message.</returns>
        public static string InvalidAiSetting(string field, string rule) => $"The ai_settings value {field} {rule}.";
    }

    /// <summary>Claude's effort levels the object listing can use.</summary>
    public enum InventoryEffort
    {
        /// <summary>Least reasoning: cheapest and fastest.</summary>
        Low,

        /// <summary>The default.</summary>
        Medium,

        /// <summary>More reasoning, for harder photos.</summary>
        High,
    }
}

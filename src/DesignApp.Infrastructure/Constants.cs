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
        public const string AiKeysRequired =
            "Ai:FalKey and either Ai:AnthropicApiKey or (in Development) Ai:UseClaudeSubscription are required unless Ai:UseFakes is true.";

        /// <summary>Shown at startup when the Claude subscription is switched on outside Development.</summary>
        public const string ClaudeSubscriptionDevelopmentOnly =
            "Ai:UseClaudeSubscription is for local development only; production uses Ai:AnthropicApiKey.";

        /// <summary>Shown when a real-AI room is analyzed but the keys aren't set.</summary>
        public const string RealAiNotSetUp =
            "The real AI isn't set up: Ai:FalKey and either Ai:AnthropicApiKey or (in Development) Ai:UseClaudeSubscription are needed.";
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

            /// <summary>
            /// The object eraser on fal.ai's queue: Bria's eraser, $0.04 a removal. Chosen over fal's own eraser
            /// (<c>fal-ai/object-removal/mask</c>), which left a dark smear over patterns such as a radiator's rails,
            /// and Ideogram's (<c>fal-ai/ideogram/object-removal</c>, $0.03, slower). The queue answers at once with a
            /// request to follow, so a slow job takes longer instead of failing.
            /// </summary>
            public const string RemovalEndpoint = "https://queue.fal.run/fal-ai/bria/eraser";

            /// <summary>
            /// The quality tier of fal's own eraser (Best $0.024 a removal, Low $0.006); Bria's and Ideogram's ignore it.
            /// </summary>
            public const RemovalQuality RemovalQuality = Constants.RemovalQuality.Best;

            /// <summary>
            /// How many pixels fal's own eraser grows the mask by, to take the object's edge and shadow too; Bria's and
            /// Ideogram's ignore it.
            /// </summary>
            public const int RemovalMaskExpansion = 15;

            /// <summary>
            /// How many pixels we grow the mask of an object marked by hand before any eraser gets it; 0, as tried on
            /// a small bottle on a sink: brushed as it is, Bria left a faint outline; grown 12 px, it painted a new jar
            /// in the larger hole. Kept as a setting to tune.
            /// </summary>
            public const int RemovalHandMaskGrowth = 0;

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

            /// <summary>An eraser quality tier.</summary>
            public const string RemovalQuality = "must be Low, Medium, High or Best";

            /// <summary>The eraser's mask growth.</summary>
            public static readonly string MaskExpansion = $"must be between 0 and {Removal.MaxMaskExpansion}";

            /// <summary>The listed-objects limit.</summary>
            public static readonly string ListedObjects = $"must be between 1 and {Application.Constants.Analysis.MaxObjects}";
        }

        /// <summary>The header that names the Anthropic workspace to bill (organization-level keys).</summary>
        public const string WorkspaceHeader = "anthropic-workspace-id";

        /// <summary>The authorization scheme fal.ai expects.</summary>
        public const string FalAuthScheme = "Key";

        /// <summary>
        /// Development only: Claude reached through Claude Code on the developer's PC (<c>claude -p</c>), signed in
        /// with their Claude subscription, so local real-AI runs don't bill the API key.
        /// </summary>
        public static class ClaudeCode
        {
            /// <summary>The Claude Code command when <c>Ai:ClaudeCodePath</c> isn't set (it must be on PATH).</summary>
            public const string DefaultPath = "claude";

            /// <summary>
            /// The empty folder Claude Code runs in (under the temp folder), so no project's CLAUDE.md,
            /// settings, hooks or MCP servers load into the call.
            /// </summary>
            public const string WorkingFolder = "designapp-claude-code";

            /// <summary>Removed from Claude Code's environment: when set, it would bill the API instead of the subscription.</summary>
            public const string ApiKeyVariable = "ANTHROPIC_API_KEY";

            /// <summary>The longest one call may take before it is stopped.</summary>
            public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

            /// <summary>
            /// Turns allowed: the answer plus the structured-output step. With no tools there is nothing
            /// else for Claude to do.
            /// </summary>
            public const int MaxTurns = 3;

            /// <summary>Settings for the call: no hooks from the user's own Claude Code setup.</summary>
            public const string HookFreeSettings = """{"disableAllHooks":true}""";

            /// <summary>The <c>claude</c> command-line flags and values the call uses.</summary>
            public static class Flags
            {
                /// <summary>Headless: answer and exit.</summary>
                public const string Print = "-p";
                /// <summary>How the message is sent.</summary>
                public const string InputFormat = "--input-format";
                /// <summary>How the result is printed.</summary>
                public const string OutputFormat = "--output-format";
                /// <summary>One JSON message per line.</summary>
                public const string StreamJson = "stream-json";
                /// <summary>Print every message (stream-json output requires it); the last line is the result.</summary>
                public const string Verbose = "--verbose";
                /// <summary>The model.</summary>
                public const string Model = "--model";
                /// <summary>The effort level.</summary>
                public const string Effort = "--effort";
                /// <summary>A file that replaces Claude Code's whole system prompt.</summary>
                public const string SystemPromptFile = "--system-prompt-file";
                /// <summary>The JSON schema the answer must match.</summary>
                public const string JsonSchema = "--json-schema";
                /// <summary>Which tools Claude may use.</summary>
                public const string Tools = "--tools";
                /// <summary>No tools at all.</summary>
                public const string NoTools = "";
                /// <summary>The most turns.</summary>
                public const string MaxTurns = "--max-turns";
                /// <summary>Don't save the session.</summary>
                public const string NoSessionPersistence = "--no-session-persistence";
                /// <summary>Only MCP servers given on the command line (none).</summary>
                public const string StrictMcpConfig = "--strict-mcp-config";
                /// <summary>Extra settings.</summary>
                public const string Settings = "--settings";
            }

            /// <summary>The stream-json user message's field names and values.</summary>
            public static class Wire
            {
                /// <summary>A message's kind.</summary>
                public const string Type = "type";
                /// <summary>A user message.</summary>
                public const string User = "user";
                /// <summary>The message itself.</summary>
                public const string Message = "message";
                /// <summary>Who wrote it.</summary>
                public const string Role = "role";
                /// <summary>Its blocks.</summary>
                public const string Content = "content";
                /// <summary>An image block.</summary>
                public const string Image = "image";
                /// <summary>An image's source.</summary>
                public const string Source = "source";
                /// <summary>Inline image data.</summary>
                public const string Base64 = "base64";
                /// <summary>The image's MIME type.</summary>
                public const string MediaType = "media_type";
                /// <summary>The image bytes.</summary>
                public const string Data = "data";
                /// <summary>A text block, and its text.</summary>
                public const string Text = "text";
            }

            /// <summary>The fields of Claude Code's result line (the last stream-json line, of type <c>result</c>).</summary>
            public static class Fields
            {
                /// <summary>A line's kind.</summary>
                public const string Type = "type";
                /// <summary>The kind of the result line.</summary>
                public const string ResultType = "result";
                /// <summary>Whether the run failed.</summary>
                public const string IsError = "is_error";
                /// <summary>The final text, or the failure.</summary>
                public const string Result = "result";
                /// <summary>The answer that matched the JSON schema.</summary>
                public const string StructuredOutput = "structured_output";
                /// <summary>Tokens per model.</summary>
                public const string ModelUsage = "modelUsage";
                /// <summary>A model's input tokens.</summary>
                public const string InputTokens = "inputTokens";
                /// <summary>A model's output tokens.</summary>
                public const string OutputTokens = "outputTokens";
                /// <summary>A model's tokens read from the prompt cache.</summary>
                public const string CacheReadTokens = "cacheReadInputTokens";
                /// <summary>A model's tokens written to the prompt cache.</summary>
                public const string CacheWriteTokens = "cacheCreationInputTokens";
                /// <summary>Claude Code's own estimate of the run's API price.</summary>
                public const string CostUsd = "total_cost_usd";
            }

            /// <summary>Messages for a failed call (developer-facing; the user sees the analysis failure).</summary>
            public static class Messages
            {
                /// <summary>Claude Code couldn't be started.</summary>
                /// <param name="path">The command tried.</param>
                /// <param name="reason">Why it failed.</param>
                /// <returns>The text.</returns>
                public static string NotStarted(string path, string reason)
                {
                    return $"Claude Code ('{path}') couldn't be started: {reason}. Install it and sign in with your Claude subscription (run 'claude', then /login).";
                }

                /// <summary>Claude Code ran but gave no usable answer.</summary>
                /// <param name="reason">What it said.</param>
                /// <returns>The text.</returns>
                public static string Failed(string reason)
                {
                    return $"Claude Code didn't list the objects: {reason}";
                }

                /// <summary>Claude Code took longer than <see cref="Timeout"/>.</summary>
                public const string TimedOut = "Claude Code took too long to list the objects.";
            }
        }

        /// <summary>Claude's object listing (the API and the Claude Code route share it).</summary>
        public static class Claude
        {
            /// <summary>The smallest box side kept, as a fraction of the image.</summary>
            public const double MinBoxSide = 0.001;

            /// <summary>Sent with the photo: Claude gives boxes most accurately in the image's own pixels.</summary>
            /// <param name="width">The photo's width in pixels.</param>
            /// <param name="height">The photo's height in pixels.</param>
            /// <returns>The text.</returns>
            public static string ImageSize(int width, int height)
            {
                return $"The photo is {width} x {height} pixels. Give every box and point in these pixels, measured from the top-left corner.";
            }

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
                public static string Box(int width, int height)
                {
                    return $"Bounding box in pixels of this {width} x {height} photo: the left and top edges, then the width and height.";
                }
                /// <summary>Of <see cref="Fields.Color"/>.</summary>
                public const string Color = "The object's main color as #RRGGBB.";
                /// <summary>The points field, for walls, floors and ceilings.</summary>
                /// <param name="maxPoints">The most points per surface.</param>
                /// <param name="width">The photo's width in pixels.</param>
                /// <param name="height">The photo's height in pixels.</param>
                /// <returns>The description.</returns>
                public static string Points(int maxPoints, int width, int height)
                {
                    return $"For walls, floors and ceilings: 2 or {maxPoints} spots on the visible surface itself, in pixels of the {width} x {height} photo. Empty for every other object.";
                }
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

        /// <summary>Removing an object from a photo with fal.ai's eraser.</summary>
        public static class Removal
        {
            /// <summary>The most the eraser grows a mask by, in pixels.</summary>
            public const int MaxMaskExpansion = 50;

            /// <summary>How the mask is sent: a PNG data URI.</summary>
            public const string PngDataUri = "data:image/png;base64,";

            /// <summary>How often the queued job's status is checked.</summary>
            public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

            /// <summary>How long a removal may take in all before the user is told it failed.</summary>
            public static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(3);

            /// <summary>How long one call to the queue (submit, status or result) may take.</summary>
            public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

            /// <summary>The name of the eraser client's resilience pipeline (a timeout, no retries).</summary>
            public const string ResilienceName = "fal-eraser";

            /// <summary>Failed status checks in a row before giving up (a status check is free to repeat).</summary>
            public const int MaxStatusErrors = 5;

            /// <summary>The mask type of an object the AI outlined (SAM 3).</summary>
            public const string AutomaticMask = "automatic";

            /// <summary>The mask type of an object the user marked with a brush.</summary>
            public const string ManualMask = "manual";

            /// <summary>The queue status of a finished job.</summary>
            public const string Completed = "COMPLETED";

            /// <summary>The wire name of each quality tier.</summary>
            /// <param name="quality">The tier.</param>
            public static string WireQuality(RemovalQuality quality)
            {
                return quality switch
                {
                    RemovalQuality.Low => "low_quality",
                    RemovalQuality.Medium => "medium_quality",
                    RemovalQuality.High => "high_quality",
                    _ => "best_quality",
                };
            }

            /// <summary>The request and response field names.</summary>
            public static class Json
            {
                /// <summary>The photo.</summary>
                public const string ImageUrl = "image_url";
                /// <summary>The mask (white = remove).</summary>
                public const string MaskUrl = "mask_url";
                /// <summary>The quality tier.</summary>
                public const string Model = "model";
                /// <summary>How far to grow the mask.</summary>
                public const string MaskExpansion = "mask_expansion";
                /// <summary>Where to check a queued job's status.</summary>
                public const string StatusUrl = "status_url";
                /// <summary>Where to fetch a queued job's result.</summary>
                public const string ResponseUrl = "response_url";
                /// <summary>A queued job's status.</summary>
                public const string Status = "status";
                /// <summary>Why a queued job failed.</summary>
                public const string Error = "error";
                /// <summary>What made the mask.</summary>
                public const string MaskType = "mask_type";
                /// <summary>The returned images.</summary>
                public const string Images = "images";
                /// <summary>The returned image (erasers that answer with one).</summary>
                public const string Image = "image";
                /// <summary>An image's URL.</summary>
                public const string Url = "url";
            }

            /// <summary>What the user is told.</summary>
            public static class Messages
            {
                /// <summary>The eraser failed or returned nothing.</summary>
                public const string Failed = "We couldn't remove that object. Try again in a moment.";
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

    /// <summary>The built-in library's files, embedded in this assembly under <c>Library/&lt;design&gt;/</c>.</summary>
    public static class Library
    {
        /// <summary>Starts the resource name of every library file (set as LogicalName in the project file).</summary>
        public const string ResourcePrefix = "DesignApp.Library/";

        /// <summary>A design's details and room list, in its folder.</summary>
        public const string DesignFile = "design.json";

        /// <summary>A layout's photo: <c>&lt;room&gt;/&lt;layout&gt;.jpg</c>.</summary>
        public const string PhotoSuffix = ".jpg";

        /// <summary>A layout's label map: <c>&lt;room&gt;/&lt;layout&gt;.labels.png</c>.</summary>
        public const string LabelMapSuffix = ".labels.png";

        /// <summary>A layout's size and objects: <c>&lt;room&gt;/&lt;layout&gt;.objects.json</c>.</summary>
        public const string ObjectsSuffix = ".objects.json";

        /// <summary>The width of a layout's thumbnail, in pixels: sharp in a tile on a high-density screen.</summary>
        public const int ThumbnailWidth = 640;

        /// <summary>The JPEG quality of a thumbnail or a cover (0-100).</summary>
        public const int ThumbnailQuality = 80;

        /// <summary>A design's cover shows this many rooms per row.</summary>
        public const int CoverColumns = 3;

        /// <summary>A design's cover shows this many rows of rooms.</summary>
        public const int CoverRows = 2;

        /// <summary>The width of one room in a cover, in pixels.</summary>
        public const int CoverCellWidth = 400;

        /// <summary>The height of one room in a cover, in pixels (the photos' 4:3).</summary>
        public const int CoverCellHeight = 300;

        /// <summary>The gap between the rooms of a cover, in pixels.</summary>
        public const int CoverGap = 4;
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

        /// <summary>
        /// In the last pass, an object that isn't a surface may only take pixels inside Claude's box for it,
        /// widened by this fraction of the photo's long side on every side: so a white teapot can't spread
        /// over a white tablecloth.
        /// </summary>
        public const double BoxReachFraction = 0.02;

        /// <summary>
        /// A large object's reach is widened by at least this fraction of its own box's width (sideways) and
        /// height (up and down), so a big counter whose box is a little tight still closes the gaps along its edges.
        /// </summary>
        public const double BoxReachOwnFraction = 0.25;
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

        /// <summary>The longest library layout name, such as <c>minimalist/kitchen/2</c>.</summary>
        public const int LibraryRoomLength = 100;

        /// <summary>The column type for objects and edits kept with their row.</summary>
        public const string Json = "jsonb";

        /// <summary>An empty jsonb array, the default for rooms stored before photo versions.</summary>
        public const string EmptyJsonArray = "'[]'::jsonb";

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

        /// <summary>A hand-marked object whose strokes leave nothing marked.</summary>
        public const string EmptyMark = "Mark at least part of the object first.";

        /// <summary>Painting where the object already is.</summary>
        /// <param name="label">The object.</param>
        public static string AlreadyThere(string label)
        {
            return $"That part is already the {label}.";
        }

        /// <summary>Erasing where the object isn't.</summary>
        /// <param name="label">The object.</param>
        public static string NothingToErase(string label)
        {
            return $"There's no {label} there to erase.";
        }

        /// <summary>Erasing the whole object.</summary>
        /// <param name="label">The object.</param>
        public static string WouldEraseAll(string label)
        {
            return $"That would erase the whole {label}. Use Reset to remove its color instead.";
        }

        /// <summary>A stored AI setting that can't be used (logged when an analysis fails).</summary>
        /// <param name="field">The setting.</param>
        /// <param name="rule">The rule it breaks.</param>
        /// <returns>The message.</returns>
        public static string InvalidAiSetting(string field, string rule)
        {
            return $"The ai_settings value {field} {rule}.";
        }
    }

    /// <summary>The quality tiers of fal.ai's object eraser, cheapest first.</summary>
    public enum RemovalQuality
    {
        /// <summary>$0.006 a removal.</summary>
        Low,

        /// <summary>$0.012 a removal.</summary>
        Medium,

        /// <summary>$0.018 a removal.</summary>
        High,

        /// <summary>$0.024 a removal; the default.</summary>
        Best,
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

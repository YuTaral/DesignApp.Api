namespace DesignApp.Api;

/// <summary>
/// The API layer's fixed values: routes, cookies, headers, limits and the texts it sends. Values of
/// the inner layers live in their own <c>Constants</c> classes.
/// </summary>
public static class Constants
{
    /// <summary>Endpoint paths and the URLs the responses link to.</summary>
    public static class Routes
    {
        /// <summary>The prefix of every endpoint.</summary>
        public const string Api = "/api";

        /// <summary>The status endpoint, under <see cref="Api"/>.</summary>
        public const string Status = "/status";

        /// <summary>The account group, under <see cref="Api"/>.</summary>
        public const string Account = "/account";

        /// <summary>The designs group, under <see cref="Api"/>.</summary>
        public const string Designs = "/designs";

        /// <summary>The rooms group, under <see cref="Api"/>.</summary>
        public const string Rooms = "/rooms";

        /// <summary>The AI availability endpoint, under <see cref="Api"/>.</summary>
        public const string Ai = "/ai";

        /// <summary>The root of a group.</summary>
        public const string Root = "/";

        /// <summary>Sign up, under <see cref="Account"/>.</summary>
        public const string SignUp = "/sign-up";

        /// <summary>Sign in, under <see cref="Account"/>.</summary>
        public const string SignIn = "/sign-in";

        /// <summary>Sign out, under <see cref="Account"/>.</summary>
        public const string SignOut = "/sign-out";

        /// <summary>One item of a group, by its id.</summary>
        public const string ById = "/{id:guid}";

        /// <summary>The rooms of one design.</summary>
        public const string DesignRooms = "/{id:guid}/rooms";

        /// <summary>One room of one design.</summary>
        public const string DesignRoom = "/{id:guid}/rooms/{roomId:guid}";

        /// <summary>A room's photo.</summary>
        public const string RoomPhoto = "/{id:guid}/photo";

        /// <summary>A room's current label map.</summary>
        public const string RoomLabels = "/{id:guid}/labels";

        /// <summary>One stored version of a room's label map.</summary>
        public const string RoomLabelRevision = "/{id:guid}/labels/{revision:int}";

        /// <summary>The brush-stroke outline fix of a room.</summary>
        public const string RoomOutline = "/{id:guid}/outline";

        /// <summary>The objects of a room, where a hand-marked object is added.</summary>
        public const string RoomObjects = "/{id:guid}/objects";

        /// <summary>The label map version a room uses.</summary>
        public const string RoomCurrentLabels = "/{id:guid}/labels/current";

        /// <summary>The URL of a design.</summary>
        /// <param name="id">The design's id.</param>
        /// <returns>The path of the design.</returns>
        public static string DesignUrl(Guid id)
        {
            return $"{Api}{Designs}/{id}";
        }

        /// <summary>The URL of a room.</summary>
        /// <param name="id">The room's id.</param>
        /// <returns>The path of the room.</returns>
        public static string RoomUrl(Guid id)
        {
            return $"{Api}{Rooms}/{id}";
        }

        /// <summary>The URL of a room's photo.</summary>
        /// <param name="id">The room's id.</param>
        /// <returns>The path of the photo.</returns>
        public static string RoomPhotoUrl(Guid id)
        {
            return $"{RoomUrl(id)}/photo";
        }

        /// <summary>The URL of one version of a room's label map.</summary>
        /// <param name="id">The room's id.</param>
        /// <param name="revision">The version.</param>
        /// <returns>The path of that label map.</returns>
        public static string RoomLabelMapUrl(Guid id, int revision)
        {
            return $"{RoomUrl(id)}/labels/{revision}";
        }
    }

    /// <summary>The SignalR hub that reports room changes.</summary>
    public static class Hub
    {
        /// <summary>The hub's path.</summary>
        public const string Path = "/api/hubs/rooms";

        /// <summary>The group of the connections watching one room.</summary>
        /// <param name="roomId">The room.</param>
        /// <returns>The group name.</returns>
        public static string Group(Guid roomId)
        {
            return $"rooms:{roomId}";
        }
    }

    /// <summary>The cookies the API sets.</summary>
    public static class Cookies
    {
        /// <summary>The sign-in cookie.</summary>
        public const string Session = "designapp.session";

        /// <summary>How long the sign-in cookie lasts without use.</summary>
        public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

        /// <summary>The cookie that holds a guest's id.</summary>
        public const string Guest = "designapp.guest";

        /// <summary>How long the guest cookie lasts.</summary>
        public static readonly TimeSpan GuestLifetime = TimeSpan.FromDays(365);

        /// <summary>The request item that holds a guest id issued during this request.</summary>
        public const string GuestIssuedItem = "designapp.guest.issued";

        /// <summary>The path both cookies apply to.</summary>
        public const string Path = "/";
    }

    /// <summary>Account rules.</summary>
    public static class Accounts
    {
        /// <summary>The shortest password allowed.</summary>
        public const int MinPasswordLength = 8;

        /// <summary>Wrong passwords in a row that lock an account.</summary>
        public const int MaxFailedAttempts = 5;

        /// <summary>How long a locked account stays locked.</summary>
        public static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(5);

        /// <summary>The Identity error codes the API words itself.</summary>
        public static class ErrorCodes
        {
            /// <summary>The user name is taken.</summary>
            public const string DuplicateUserName = "DuplicateUserName";

            /// <summary>The email is taken.</summary>
            public const string DuplicateEmail = "DuplicateEmail";

            /// <summary>The email isn't valid.</summary>
            public const string InvalidEmail = "InvalidEmail";

            /// <summary>The user name isn't valid.</summary>
            public const string InvalidUserName = "InvalidUserName";

            /// <summary>The password is too short.</summary>
            public const string PasswordTooShort = "PasswordTooShort";
        }
    }

    /// <summary>Rate limits per IP address.</summary>
    public static class RateLimits
    {
        /// <summary>The policy for photo uploads.</summary>
        public const string UploadsPolicy = "uploads";

        /// <summary>The policy for sign-up and sign-in.</summary>
        public const string AccountsPolicy = "accounts";

        /// <summary>The setting for uploads per hour.</summary>
        public const string UploadsPerHourKey = "RateLimits:UploadsPerHour";

        /// <summary>Uploads per hour when the setting is missing.</summary>
        public const int DefaultUploadsPerHour = 30;

        /// <summary>The window of the uploads limit.</summary>
        public static readonly TimeSpan UploadsWindow = TimeSpan.FromHours(1);

        /// <summary>The setting for account attempts per 10 minutes.</summary>
        public const string AccountAttemptsKey = "RateLimits:AccountAttemptsPer10Minutes";

        /// <summary>Account attempts per window when the setting is missing.</summary>
        public const int DefaultAccountAttempts = 20;

        /// <summary>The window of the account limit.</summary>
        public static readonly TimeSpan AccountsWindow = TimeSpan.FromMinutes(10);

        /// <summary>The partition of requests without a known address.</summary>
        public const string UnknownAddress = "unknown";
    }

    /// <summary>HTTP details.</summary>
    public static class Http
    {
        /// <summary>The status of a request the client abandoned (nginx's convention).</summary>
        public const int ClientClosedRequest = 499;

        /// <summary>Room for the multipart framing around an upload's photo.</summary>
        public const long UploadFramingBytes = 1024 * 1024;

        /// <summary>The cache header of files that never change.</summary>
        public const string ImmutableCache = "private, max-age=31536000, immutable";

        /// <summary>The Origin header.</summary>
        public const string Origin = "Origin";

        /// <summary>The Location header.</summary>
        public const string Location = "Location";

        /// <summary>The CORS policy for the web app.</summary>
        public const string WebCorsPolicy = "web";

        /// <summary>The setting that lists the web app's origins.</summary>
        public const string WebOriginsKey = "Cors:WebOrigins";
    }

    /// <summary>OpenAPI and endpoint grouping.</summary>
    public static class OpenApi
    {
        /// <summary>The API's name.</summary>
        public const string Title = "DesignApp API";

        /// <summary>The account endpoints' tag.</summary>
        public const string AccountTag = "Account";

        /// <summary>The design endpoints' tag.</summary>
        public const string DesignsTag = "Designs";

        /// <summary>The room endpoints' tag.</summary>
        public const string RoomsTag = "Rooms";
    }

    /// <summary>Titles of problem responses.</summary>
    public static class Titles
    {
        /// <summary>A request the domain or the framework rejected.</summary>
        public const string InvalidRequest = "Invalid request";

        /// <summary>Sign-up failed.</summary>
        public const string SignUpFailed = "Couldn't sign up";

        /// <summary>Sign-in failed.</summary>
        public const string SignInFailed = "Couldn't sign in";

        /// <summary>An unknown AI was chosen.</summary>
        public const string InvalidAi = "Invalid AI";

        /// <summary>The chosen AI isn't allowed here.</summary>
        public const string AiNotAvailable = "AI not available";

        /// <summary>An unknown scope was chosen.</summary>
        public const string InvalidScope = "Invalid scope";

        /// <summary>The photo is over the size limit.</summary>
        public const string PhotoTooLarge = "Photo too large";

        /// <summary>The photo couldn't be used.</summary>
        public const string InvalidPhoto = "Invalid photo";

        /// <summary>An outline fix failed.</summary>
        public const string OutlineFixFailed = "Can't fix the outline";

        /// <summary>A hand-marked object couldn't be added.</summary>
        public const string NewObjectFailed = "Can't add the object";

        /// <summary>An unknown label map version was chosen.</summary>
        public const string UnknownOutlineVersion = "Unknown outline version";
    }

    /// <summary>Texts the API sends to people.</summary>
    public static class Messages
    {
        /// <summary>Sent when the API is started without the AppHost.</summary>
        public const string NeedsAppHost =
            "DesignApp.Api can't run on its own: it needs the database and storage that DesignApp.AppHost starts. " +
            "Run DesignApp.AppHost instead (in Visual Studio: right-click DesignApp.AppHost > Set as Startup Project, then F5).";

        /// <summary>The email or the password is wrong.</summary>
        public const string WrongCredentials = "Wrong email or password.";

        /// <summary>The account is locked.</summary>
        public const string LockedOut = "Too many wrong passwords. Try again in a few minutes.";

        /// <summary>No email was given.</summary>
        public const string EnterEmail = "Enter your email.";

        /// <summary>No password was given.</summary>
        public const string EnterPassword = "Enter your password.";

        /// <summary>The email is taken.</summary>
        public const string EmailTaken = "An account with this email already exists. Sign in instead.";

        /// <summary>The email isn't valid.</summary>
        public const string InvalidEmail = "Enter a valid email address.";

        /// <summary>The password is too short.</summary>
        public static readonly string PasswordTooShort = $"Use a password of at least {Accounts.MinPasswordLength} characters.";

        /// <summary>The real AI was chosen but has no keys.</summary>
        public const string RealAiNotSetUp = "The real AI isn't set up yet: add the Anthropic and fal.ai keys to the API's user secrets.";

        /// <summary>The hub was asked for a room the caller can't see.</summary>
        public const string RoomNotFound = "We couldn't find that room.";

        /// <summary>The photo is over the size limit.</summary>
        public static readonly string PhotoTooLarge = $"Photos can be up to {Application.Constants.Photos.MaxUploadMegabytes} MB.";

        /// <summary>An unknown AI was chosen.</summary>
        /// <param name="ai">What was sent.</param>
        /// <returns>The message.</returns>
        public static string UnknownAi(string ai)
        {
            return $"Unknown AI \"{ai}\". Use Fake or Real.";
        }

        /// <summary>The server uses one AI only.</summary>
        /// <param name="only">The AI it uses.</param>
        /// <returns>The message.</returns>
        public static string OnlyAi(string only)
        {
            return $"This server only uses the {only.ToLowerInvariant()} AI.";
        }

        /// <summary>An unknown scope was chosen.</summary>
        /// <param name="scope">What was sent.</param>
        /// <param name="known">The scopes that exist.</param>
        /// <returns>The message.</returns>
        public static string UnknownScope(string scope, IEnumerable<string> known)
        {
            return $"Unknown scope \"{scope}\". Use {string.Join(", ", known)}.";
        }
    }
}

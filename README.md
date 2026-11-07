# DesignApp.Api

Backend for **DesignApp**, an AI room styler. Users upload a room photo, an AI pipeline detects and outlines every object, and users recolor objects and save designs.

The React frontend lives in the separate `DesignApp.Web` repository.

## How the AI pipeline works

An upload returns at once; a background worker then analyzes the room and pushes each step to the browser over SignalR (`Uploaded → Analyzing → Segmenting → Ready`, or `Failed` with a reason the user can read).

1. **Claude Sonnet 5.5 (vision)** lists the objects in the photo as structured JSON (a schema the answer must match): label, category, main color and a rough bounding box. It also says when a photo isn't of a room.
2. **SAM 3 (Segment Anything 3) on fal.ai** outlines every object in one request: all of Claude's boxes at once, one mask per box. A box alone outlines a large surface in patches, so Claude also puts 2-3 points on each wall, floor and ceiling, and they go in the same request. A surface without points, or any object whose mask doesn't fit its box, gets a request of its own with its label as the text prompt.
3. **Post-processing (SkiaSharp)** stacks the outlines (smaller objects on top), drops ones left with almost no visible pixels, measures each object's real box and main color from the photo, and builds a **label map**: a grayscale PNG where each pixel's value is the index of the object it belongs to. The browser finds the object under the cursor with one pixel lookup.
4. **The browser recolors** in the OKLab color space, keeping the original light and shadow (milestone 4).

The room's status in Postgres is the job state: rooms whose analysis was cut off (the API stopped) are picked up again on the next start, at most 3 times per room.

Both AI providers sit behind interfaces, so they can be swapped (another SAM host, a self-hosted model, a different Claude model).

## Stack

- ASP.NET Core 10 Minimal APIs, OpenAPI with the Scalar docs UI
- Aspire for local orchestration, OpenTelemetry traces, health checks and resilience
- PostgreSQL via EF Core (snake_case schema, migrations in `Infrastructure/Database/Migrations`)
- S3-compatible storage for photos and masks through the AWS SDK: RustFS locally, any S3 service in the cloud
- SkiaSharp for photo processing: uploads are validated, turned upright, scaled to at most 4096 px, converted to sRGB JPEG and stripped of metadata (including GPS location)
- SignalR for live analysis progress
- ASP.NET Core Identity accounts (email and password) in a secure cookie, with a private guest id per browser before sign-in
- Rate-limited uploads and sign-in attempts, RFC 9457 problem details for every error
- xUnit unit tests, and integration tests against real Postgres and RustFS containers (Testcontainers); CI on GitHub Actions

## Solution layout

```
src/
  DesignApp.AppHost          Aspire orchestration: Postgres, the API and the web app
  DesignApp.ServiceDefaults  Telemetry, health checks, HTTP resilience
  DesignApp.Api              HTTP endpoints, auth, CORS, OpenAPI
  DesignApp.Application      Use cases and the interfaces the AI providers implement
  DesignApp.Domain           Rooms, detected objects, designs and edit operations
  DesignApp.Infrastructure   Database, storage and AI provider clients
tests/
  DesignApp.UnitTests        Domain, use case, imaging and AI-provider tests
  DesignApp.IntegrationTests The real API, worker and SignalR against Postgres and RustFS containers
```

Fixed values (limits, durations, setting keys, routes, cookie and header names, table names and
the messages people see) live in each project's `Constants` class at its root, in nested groups
such as `Constants.Photos` or `Constants.Messages`. Add a new value there first and reference it;
don't write it inline. Texts with values are small methods, such as `Constants.Messages.UnknownObject(index)`.
Exceptions: developer-only exception texts, logger templates and math constants stay where they are used.
Enums are part of it: `RoomStatus`, `ObjectCategory`, `AnalysisScope` and `AiMode` are nested in the Domain's
`Constants`, `OutlineBrushMode` in the Application's and `InventoryEffort` in the Infrastructure's; files that
use them add `using static DesignApp.<Layer>.Constants;`.

Each use case is its own sealed class in its own file, grouped in a folder per entity:
`Rooms/RoomUseCases/` (such as `UploadRoom.cs` and `AnalyzeRoom.cs`), `Designs/DesignUseCases/` and
`Accounts/AccountUseCases/`. A use case takes only the services it needs.

Every type has its own file named after it, small records and interfaces included (`StoredFile.cs` next to
`IFileStorage.cs`). The one exception is `Common/Result.cs`, which keeps `Result<T>` with its `Result` helper.

Every interface sits in an `Interfaces/` folder inside the folder it belongs to, in every project: the ports
shared by every use case in `Application/Abstractions/Interfaces/` (the repositories, `IFileStorage`,
`ICurrentUser`), a feature's own ports in `Application/Analysis/Interfaces/` and `Application/Photos/Interfaces/`,
and `Infrastructure/Ai/Interfaces/IAiSettingsStore.cs` and `Api/Rooms/Interfaces/IRoomClient.cs`.
Records and record structs sit in an `Objects/` folder inside theirs, in every project: for example
`Domain/Rooms/Objects/BoundingBox.cs`, `Application/Analysis/Objects/InventoryItem.cs` and
`Api/Designs/Objects/DesignResponse.cs`. Like the `UseCases` folders, the `Interfaces` and `Objects` folders
don't change the namespace.

## Running locally

Requirements: .NET 10 SDK and Docker Desktop (running).

Clone both repositories into the same parent folder, so the AppHost can find the frontend:

```
DesignApp/
  DesignApp.Api/
  DesignApp.Web/
```

```bash
dotnet run --project src/DesignApp.AppHost
```

The Aspire dashboard opens with links to the API, the API docs (`/scalar`), the web app and the storage console. Postgres and storage run as persistent containers, so data survives restarts. The database is migrated automatically on startup in Development.

### Endpoints

The full list, with request and response shapes, is in the Scalar docs at `/scalar` in Development.

| Method | Path | What it does |
|---|---|---|
| `GET` | `/api/account` | Whether the browser is signed in, and as whom |
| `POST` | `/api/account/sign-up`, `/sign-in`, `/sign-out` | Create an account or sign in (email and password; the browser's guest rooms move into the account), or sign out |
| `POST` | `/api/rooms` | Upload a photo (multipart field `photo`; JPEG, PNG or WebP up to 15 MB) and create a room |
| `GET` | `/api/rooms/{id}` | The room, its analysis status and detected objects |
| `GET` | `/api/rooms/{id}/photo` | The stored photo |
| `GET` | `/api/rooms/{id}/labels/{revision}` | A version of the label map, once the room is Ready |
| `POST` | `/api/rooms/{id}/outline` | Fix an object's outline with a brush stroke |
| `GET` `POST` | `/api/designs` | List the account's designs, or create one from a room and its colors |
| `GET` `PUT` `DELETE` | `/api/designs/{id}` | Open a design, change its name and description, or delete it with its rooms |
| `POST` | `/api/designs/{id}/rooms` | Add one of the account's rooms to a design |
| `PUT` `DELETE` | `/api/designs/{id}/rooms/{roomId}` | Save a room's colors, or remove the room (which deletes it) |
| SignalR | `/api/hubs/rooms` | Call `Watch(roomId)` to get the room now and a `RoomChanged` message on every change |

### Designs and rooms

A design has a name, an optional description (up to 300 characters) and 1 to 20 rooms, each with its own colors. A room belongs to at most one design. Rooms are only kept inside a design: removing a room from its design deletes it (photo, outlines and all), deleting a design deletes its rooms, and an upload that isn't added to a design within 24 hours is deleted by a background job that runs every hour. A design always keeps at least one room; to remove the last one, delete the design.

### Accounts and guests

Visitors can upload and edit without an account: each browser gets its own random guest id in an HTTP-only cookie (`designapp.guest`), so guests never see each other's rooms. Signing up or signing in moves that browser's rooms into the account, so nothing made before signing in is lost; signing in from several browsers brings all their rooms together. Saving designs needs an account.

Accounts are ASP.NET Core Identity users in Postgres, signed in with a 30-day sliding cookie (`designapp.session`). Both cookies are HTTP-only, Secure and SameSite=Lax, so other sites can't send them with a form post. Passwords need at least 8 characters; 5 wrong passwords in a row lock an account for 5 minutes, and each address gets 20 sign-up or sign-in attempts per 10 minutes (`RateLimits:AccountAttemptsPer10Minutes`). A wrong email and a wrong password get the same answer. There is no email confirmation or password reset yet.

### Database migrations

The EF Core tool is pinned in `dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/DesignApp.Infrastructure --startup-project src/DesignApp.Infrastructure --output-dir Database/Migrations
```

### AI providers and secrets

In Development the API uses **fake AI providers** by default (`Ai:UseFakes` is `true` in `appsettings.Development.json`). The five sample photos (bathroom, bedroom, two kitchens, living room) are recognized by a 16 x 16 brightness fingerprint and get their real objects: SAM 3's recorded masks (`Samples/*.sam3.json`), with hand-traced outlines (`Samples/*.json`, polygons, ellipses and thick lines) as the fallback. Any other photo gets a generic layout (walls, floor, a sofa, a rug, a lamp, a plant). Each step waits briefly so the progress UI is visible. The whole app runs without API keys and at no cost. The tests use the same fakes, with no delay.

To call the real Claude and SAM 3 models, store the keys with user secrets (they are never committed):

```bash
dotnet user-secrets --project src/DesignApp.Api set "Ai:AnthropicApiKey" "<your key>"
dotnet user-secrets --project src/DesignApp.Api set "Ai:FalKey" "<your key>"
```

With the keys set, the upload page shows a **Fake AI / Real AI** switch (`Ai:AllowAiSwitch`, on in Development only), so each photo can be analyzed either way without restarting. Fake is the default; the choice lasts for the browser tab. The editor shows which AI analyzed the room, and `GET /api/ai` says what the API can use. Setting `Ai:UseFakes` to `false` makes the real AI the default instead.

If your Anthropic key belongs to the organization rather than a workspace (it starts with `sk-ant-usr-`), also set `Ai:AnthropicWorkspaceId` to the workspace's id (`wrkspc_...`, shown in the Claude Console under Settings > Workspaces).

To refresh the sample data with real results, set `Ai:RecordSamplesTo` to a folder as well: real analyses of the five sample photos are saved there as `<photo>.sam3.json`. Copied into `src/DesignApp.Infrastructure/Ai/Fakes/Samples/`, a recording replaces the hand-traced outlines of that photo, so the fakes show real SAM 3 edges for free. The current samples were recorded this way.

Outside Development the fakes are off and there is no switch: every photo uses the real AI, and the API refuses to start without both keys. A photo costs about 3 cents: one Claude call and one SAM 3 call, plus a SAM 3 retry for any outline that doesn't fit its box (fewer with the "Skip doors & windows" or "Objects only" choices). The `Ai` section also sets `MaxConcurrentAnalyses` (2).

### Tuning the AI (the `ai_settings` table)

How the real AI is tuned lives in the database, in the one-row `ai_settings` table, so it can change without a rebuild or restart: each analysis reads the row when it starts. The migration seeds it with the built-in defaults (`Constants.Ai.Defaults` in the Infrastructure project), so a fresh database behaves exactly like the code.

| Column | Default | What it does |
|---|---|---|
| `inventory_model` | `claude-sonnet-5-5` | The Claude model that lists the objects. Haiku is cheaper but misplaces boxes and forgets objects. |
| `inventory_effort` | `Medium` | `Low`, `Medium` or `High`. `Low` measured only about 4% fewer tokens. |
| `max_tokens` | 16000 | The longest answer Claude may give. |
| `instructions` | the prompt | Claude's instructions. `{MaxPoints}` and `{MaxListedObjects}` are replaced by the two columns below. |
| `user_message` | "List the objects in this room photo." | The text sent with the photo. |
| `max_listed_objects` | 30 | The most objects Claude is asked for (1 to 40). |
| `max_points` | 3 | The most points Claude puts on a wall, floor or ceiling. |
| `segmentation_endpoint` | `https://fal.run/fal-ai/sam-3/image` | The SAM 3 endpoint (https only). |
| `segmentation_parallelism` | 4 | SAM 3 requests at the same time for one photo. |
| `max_masks` | 3 | Masks SAM 3 returns per object, to pick the best-fitting one. |
| `min_fit` | 0.5 | How well (0 to 1) a mask must fit Claude's box before asking again. |
| `min_share_inside_box` | 0.8 | How much of a mask (0 to 1) must lie inside the box to count as a piece. |

Until there is an admin app, change it in pgAdmin (the Aspire dashboard shows the database's connection details). For example:

```sql
update ai_settings set inventory_model = 'claude-opus-5-5', inventory_effort = 'High' where id = 1;
```

The API keys are not in this table; they stay in user secrets. An unusable value (an empty model, a zero limit, a fraction outside 0 to 1, an endpoint that isn't https) fails the analysis with the reason in the log, so a typo never sends a broken request. If the row is deleted, the built-in defaults are used (with a warning in the log) until a row with id 1 is inserted again. To go back to the defaults, set the values from the table above.

## Tests

```bash
dotnet test DesignApp.slnx
```

The integration tests start throwaway Postgres and RustFS containers, so Docker must be running.

The browser passes live in the web repository: with the app running, `npm run e2e` in `DesignApp.Web`
runs them in Firefox against the five sample photos.

## Tools

- `tools/docs`: builds the two Word documents next to the repositories (`npm install` once, then
  `npm run docs`). The Class Reference is generated from the code's XML documentation comments, so it
  always matches the commit it follows.
- `tools/hooks/build-check.sh`: a Claude Code Stop hook (set in `../.claude/settings.json`) that builds the
  solution when a turn ends with uncommitted C# changes and sends any errors back.

## Third-party licenses

All dependencies are MIT or Apache-2.0 licensed.

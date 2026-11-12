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

Domain, Application and Api are grouped by **type**, one folder per kind of class:

| Project | Folders |
|---|---|
| Domain | `Entities/` (Room, Design, DesignRoom), `Objects/` (value objects such as `BoundingBox`, `HexColor`), `Extensions/`, `Exceptions/` |
| Application | `Interfaces/` (every port: repositories, file storage, current user, AI, imaging), `UseCases/` (one sealed class per use case, in `Designs/` or `Rooms/` by the entity it changes, such as `UploadRoom` and `CreateDesign`), `Objects/` (the records passed between steps), `Helpers/` (`GuestIds`, and `RoomRemover`, which several use cases share) |
| Api | `Endpoints/`, `Requests/`, `Responses/`, `Hubs/`, `Interfaces/`, `Services/`, `Middleware/`, `Setup/`, `Helpers/` |

Infrastructure is grouped by **technology** instead (`Ai/`, `Background/`, `Database/`, `FileStorage/`, `Photos/`...),
because there the code that changes together is the code around one technology. A use case takes only the
services it needs. Each top-level folder is a namespace, such as `DesignApp.Application.UseCases`; subfolders such as
`UseCases/Designs` don't add to it.

Every type has its own file named after it, small records and interfaces included (`StoredFile.cs` next to
`IFileStorage.cs`). The one exception is `Objects/Result.cs`, which keeps `Result<T>` with its `Result` helper.

Inside Infrastructure's technology folders, interfaces sit in an `Interfaces/` subfolder (such as
`Ai/Interfaces/IAiSettingsStore.cs`) and records and static helpers in an `Objects/` subfolder (such as
`Database/Objects/EditsJson.cs`); those subfolders don't change the namespace.

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
| `GET` | `/api/library` | The built-in sample houses with their rooms |
| `GET` | `/api/library/{design}/{room}/{layout}/photo` | The photo of one layout of a library room |
| `POST` | `/api/library/{design}/{room}/{layout}/copy` | Copy a layout into a new room of the visitor, already analyzed (no AI runs) |
| `POST` | `/api/library/{design}/copy` | Copy a whole sample house (each room's first layout) into a new design of the account |
| `GET` | `/api/dev/library-export/{id}?name=` | Development only: pack one of your rooms as library files (a zip) |
| `GET` `POST` | `/api/designs` | List the account's designs, or create one from a room and its colors |
| `GET` `PUT` `DELETE` | `/api/designs/{id}` | Open a design, change its name and description, or delete it with its rooms |
| `POST` | `/api/designs/{id}/rooms` | Add one of the account's rooms to a design |
| `PUT` `DELETE` | `/api/designs/{id}/rooms/{roomId}` | Save a room's colors, or remove the room (which deletes it) |
| SignalR | `/api/hubs/rooms` | Call `Watch(roomId)` to get the room now and a `RoomChanged` message on every change |

### Designs and rooms

A design has a name, an optional description (up to 300 characters) and 1 to 20 rooms, each with its own colors. A room belongs to at most one design. Rooms are only kept inside a design: removing a room from its design deletes it (photo, outlines and all), deleting a design deletes its rooms, and an upload that isn't added to a design within 24 hours is deleted by a background job that runs every hour. A design always keeps at least one room; to remove the last one, delete the design.

### The built-in library

Visitors can start without a photo: the library holds ready-made **sample houses**, one style each. Every room
has one or more **layouts** (different photos of it, such as three kitchens), all analyzed ahead of time. The
first is the **Minimalist house**: seven rooms with three layouts each. The files ship inside the API as embedded
resources, in `src/DesignApp.Infrastructure/Library/<design>/`:

- `design.json`: the id (the folder's name), name, style, description, list order and the rooms (id, title and
  layout ids, the first being the default);
- per layout, in the room's folder: `<layout>.jpg` (the processed photo), `<layout>.labels.png` (its label map)
  and `<layout>.objects.json` (the photo's size and the objects).

Opening a layout **copies** it into an ordinary room of the visitor, Ready at once, so Claude and SAM 3 never run
for it; from there it is like any upload (editing, removing objects with the room's AI, saving to a design,
deleted after 24 hours if not saved). A signed-in account can copy a whole house (each room's first layout) into
a new design.

To add a style or a layout: make the photos (`AiLab generate`, below), upload each in Development with the real
AI, fix the outlines in the editor if needed, then open `/api/dev/library-export/{roomId}?name=<layout id>`
while signed in as the room's owner. Unzip the three files into the room's folder and list the layout in
`design.json`. Objects removed from the photo are left out. A new style is a new folder; no code changes. The
unit tests check that every listed layout has its files and that its label map and objects match.

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
| `instructions` | the prompt | Claude's instructions. `{MaxPoints}` and `{MaxListedObjects}` are replaced by the two columns below. Claude is also told the photo's size and gives boxes and points in its pixels, which it places far more accurately than fractions. |
| `user_message` | "List the objects in this room photo." | The text sent with the photo. |
| `max_listed_objects` | 30 | The most objects Claude is asked for (1 to 40). |
| `max_points` | 3 | The most points Claude puts on a wall, floor or ceiling. |
| `segmentation_endpoint` | `https://fal.run/fal-ai/sam-3/image` | The SAM 3 endpoint (https only). |
| `segmentation_parallelism` | 4 | SAM 3 requests at the same time for one photo. |
| `max_masks` | 3 | Masks SAM 3 returns per object, to pick the best-fitting one. |
| `min_fit` | 0.5 | How well (0 to 1) a mask must fit Claude's box before asking again. |
| `min_share_inside_box` | 0.8 | How much of a mask (0 to 1) must lie inside the box to count as a piece. |
| `min_fill` | 0.2 | How much of Claude's box (0 to 1) a mask must fill before its fit counts in full, so a mask that only frames or edges the object gets its own request. |

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
- `tools/AiLab`: runs the real AI pipeline on one photo outside the API. `record <photo> <folder>` (paid: one
  Claude call and the SAM 3 requests) saves every answer; `replay <folder>` (free) feeds them back to the same
  code, so mask picking and label-map cleanup can be tuned without paying. `measure.py` and `draw.py` score
  and draw a run. Run with `dotnet run --project tools/AiLab -- ...`; keys come from the API's user secrets.
  `eval.py <set folder>` scores a whole photo test set (`expected.json` plus one run per photo in `runs/`): the
  expected objects found, how well Claude's boxes and SAM 3's outlines agree, loose outlines and unassigned pixels,
  per photo and per scenario. The set of 20 hard photos lives in `../rooms-eval` (outside the repos).
- `tools/AiLab generate <prompts.json> <folder> [--rooms a,b] [--candidates n] [--reference <photo>] [--yes]`:
  makes library photos with an image model on fal (the prompts file sets the model, size, price per image and the
  house's shared style; `library/minimalist/prompts.json` made the Minimalist house). Without `--yes` it only prints
  what it would make and the cost; with it, it is **paid**. The first room is made from text; `--reference` makes
  the others from the chosen photo so the whole house matches.
- `tools/hooks/build-check.sh`: a Claude Code Stop hook (set in `../.claude/settings.json`) that builds the
  solution when a turn ends with uncommitted C# changes and sends any errors back.

## Third-party licenses

All dependencies are MIT or Apache-2.0 licensed.

# DesignApp.Api

Backend for **DesignApp**, an AI room styler. Users upload a room photo, an AI pipeline detects and outlines every object, and users recolor objects and save designs.

The React frontend lives in the separate `DesignApp.Web` repository.

## How the AI pipeline works

An upload returns at once; a background worker then analyzes the room and pushes each step to the browser over SignalR (`Uploaded → Analyzing → Segmenting → Ready`, or `Failed` with a reason the user can read).

1. **Claude Haiku 5.5 (vision)** lists the objects in the photo as structured JSON (a schema the answer must match): label, category, main color and a rough bounding box. It also says when a photo isn't of a room.
2. **SAM 3 (Segment Anything 3) on fal.ai** outlines each object: one request per object, with its label as the text prompt and Claude's box as a box prompt, so look-alikes are told apart.
3. **Post-processing (SkiaSharp)** stacks the outlines (smaller objects on top), drops ones left with almost no visible pixels, measures each object's real box and main color from the photo, and builds a **label map**: a grayscale PNG where each pixel's value is the index of the object it belongs to. The browser finds the object under the cursor with one pixel lookup.
4. **The browser recolors** in the OKLab color space, keeping the original light and shadow (milestone 4).

The room's status in Postgres is the job state: rooms whose analysis was cut off (the API stopped) are picked up again on the next start, at most 3 times per room.

Both AI providers sit behind interfaces, so they can be swapped (another SAM host, a self-hosted model, a different Claude model).

## Stack

- ASP.NET Core 10 Minimal APIs, OpenAPI with the Scalar docs UI
- Aspire for local orchestration, OpenTelemetry traces, health checks and resilience
- PostgreSQL via EF Core (snake_case schema, migrations in `Infrastructure/Persistence/Migrations`)
- S3-compatible storage for photos and masks through the AWS SDK: RustFS locally, any S3 service in the cloud
- SkiaSharp for photo processing: uploads are validated, turned upright, scaled to at most 4096 px, converted to sRGB JPEG and stripped of metadata (including GPS location)
- SignalR for live analysis progress
- Rate-limited uploads, RFC 9457 problem details for every error
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

### Endpoints so far

| Method | Path | What it does |
|---|---|---|
| `POST` | `/api/rooms` | Upload a photo (multipart field `photo`; JPEG, PNG or WebP up to 15 MB) and create a room |
| `GET` | `/api/rooms/{id}` | The room, its analysis status and detected objects |
| `GET` | `/api/rooms/{id}/photo` | The stored photo |
| `GET` | `/api/rooms/{id}/labels` | The label map, once the room is Ready |
| SignalR | `/api/hubs/rooms` | Call `Watch(roomId)` to get the room now and a `RoomChanged` message on every change |

### Database migrations

The EF Core tool is pinned in `dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/DesignApp.Infrastructure --startup-project src/DesignApp.Infrastructure --output-dir Persistence/Migrations
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

Outside Development the fakes are off and there is no switch: every photo uses the real AI, and the API refuses to start without both keys. A photo costs a few cents: one Claude call plus one SAM 3 call per object (fewer with the "Skip doors & windows" or "Objects only" choices). Other settings in the `Ai` section: `InventoryModel` (default `claude-haiku-5-5`), `SegmentationParallelism` (4) and `MaxConcurrentAnalyses` (2).

## Tests

```bash
dotnet test DesignApp.slnx
```

The integration tests start throwaway Postgres and RustFS containers, so Docker must be running.

## Third-party licenses

All dependencies are MIT or Apache-2.0 licensed.

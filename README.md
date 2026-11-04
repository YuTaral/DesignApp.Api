# DesignApp.Api

Backend for **DesignApp**, an AI room styler. Users upload a room photo, an AI pipeline detects and outlines every object, and users recolor objects and save designs.

The React frontend lives in the separate `DesignApp.Web` repository.

## How the AI pipeline works

1. **Claude Haiku 5.5 (vision)** lists the objects in the photo as structured JSON: label, category, dominant color and a rough bounding box.
2. **SAM 3 (Segment Anything 3)** turns each label into a pixel-exact mask.
3. **Post-processing (ImageSharp)** removes duplicate masks, sets the stacking order and builds a label map, so the browser finds the object under the cursor with one pixel lookup.
4. **The browser recolors** in the OKLab color space, keeping the original light and shadow.

Both AI providers sit behind interfaces, so they can be swapped (another SAM host, a self-hosted model, a different Claude model).

## Stack

- ASP.NET Core 10 Minimal APIs, OpenAPI with the Scalar docs UI
- Aspire for local orchestration, OpenTelemetry traces, health checks and resilience
- PostgreSQL via EF Core, S3-compatible storage for photos and masks
- SignalR for live analysis progress
- xUnit tests; CI on GitHub Actions

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
  DesignApp.UnitTests        Domain and application tests
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

The Aspire dashboard opens with links to the API, the API docs (`/scalar`) and the web app.

### AI providers and secrets

In Development the API uses **fake AI providers** by default (`Ai:UseFakes` is `true` in `appsettings.Development.json`). They return saved, realistic results, so the whole app runs without API keys and at no cost. The tests use the same fakes.

To call the real Claude and SAM 3 models, store the keys with user secrets (they are never committed) and switch the fakes off:

```bash
dotnet user-secrets --project src/DesignApp.Api set "Anthropic:ApiKey" "<your key>"
dotnet user-secrets --project src/DesignApp.Api set "Fal:ApiKey" "<your key>"
dotnet user-secrets --project src/DesignApp.Api set "Ai:UseFakes" "false"
```

Remove that last secret (`dotnet user-secrets --project src/DesignApp.Api remove "Ai:UseFakes"`) to go back to the fakes.

## Tests

```bash
dotnet test DesignApp.slnx
```

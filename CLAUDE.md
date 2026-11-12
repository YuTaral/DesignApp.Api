# DesignApp.Api

.NET 10 backend of DesignApp (AI room styler), Clean Architecture with Aspire. The React frontend is the
sibling repo `../DesignApp.Web`; the two are always changed and tested together. README.md has the full
picture; this file is the working agreement.

## Commands

```bash
dotnet build DesignApp.slnx                     # warnings are errors; every public member needs an XML doc
dotnet test DesignApp.slnx                      # 202 unit + 46 API tests; Docker must run (Testcontainers)
dotnet ef migrations add <Name> --project src/DesignApp.Infrastructure --startup-project src/DesignApp.Infrastructure --output-dir Database/Migrations
cd tools/docs && npm install && npm run docs    # regenerate both Word files in the parent DesignApp folder
dotnet run --project tools/AiLab -- record <photo> <folder>   # PAID (ask first): record one real AI run
dotnet run --project tools/AiLab -- replay <folder>           # free: replay it after changing mask picking/cleanup
```

While the user's Visual Studio F5 run is active its `bin` folders are locked: build and test with
`--artifacts-path <some temp folder>` instead of the default output.

## Rules of the house

- **Never commit or push without an explicit OK** for that commit. "Commit" means commit only; push only when asked.
- **After every commit, regenerate the Word files** (`npm run docs` above). If a file is open in Word the
  script says so: ask the user to close it.
- **Test like the user:** on their own Visual Studio F5 run (DesignApp.AppHost, https profile) at
  http://localhost:5173, never a separate CLI AppHost. After backend changes, restart that run yourself
  (allowed for testing: Restart, or Stop and https, in Visual Studio by computer use), then run the Firefox
  passes (`npm run e2e` in DesignApp.Web) and check Visual Studio didn't pause.
- **Secrets:** API keys live only in user secrets (`dotnet user-secrets --project src/DesignApp.Api`). Never
  print their values, never commit them; check presence by key name only.
- **Paid AI:** real Claude / SAM 3 runs cost money; get the user's OK first. Development uses the fakes.

## Code conventions

- **Layers:** Api -> Infrastructure -> Application -> Domain; dependencies only point inward.
- **Constants:** every fixed value (limits, durations, config keys, routes, cookie/header/table names,
  wire names, user-facing messages, and enums) lives in the project's root `Constants` class, in nested
  groups with a `Messages` group. Files using an enum add `using static DesignApp.<Layer>.Constants;`.
  Exception: the real AI's tuning lives in the one-row `ai_settings` table (seeded from `Constants.Ai.Defaults`).
- **Block bodies for methods:** a method, local function or constructor uses `{ return ...; }`, never `=>`. Name an
  awaited or nested step in a local variable (`var outcome = await ...; return ToResponse(outcome, ...);`) so no
  line mixes an `await` with a mapping. Properties and lambdas may keep `=>`.
- **One type per file**, named after it. Only `Objects/Result.cs` pairs `Result<T>` with its `Result` helper.
- **Folders: Domain, Application and Api are grouped by type** (one folder per kind of class, not per entity):
  - Domain: `Entities/`, `Objects/` (value objects and records), `Extensions/`, `Exceptions/`.
  - Application: `Interfaces/` (every port), `UseCases/` (one sealed class per use case, in `Designs/` or
    `Rooms/` by the entity it changes), `Objects/` (records, `StoredFile`, `Result`), `Helpers/` (shared pieces that aren't
    use cases, such as `RoomRemover`).
  - Api: `Endpoints/`, `Requests/`, `Responses/`, `Hubs/`, `Interfaces/`, `Services/`, `Middleware/`, `Setup/`, `Helpers/`.
  - `Constants`, `DependencyInjection` and `Program` stay at the project roots. A new class goes in the folder of its kind.
- **Infrastructure is grouped by technology:** `Ai/`, `Analysis/`, `Background/` (the hosted services), `Database/`
  (with `Configurations/`, `Objects/`, `Repository/`, `Migrations/`), `FileStorage/`, `Identity/` and `Photos/`; inside
  them, interfaces go in `Interfaces/` and records and static helpers in `Objects/`.
- **Namespaces:** a top-level folder is a namespace (`DesignApp.Application.UseCases`, `DesignApp.Infrastructure.Database`).
  Subfolders never change it: `UseCases/Designs` and `UseCases/Rooms` are both `DesignApp.Application.UseCases` (a
  `Design` or `Room` namespace would hide the entities), and so are Infrastructure's `Interfaces`, `Objects`,
  `Configurations` and `Repository`:
  after a Visual Studio move, put the namespace back and drop the usings it added. Moving an entity changes its type
  name in the EF Core migrations and snapshot: update those strings, then check `dotnet ef migrations has-pending-model-changes`.
- Apply layout rules to the whole solution, not only the folders named in a request; say what was left out.
- Scripted edits on Windows: keep each file's line endings and byte-order mark as they were.

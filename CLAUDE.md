# DesignApp.Api

.NET 10 backend of DesignApp (AI room styler), Clean Architecture with Aspire. The React frontend is the
sibling repo `../DesignApp.Web`; the two are always changed and tested together. README.md has the full
picture; this file is the working agreement.

## Commands

```bash
dotnet build DesignApp.slnx                     # warnings are errors; every public member needs an XML doc
dotnet test DesignApp.slnx                      # 153 unit + 42 API tests; Docker must run (Testcontainers)
dotnet ef migrations add <Name> --project src/DesignApp.Infrastructure --startup-project src/DesignApp.Infrastructure --output-dir Database/Migrations
cd tools/docs && npm install && npm run docs    # regenerate both Word files in the parent DesignApp folder
```

While the user's Visual Studio F5 run is active its `bin` folders are locked: build and test with
`--artifacts-path <some temp folder>` instead of the default output.

## Rules of the house

- **Never commit or push without an explicit OK** for that commit. "Commit" means commit only; push only when asked.
- **After every commit, regenerate the Word files** (`npm run docs` above). If a file is open in Word the
  script says so: ask the user to close it.
- **Test like the user:** on their own Visual Studio F5 run (DesignApp.AppHost, https profile) at
  http://localhost:5173, never a separate CLI AppHost. Ask them to restart after backend changes, then run
  the Firefox passes (`npm run e2e` in DesignApp.Web) and check Visual Studio didn't pause.
- **Secrets:** API keys live only in user secrets (`dotnet user-secrets --project src/DesignApp.Api`). Never
  print their values, never commit them; check presence by key name only.
- **Paid AI:** real Claude / SAM 3 runs cost money; get the user's OK first. Development uses the fakes.

## Code conventions

- **Layers:** Api -> Infrastructure -> Application -> Domain; dependencies only point inward.
- **Constants:** every fixed value (limits, durations, config keys, routes, cookie/header/table names,
  wire names, user-facing messages, and enums) lives in the project's root `Constants` class, in nested
  groups with a `Messages` group. Files using an enum add `using static DesignApp.<Layer>.Constants;`.
  Exception: the real AI's tuning lives in the one-row `ai_settings` table (seeded from `Constants.Ai.Defaults`).
- **One type per file**, named after it. Only `Common/Result.cs` pairs `Result<T>` with its `Result` helper.
- **Folders:** use cases in `<Entity>/<Entity>UseCases/` (one sealed class per use case); every interface
  in an `Interfaces/` subfolder of its folder; every record / record struct in an `Objects/` subfolder, as
  are the static helpers of `Infrastructure/Photos` and `Infrastructure/Database`. Infrastructure is split into `Ai/`, `Analysis/`,
  `Background/` (the hosted services), `Database/` (with `Configurations/`, `Objects/`, `Repository/`,
  `Migrations/`), `FileStorage/`, `Identity/` and `Photos/`.
- **Namespaces:** a feature folder is a namespace (`DesignApp.Infrastructure.Database`). The organizing
  subfolders (`UseCases`, `Interfaces`, `Objects`, `Configurations`, `Repository`) never change it: after a
  Visual Studio move, put the namespace back and drop the usings it added.
- Apply layout rules to the whole solution, not only the folders named in a request; say what was left out.
- Scripted edits on Windows: keep each file's line endings and byte-order mark as they were.

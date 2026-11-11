# MediaFeeder

Self-hosted video frontend: .NET 10 Blazor app (Blazor UI + REST + gRPC + EF/Postgres) with a
Python yt-dlp download service and Python playback bridges.

## Layout
- `MediaFeeder/` — the main project in `MediaFeeder.sln` (test project: `MediaFeeder.Tests/`). Entry: `Program.cs`.
  - `Components/` Blazor UI (AntDesign), `Controllers/` REST, `Services/` gRPC servers (`Api.proto` "API" service, `mediatoad.proto` MediaToad service from haku/mediatoad), `Providers/` (Youtube, CCC, RSS, Sonarr), `Tasks/` TickerQ jobs, `Data/` EF Core + Identity.
- `YTDownloader/` — Python (uv, 3.12) gRPC service wrapping yt-dlp. The .NET app is the gRPC client; address from `YTSM_DOWNLOADER_ADDRESS`.
- `scripts/` — Python client/bridge scripts for the gRPC API (chromecast, lounge, atv, …). Shared client + token logic in `common.py` / `auth.py`.
- `NetworkPlaybackAgent/` — Python playback agent (plus one reference .cs file).
- `AndroidTV/` — Android TV client: Kotlin/Compose for TV app (AGP 9.4.0), built as the `mediafeeder-android-tv` package in `AndroidTV/default.nix` (with the `android` dev shell).
- `Might be useful/` — dead archive of the old API/Web split. Not in the solution; ignored by MegaLinter and Renovate. Do not modify or reference.

## Commands
- .NET 10 SDK required. `dotnet build` from repo root. The build enforces all C# analyzers
  (`EnforceCodeStyleInBuild`) — code must pass analyzers to build; `.editorconfig` is the source of truth.
- Tests: `MediaFeeder.Tests/` — xUnit v3 on Microsoft Testing Platform (Shouldly, NSubstitute, AutoFixture.AutoNSubstitute),
  mirroring the main project's analyzer stack. Run `dotnet test --solution MediaFeeder.sln` from repo root.
  The root `global.json` selects the MTP runner — xUnit v3 is MTP-based and the .NET 10 SDK no longer supports it via VSTest.
  There is no pytest; Python parts have no test suite. Verify with `dotnet build` + `dotnet test` + CI MegaLinter.
- Running the app requires: Postgres (appsettings.json: `ytsm` / `ytsm` / `verysecurepassword` @ localhost:5432),
  `MediaRoot` (mandatory — app throws at startup if missing; data-protection keys go to `$MediaRoot/dpkeys`),
  `YTSM_DOWNLOADER_ADDRESS`, `youtube_api_key`, and an `Auth` section (OIDC/JWT). Use `dotnet user-secrets`
  for dev secrets (UserSecretsId is in the csproj).
- DB migrations auto-apply at startup (`Program.cs` `MigrateAsync`). Create schema changes with `dotnet ef migrations add <Name>`; no manual `database update` step.
- Python: scripts dev shell is `nix develop .#scripts` (grpc tools, pyatv, pychromecast, …). Repo ruff (nix treefmt)
  uses `line-length = 180` and excludes `YTDownloader/` and `NetworkPlaybackAgent/`. `YTDownloader/` uses `uv sync` / `uv run` (`pyproject.toml` + `uv.lock`).
- Android TV app: `nix build .#mediafeeder-android-tv` from the repo root (JDK 21 + Android SDK 36 + Gradle 9.6.0 via
  nixpkgs; the non-free SDK is accepted in `AndroidTV/default.nix`, maven deps are locked in `AndroidTV/deps.json`
  and replayed from a mitm cache — sandboxed, no env vars, no `--no-sandbox`; APK at `result/app-debug.apk`).
  Dev shell: `nix develop .#android`. `AndroidTV/README.md` has the details, incl. how to re-record `deps.json`.

## Codegen (nothing generated is committed)
- C# gRPC stubs: built by Grpc.Tools from `MediaFeeder/Services/*.proto` and `YTDownloader/downloadServer.proto`.
- Python gRPC stubs: generated in the Docker build (`YTDownloader/Dockerfile`) or via the nix `mfprotos` package (for scripts). Never commit `*_pb2*.py`.
- When editing a `.proto`, the .NET app, the YTDownloader server, and the Python scripts all consume it — keep them in sync.

## Gotchas
- The root `downloadServer.proto` is tracked but stale (VideoId vs VideoUrl). The live file is `YTDownloader/downloadServer.proto` (referenced by the csproj).
- `RunAOTCompilation=true` — publish is AOT (Dockerfile uses `UseAppHost=false`); avoid native-heavy dependencies.
- `packages.lock.json` is committed (`RestorePackagesWithLockFile`) — update the lockfile when changing package versions.
- `nginx.conf` is a leftover from the old static-web architecture; not used by the current Docker build.
- The `build-grpc` pre-commit hook in `scripts/default.nix` references a root-level `Api.proto` that does not exist (real file: `MediaFeeder/Services/Api.proto`) — treat it as stale. The nix pre-commit config is generated; `.pre-commit-config.yaml` is gitignored.

## CI / deployment
- GitHub Actions on PR/push to `main`: MegaLinter (bot may open an auto-fix PR; opt out with "skip fix" in the commit message), `dotnet test` (MTP runner via root `global.json`), + builds two ghcr images on `main`: `<repo>-API` (`MediaFeeder/Dockerfile`) and `<repo>-Downloader` (`YTDownloader/Dockerfile`).
- Renovate manages dependency updates (ignores `Might be useful/`).
- Observability: `/healthz`, Prometheus `/metrics` (anonymous), gRPC health checks; gRPC reflection in Development only.

# Copilot Instructions — Roadnik

## Repository overview

Roadnik is a geolocation-sharing platform consisting of four components that live in a single repo:

| Component | Path | Technology |
|---|---|---|
| Server | `Roadnik/` | C# .NET 10, ASP.NET Core minimal APIs, AOT published |
| Shared library | `Roadnik.Common/` | C# .NET 10 (DTOs, API paths, shared toolkit) |
| Android app | `Roadnik.MAUI/` | C# .NET MAUI (`net10.0-android`) |
| Web frontend | `www-vue/` | Vue 3, TypeScript, Vite, Leaflet |

`build_common/` is a git submodule (`github.com/casualshammy/build_common`) providing Python build utilities used by `build-server.py` and `build-client.py`.

---

## Build commands

### Vue frontend (`www-vue/`)
```bash
npm run debug        # Vite dev server
npm run watch        # Vite dev server + tsc --watch in parallel
npm run build        # type-check then production build
npm run type-check   # vue-tsc only
```

### Server (Docker, requires env vars `DOCKER_REPO` / `DOCKER_LOGIN` / `DOCKER_PASSWORD`)
```bash
python build-server.py
```

### Android app (requires `ANDROID_SIGNING_KEY_PASSWORD` env var)
```bash
python build-client.py --framework net10.0-android
```

Or directly, without signing:
```bash
dotnet publish Roadnik.MAUI -c Release -f net10.0-android
```

> The build scripts also auto-increment the version from the git branch name and commit index, then merge into `main`. Run `dotnet publish` directly for local iteration.

There are **no automated tests** in this repository.

---

## Architecture

### Core concept: Rooms
The central abstraction is a **Room** — identified by a string key (no passwords). A room stores:
- **Path points** (`paths.v2.db`): GPS track points keyed by `(roomId, userName)`.
- **Waypoints/Points** (`data.v2.db`): Named map markers within a room.

Multiple users (identified by nickname) can share a room simultaneously. There is no user authentication — knowing the room key is sufficient.

### Server modules
The server uses the `Ax.Fw.App` DI framework (not the default ASP.NET Core DI for application-level modules). Modules are registered in `Program.cs` with:
```csharp
.AddModule<FooImpl, IFoo>()
```
Each module class implements `IAppModule<IFoo>` and exposes a static factory:
```csharp
public static IFoo ExportInstance(IAppDependencyCtx _ctx) { ... }
```
Dependencies are resolved by passing a delegate to `_ctx.CreateInstance(...)` — the framework resolves the delegate's parameters from the container.

Key server modules:
- **`WebServerImpl`** — Kestrel host, registers middleware and routes. Uses `WebApplication.CreateSlimBuilder()`.
- **`RoomsControllerImpl`** — Business logic for rooms: storing points, cleanup, enforcing limits.
- **`DbProviderImpl`** — Three SQLite stores via `Ax.Fw.Storage`: `GenericData`, `Paths`, `Tiles`.
- **`WebSocketCtrlImpl`** — WebSocket session management.
- **`WsMsgControllerImpl`** — Translates domain events into WebSocket push messages.
- **`FCMPublisherImpl`** — Firebase push notifications for the Android app.
- **`StravaTilesProviderImpl`** — Manages authenticated Strava heatmap tile headers.

### API
- REST under `/api/v1/` — endpoint paths are constants in `Roadnik.Common/ReqRes/ReqPaths.cs`, used by both server routing and MAUI HTTP calls.
- WebSocket at `/api/v1/ws`.
- Admin endpoints are protected with `[ApiTokenRequired]` attribute + `ApiTokenAuthMiddleware` (checks `api-key` request header against `ROADNIK_ADMIN_API_KEY` env var).

### Server configuration
All settings are read from **environment variables** at startup via `AppConfig.TryCreateAppConfig()`. Required variables include `ROADNIK_WEBROOT`, `ROADNIK_LOG_DIR`, `ROADNIK_DATA_DIR`, `ROADNIK_BIND_IP`, `ROADNIK_BIND_PORT`, `ROADNIK_MAX_PATH_POINTS_PER_ROOM`, `ROADNIK_MAX_PATH_POINTS_AGE_HOURS`, `ROADNIK_MIN_REPORT_INTERVAL`, `ROADNIK_FIREBASE_JSON`, `ROADNIK_FIREBASE_PROJECT_ID`. Optional: `ROADNIK_TF_API_KEY`, `ROADNIK_MAP_TILES_CACHE_SIZE`, `ROADNIK_ADMIN_API_KEY`, `ROADNIK_STRAVA_SESSION`.

### MAUI app
- **Hard-coded to `https://roadnik.app`** — the app is not designed for use with a self-hosted server. The server URL is in `Roadnik.MAUI/Data/AppConsts.cs` (`ROADNIK_APP_ADDRESS`); deep-link host is in `Roadnik.MAUI/Platforms/Android/DeepLinkActivity.cs` (`DataHost`). Both must be changed to retarget the app.
- Uses `Ax.Fw.DependencyInjection` (`AppDependencyManager`) — same module pattern as the server.
- The static `MauiProgram.Container` holds the DI container. `CMauiApplication.Container` exposes it to ViewModels.
- The Vue frontend is bundled into the MAUI app as raw resources (`Roadnik.MAUI/Resources/Raw/webApp/`) served on virtual host `webapp.local`. `build-client.py` runs `npm run build` and copies the output there automatically.
- **`InteractableWebView`** bridges C# and the embedded Vue app: `IObservable<JsToCSharpMsg> JsonData` for JS→C# messages; C# sends messages to JS via `window.postMessage` patterns. Host message type constants are in `Roadnik.MAUI/Data/Consts.cs` (`HOST_MSG_*`, `JS_TO_CSHARP_MSG_TYPE_*`).

### Vue frontend
- Single-page app with Vue Router. Main view is `MapView.vue`.
- Map rendered with **Leaflet** + `leaflet-rotatedmarker`.
- Reactive state managed with **RxJS** observables.
- Communicates with the server via REST (`src/api/backendApi.ts`) and WebSocket (`websocket-ts`).
- When embedded in the MAUI app, tile requests are intercepted and proxied through the MAUI `WebDataCache`.

---

## Key conventions

### Naming
- **Private fields**: `p_` prefix (e.g., `p_log`, `p_storage`).
- **Constructor/method parameters**: `_` prefix (e.g., `_log`, `_storage`).
- **Implementations**: `FooImpl` for interface `IFoo`.
- **Constants**: `SCREAMING_SNAKE_CASE` in `Data/Consts.cs` in each project.

### JSON serialization (AOT-safe)
The server is compiled with `PublishAot=true`. All JSON types **must** be registered in a `System.Text.Json` source-generated `JsonSerializerContext` subclass (e.g., `RestJsonCtx`, `DocStorageJsonCtx`). Never use reflection-based serialization.

### Lifetime management (`Ax.Fw`)
`IReadOnlyLifetime` / `ILifetime` replaces `CancellationToken` for service lifetimes. Use:
- `_lifetime.Token` to get a `CancellationToken`.
- `_lifetime.ToDisposeOnEnding(resource)` to register disposables.
- `_lifetime.GetChildLifetime()` to create a scoped lifetime.

### Reactive programming
Rx.NET (`System.Reactive`) is used throughout for async event streams, scheduling, and state. Subjects (`Subject<T>`, `ReplaySubject<T>`) are used as internal event buses within modules.

### Logging
`ILog` from `Ax.Fw`. Create sub-loggers with `_log["category"]`. Log messages support markdown-style formatting: `**bold**` and `__underline__` (rendered in file logs).

### MAUI ViewModel pattern
All ViewModels extend `BaseViewModel`, which:
- Exposes `IReadOnlyDependencyContainer Container` (from `CMauiApplication`).
- Provides `SetProperty<T>(ref T field, T value, ...)` for `INotifyPropertyChanged`.

### Preferences storage in MAUI
All persistent settings keys are `PREF_*` constants in `Roadnik.MAUI/Data/Consts.cs`. Access via `IPreferencesStorage`.

### Versioning
Version is derived at build time from the git branch name + commit index by `build_common`. The `.csproj` version fields (`ApplicationDisplayVersion`, `ApplicationVersion`) are rewritten by the build scripts — do not set them manually for release builds.

---

## Deployment & distribution

- **Docker image**: `oixa/roadnik` (multi-arch: `linux/amd64`, `linux/arm64`).
- **Google Play Store description**: `.google-play/description.md`.
- **New README** (in progress): `README_new.md` — replaces the existing `README.md`.

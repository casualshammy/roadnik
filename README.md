# Roadnik

[![AI Level 2](https://ai-level.dev/badge/standard/2.svg)](https://ai-level.dev/level-2)

**Roadnik** is a privacy-friendly geolocation sharing system that lets you share your real-time location with others. The **server** and the built-in **web map** are fully self-hostable.

## Public instance

A ready-to-use instance is available at **[roadnik.app](https://roadnik.app)** — no registration required. Download the Android app from [Google Play](https://play.google.com/store/apps/details?id=com.axiolab.roadnik), open it, and it will generate a room key for you automatically.

The public instance is free to use. It has rate limits and storage quotas applied per room. No personal data is collected beyond the location points you choose to share.

<a href="https://play.google.com/store/apps/details?id=com.axiolab.roadnik">
  <img alt="Google Play" width="200px" src="https://play.google.com/intl/en_us/badges/static/images/badges/en_badge_web_generic.png" />
</a>

## How it works

The server organizes location data into **rooms**. Each room is identified by a **key** that acts as a shared secret — anyone with the key can read and write location points to that room. Because a room can hold entries from multiple users (identified by a **nickname**), a single room is enough for a group ride, hike, or trip.

> [!WARNING]
> Anyone who knows your key can access your location history. Use a long random string as your key. Roadnik App generates a secure key for you on first launch, and the server rate-limits requests to prevent brute-forcing.

## Components

### Server

A .NET 10 application that stores geolocation points and serves the web map. Key features:

- Stores latitude, longitude, altitude, speed, bearing, accuracy, battery level, signal strength, and heart rate
- Per-room track storage with configurable point limits and age expiry
- Built-in interactive map (Vue 3 + Leaflet) with auto-centering on the selected user
- Map tile support: **OpenStreetMap** (no key required), **Thunderforest** (optional API key), and **Strava heatmaps** (requires token)
- Real-time updates
- Optional room registration for higher rate limits and point quotas (via [API](./docs/api/room-management-api.md) only)
- Optional Firebase Cloud Messaging (FCM) push notifications

### Android App

A .NET MAUI Android app (API 28+) that sends your location to **roadnik.app** in the background.  
Features include:
- Built-in map with real-time location updates
- Uninterrupted background location reporting
- BLE heart rate monitor (HRM) support — pairs with any standard BLE HRM device and includes heart rate in each location report
- QR code room sharing
- Discord Rich Presence — while sharing is active, broadcasts approximate location, speed, and heart rate as a Discord activity with a direct link to the room

> [!NOTE]
> The Android app is hard-coded to connect to **roadnik.app** and is not suitable for use with a self-hosted server. If you run your own instance, use the built-in **web map** instead.

<details><a id="self-hosted-android" name="self-hosted-android"></a>
<summary>Adapting the app for a self-hosted server</summary>

1. **`src/mobile/Data/AppConsts.cs`** — set `ROADNIK_APP_ADDRESS` to your server URL:
   ```csharp
   public const string ROADNIK_APP_ADDRESS = "https://your-domain.example";
   ```

2. **`src/mobile/Platforms/Android/DeepLinkActivity.cs`** — set `DataHost` to your domain (required for QR-code room sharing deep links):
   ```csharp
   DataHost = "your-domain.example",
   ```

3. You must create and configure a Firebase project and link it to the app and server. This process is outside the scope of this README — refer to the [Firebase documentation](https://firebase.google.com/docs) for guidance.

After these changes, rebuild the app with `python build-client.py --framework net10.0-android`.

</details>

### Web Map

A Vue 3 / TypeScript SPA bundled with the server. It shows all tracks in a room on an interactive Leaflet map with auto-centering and live updates. Clicking a user's marker opens a popup with speed, battery, signal strength, and — when available — heart rate.

## Running the server

### Docker (recommended)

Multi-arch images (`linux/amd64`, `linux/arm64`) are published to [Docker Hub](https://hub.docker.com/r/oixa/roadnik).

Example `docker-compose.yml`:

```yaml
services:
  server:
    image: "oixa/roadnik:latest"
    restart: always
    ports:
      - "0.0.0.0:8080:8080/tcp"
    user: "1004:1004" # UID:GID of the roadnik user on the host
    environment:
      ROADNIK_WEBROOT: "/app/www"
      ROADNIK_LOG_DIR: "/var/roadnik/logs"
      ROADNIK_DATA_DIR: "/var/roadnik/data"
      ROADNIK_BIND_IP: "0.0.0.0"
      ROADNIK_BIND_PORT: "8080"
      ROADNIK_MAX_PATH_POINTS_PER_ROOM: "1000"
      ROADNIK_MAX_PATH_POINTS_AGE_HOURS: "720"
      ROADNIK_MIN_REPORT_INTERVAL: "9900" # ms
      ROADNIK_FIREBASE_JSON: "/var/roadnik/google_service_account.json"
      ROADNIK_FIREBASE_PROJECT_ID: "<your-firebase-project-id>"
      ROADNIK_TF_API_KEY: "<your-thunderforest-api-key>"       # optional
      ROADNIK_MAP_TILES_CACHE_SIZE: "10737418240"              # optional, bytes
      ROADNIK_ADMIN_API_KEY: "<your-admin-api-key>"            # optional
      ROADNIK_STRAVA_SESSION: "<your-strava-session-cookie>"   # optional
    volumes:
      - /home/roadnik:/var/roadnik
    tty: true
```

### Configuration

All settings are passed via environment variables.

**Required:**

| Variable | Description |
|---|---|
| `ROADNIK_WEBROOT` | Path to the web app folder |
| `ROADNIK_LOG_DIR` | Path to the logs folder |
| `ROADNIK_DATA_DIR` | Path to the data storage folder |
| `ROADNIK_BIND_IP` | IP address to bind to (e.g. `0.0.0.0`) |
| `ROADNIK_BIND_PORT` | Port to listen on |
| `ROADNIK_MAX_PATH_POINTS_PER_ROOM` | Maximum number of stored points per room |
| `ROADNIK_MAX_PATH_POINTS_AGE_HOURS` | Maximum age of stored points (hours) |
| `ROADNIK_MIN_REPORT_INTERVAL` | Minimum interval between accepted reports (ms) |
| `ROADNIK_FIREBASE_JSON` | Path to Firebase service account JSON file |
| `ROADNIK_FIREBASE_PROJECT_ID` | Firebase project ID |

**Optional:**

| Variable | Description |
|---|---|
| `ROADNIK_TF_API_KEY` | Thunderforest map tiles API key |
| `ROADNIK_MAP_TILES_CACHE_SIZE` | Local tile cache size in bytes |
| `ROADNIK_ADMIN_API_KEY` | API key for admin endpoints |
| `ROADNIK_STRAVA_SESSION` | Strava session cookie for heatmap tiles |

### Behind NGINX

```nginx
location / {
    proxy_pass http://127.0.0.1:8080/;

    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;

    proxy_http_version 1.1;

    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
}
```

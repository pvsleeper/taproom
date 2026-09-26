# Taproom — Phase 1 Spec

Sep 26, 2026 · @Pieter Venter

## Overview

Taproom is a self-hosted network dashboard that merges data from the Omada Software Controller and the OPNsense router into one Omada-style UI. Phase 1 delivers the app shell plus an Overview page listing every client on the network, wireless and wired, with search by name, IP or MAC.

**In scope for phase 1**

- C# backend that polls Omada and OPNsense, merges clients by MAC, and serves one JSON API
- React frontend with a left sidebar that drives the page on the right
- Overview page: summary counts, client table, search box
- Hardcoded URLs and credentials in a local config file

**Out of scope for phase 1**

- Authentication (the app sits behind Traefik's local-only middleware instead)
- Stored history, charts, per-client detail pages
- Actions such as blocking or reconnecting a client
- A settings UI for URLs and credentials

## Architecture

One deployable: an ASP.NET Core app that polls both sources in the background, keeps the latest merged snapshot in memory, and serves both the JSON API and the built React app.

| Layer | Choice | Notes |
| --- | --- | --- |
| Backend runtime | .NET 10, ASP.NET Core minimal APIs | Current LTS |
| Source clients | Typed `HttpClient` per source | `OmadaClient`, `OpnsenseClient`, each behind an interface |
| Polling | `BackgroundService` | Polls both sources in parallel on an interval |
| State | Singleton `ClientSnapshotStore` | Latest merged snapshot plus per-source status; no database |
| Frontend | React + TypeScript + Vite | Built output copied to backend `wwwroot` |
| Styling | Tailwind CSS | Dark theme first |
| Data fetching | TanStack Query | Refetch on an interval |
| Table | TanStack Table | Sorting and filtering |
| Routing | React Router | One route per sidebar item |
| Icons | lucide-react |  |

Repo layout:

```
taproom/
  backend/
    Taproom.Api/
      Program.cs
      Config/TaproomOptions.cs
      Sources/Omada/        # OmadaClient, DTOs, token handling
      Sources/Opnsense/     # OpnsenseClient, DTOs
      Clients/              # NetworkClient model, ClientMerger, ClientSnapshotStore
      Polling/PollerService.cs
      appsettings.json
      appsettings.Local.json   # gitignored, holds URLs + secrets
    Taproom.Tests/          # merger unit tests with fixture JSON
  frontend/
    src/
      app/Layout.tsx        # sidebar + content outlet
      app/nav.ts            # sidebar items as a config array
      pages/Overview/
      api/                  # typed fetch + TanStack Query hooks
  Dockerfile
  docker-compose.yml
```

## Data sources

Omada supplies wireless detail (AP, SSID, band, signal, traffic); OPNsense supplies the authoritative IP and hostname for every device, including wired ones Omada never sees. Endpoint paths below are the expected ones: Claude Code should confirm each against the live systems before coding the DTOs.

### Omada Software Controller (Open API)

- Setup: Settings → Platform Integration → Open API → add an app in **Client** mode, grant read access to the site. Gives a client ID and client secret.
- `omadacId`: `GET {baseUrl}/api/info` (no auth).
- Token: `POST {baseUrl}/openapi/authorize/token?grant_type=client_credentials` with JSON body `{ omadacId, client_id, client_secret }`. Send it as header `Authorization: AccessToken=<token>`. Cache it until shortly before expiry; on a 401, fetch a new one and retry once.
- Sites: `GET /openapi/v1/{omadacId}/sites?page=1&pageSize=100` (single site expected).
- Clients: `GET /openapi/v1/{omadacId}/sites/{siteId}/clients?page=1&pageSize=500`.
- Devices (for AP names): `GET /openapi/v1/{omadacId}/sites/{siteId}/devices`.
- The controller serves its own Swagger docs; treat those as the source of truth, since paths and fields shift between controller versions.

### OPNsense API

- Setup: System → Access → Users → dedicated user with read-only privileges for Diagnostics and DHCP pages → create API key. Auth is HTTP Basic with key:secret.
- The router uses a self-signed cert, so the OPNsense `HttpClient` needs an opt-in `AllowInvalidCertificate` flag scoped to that client only.
- ARP table: `GET /api/diagnostics/interface/get_arp`. Gives current IP, MAC, interface, sometimes hostname.
- DHCP leases, depending on which DHCP server is enabled:
  - Dnsmasq: `GET /api/dnsmasq/leases/search`
  - Kea: `GET /api/kea/leases4/search`
  - ISC: `GET /api/dhcpv4/leases/search_lease`
- Make the DHCP backend a config value (`DhcpProvider`) so the right endpoint is called.

### Config (hardcoded for phase 1)

Values live in `appsettings.Local.json`, which is gitignored and loaded after `appsettings.json`:

```json
{
  "Taproom": {
    "PollIntervalSeconds": 30,
    "Omada": {
      "BaseUrl": "https://omada.venter.center",
      "ClientId": "...",
      "ClientSecret": "...",
      "SiteName": "Default"
    },
    "Opnsense": {
      "BaseUrl": "https://10.0.1.1",
      "ApiKey": "...",
      "ApiSecret": "...",
      "AllowInvalidCertificate": true,
      "DhcpProvider": "Dnsmasq"
    }
  }
}
```

## Client model

Every device becomes one `NetworkClient`, keyed on MAC normalised to uppercase colon form (`AA:BB:CC:DD:EE:FF`). The merger takes the union of Omada clients, OPNsense ARP entries and DHCP leases, then fills each field from the best available source.

| Field | Type | Source (in precedence order) | Notes |
| --- | --- | --- | --- |
| `mac` | string | all | Merge key |
| `name` | string | Omada custom name → DHCP hostname → Omada hostname → ARP hostname → MAC | What the table shows |
| `hostname` | string? | DHCP → Omada → ARP | Shown under the name when it differs |
| `ip` | string? | ARP → DHCP lease → Omada | ARP reflects what is live now |
| `connection` | `Wireless` or `Wired` | Omada `wireless` flag; anything else is Wired |  |
| `online` | bool | In Omada's active list or a non-expired ARP entry |  |
| `ssid` | string? | Omada | Wireless only |
| `apName` | string? | Omada (AP MAC → device name) | Wireless only |
| `band` | string? | Omada | `2.4 GHz`, `5 GHz`, `6 GHz` |
| `rssi` | int? | Omada | dBm |
| `rxBytes`, `txBytes` | long? | Omada | Since connect; wired is null in phase 1 |
| `connectedSince` | DateTimeOffset? | Omada uptime |  |
| `lastSeen` | DateTimeOffset | Latest across sources |  |
| `isStaticLease` | bool? | DHCP |  |
| `sources` | string\[\] |  | e.g. `["omada","arp","dhcp"]`, handy for debugging |

Rules:

- A DHCP lease with no ARP entry and no Omada entry is included as `online: false`.
- The router's own interfaces and the APs appear like any other client in phase 1.
- `ClientMerger` is a pure function over the three input lists so it can be unit-tested with captured JSON fixtures.

## Backend API

The frontend calls one endpoint, `GET /api/clients`, which returns the latest in-memory snapshot instantly; it never calls Omada or OPNsense on the request path.

| Endpoint | Returns |
| --- | --- |
| `GET /api/clients` | Snapshot: generated time, per-source status, full client list |
| `GET /api/health` | 200 with per-source status, for Docker health checks |

Example response:

```json
{
  "generatedAt": "2026-09-26T10:15:30+10:00",
  "sources": {
    "omada":    { "ok": true,  "lastSuccess": "2026-09-26T10:15:30+10:00", "error": null },
    "opnsense": { "ok": false, "lastSuccess": "2026-09-26T10:14:30+10:00", "error": "Timeout" }
  },
  "clients": [
    {
      "mac": "AA:BB:CC:DD:EE:FF", "name": "Pieter-iPhone", "hostname": "pieters-iphone",
      "ip": "10.0.1.52", "connection": "Wireless", "online": true,
      "ssid": "venter", "apName": "EAP670 Downstairs", "band": "5 GHz", "rssi": -52,
      "rxBytes": 1830000000, "txBytes": 240000000,
      "connectedSince": "2026-09-26T07:02:11+10:00", "lastSeen": "2026-09-26T10:15:30+10:00",
      "isStaticLease": false, "sources": ["omada", "arp", "dhcp"]
    }
  ]
}
```

Polling behaviour:

- `PollerService` runs every `PollIntervalSeconds` (default 30) and fetches Omada and OPNsense in parallel with a 10-second timeout each.
- If one source fails, the merge reuses that source's last good data and marks it `ok: false` with the error. One source being down never empties the table.
- The first poll runs at startup. Until it completes, `/api/clients` returns 503 so the UI shows a loading state.
- JSON is camelCase, enums as strings, timestamps ISO 8601 with offset.
- Search is done client-side in phase 1, so there are no query parameters.

## Frontend

A fixed left sidebar selects the page; the right side shows it. Phase 1 has one sidebar item, Overview, but the sidebar is driven by a config array so adding Actions or other pages later is one entry plus one route.

### Layout

- **Sidebar** (240 px, collapsible to a 64 px icon rail): Taproom wordmark at the top, nav items with icon and label, active item highlighted with an accent bar. Below 768 px it becomes a slide-out drawer behind a hamburger button.
- **Page header**: page title on the left. On the right, "Updated 12 s ago" plus one status pill per source (green when `ok`, amber when stale, with the error in a tooltip).
- **Content area**: scrolls independently of the sidebar.

### Overview page

1. **Summary cards** in a row: Total clients, Online, Wireless, Wired.
2. **Toolbar**: search box (placeholder "Search name, IP or MAC") plus a segmented filter: All / Wireless / Wired / Offline.
3. **Client table**, one row per client:

| Column | Content |
| --- | --- |
| Status | Green dot online, grey dot offline |
| Name | `name`, with `hostname` in small muted text underneath when different |
| IP | Monospace; sorts numerically, not as text |
| MAC | Monospace |
| Connection | Wi-Fi or ethernet icon; for wireless, SSID and band |
| AP | AP name; blank for wired |
| Signal | RSSI in dBm with a 4-bar icon: green ≥ −60, amber −60 to −70, red < −70 |
| Traffic | ↓ rx / ↑ tx, human-readable (KB, MB, GB) |
| Uptime | From `connectedSince`, e.g. "3h 12m" |

Table behaviour:

- Click a column header to sort. Default sort is online first, then name A–Z.
- Compact 40 px rows, sticky header, the table scrolls horizontally inside its container on narrow screens.
- Data refetches every 30 seconds via TanStack Query without resetting sort, search or scroll position.
- Loading shows skeleton rows. If both sources fail, show an error banner above the last data.

### Search

- Case-insensitive substring match against `name`, `hostname`, `ip` and `mac`.
- MAC matching ignores separators, so `aabbcc`, `AA:BB:CC` and `aa-bb-cc` all match.
- Debounced by 150 ms and combined with the segmented filter.
- Matching text is highlighted in the row. The search box has a clear (×) button, and pressing `/` focuses it.

### Visual style

- Dark theme by default, modelled on the Omada UI: dark navy sidebar, slightly lighter card surfaces, 8 px corner radius, subtle borders rather than heavy shadows.
- One accent colour (teal) for the active nav item, focus rings and primary highlights.
- Inter for UI text, a monospace font for IPs and MACs.
- Colours defined as CSS variables or Tailwind theme tokens so a light theme can be added later.

## Running and deployment

In development the backend and the Vite dev server run side by side. In production a single container on brewhouse serves everything at `taproom.venter.center`.

**Local development**

- Backend: `dotnet run` in `backend/Taproom.Api`, listening on `http://localhost:5080`.
- Frontend: `npm run dev` in `frontend`, on port 5173, with a Vite proxy sending `/api` to `localhost:5080`.
- The dev machine must be on the LAN (or on WireGuard) to reach both sources.

**Production (brewhouse)**

- Multi-stage `Dockerfile`: a Node stage builds the frontend, a .NET SDK stage publishes the backend with the frontend `dist` copied into `wwwroot`, and the final image is `mcr.microsoft.com/dotnet/aspnet:10.0`.
- The backend serves static files and falls back to `index.html` for client-side routes.
- `docker-compose.yml` lives at `/data/docker/taproom`, mounts `appsettings.Local.json` read-only, and sets a health check on `/api/health`.
- Traefik labels route `taproom.venter.center` to the container, with the existing `local-only` middleware because phase 1 has no login.
- The Omada controller runs with `network_mode: host`, so the container reaches it via `omada.venter.center` or brewhouse's LAN IP, not a Docker network name.

## Build order and acceptance criteria

Claude Code should build in this order, getting real data flowing before any UI polish.

1. Scaffold the repo layout, the backend with `/api/health`, and the frontend with the Vite proxy.
2. `OpnsenseClient`: fetch ARP and DHCP leases, and log the raw JSON once to confirm the field names.
3. `OmadaClient`: token flow, site lookup, clients and devices; log the raw JSON once as well.
4. Save the captured JSON as test fixtures, then build `ClientMerger` with unit tests (MAC normalisation, name precedence, wired vs wireless, offline DHCP-only clients).
5. `PollerService` and `ClientSnapshotStore`, then `/api/clients`.
6. Frontend shell: sidebar, page header, routing.
7. Overview page: cards, table, sorting, search, filter, auto-refresh.
8. Visual polish, then the Dockerfile and compose file.

Phase 1 is done when:

- [ ] `/api/clients` returns every device on the LAN, with wireless ones carrying AP, SSID, band and RSSI
- [ ] A device seen by both Omada and OPNsense appears once, not twice
- [ ] Wired devices that Omada never sees still appear, with IP and hostname from OPNsense
- [ ] Stopping one source keeps the table populated and turns that source's status pill amber
- [ ] Search finds a device by partial name, partial IP, or MAC in any separator format
- [ ] Sort, search and filter survive the 30-second refresh
- [ ] The page works on a phone-width screen (drawer sidebar, horizontally scrolling table)
- [ ] `docker compose up` on brewhouse serves the app at `taproom.venter.center`
- [ ] Merger unit tests pass

## Later phases

These are parked, not designed. Each becomes a sidebar item or an extension of the Overview page.

| Idea | What it adds | Likely source |
| --- | --- | --- |
| Actions | Block or unblock a client, reconnect it, reboot an AP | Omada Open API write endpoints, OPNsense firewall aliases |
| Client detail page | Everything about one device, plus its history | Merged data + stored history |
| History | Stored snapshots for charts and "first seen" / "last seen" | SQLite or Postgres on brewhouse |
| WAN and interfaces | Live throughput and daily totals | OPNsense interface statistics and system health |
| Access points | Per-AP client count, channel, utilisation | Omada devices |
| DNS | Query volume, cache hit rate, top domains | OPNsense Unbound stats |
| WireGuard | Peers, last handshake, transfer | OPNsense WireGuard API |
| Wired traffic | Per-device traffic for wired clients | OPNsense NetFlow/Insight or top talkers |
| Vendor lookup | Device maker from the MAC's OUI prefix | Local OUI database |
| Proper config | Secrets out of files, a settings page | Environment variables or a secrets store |

# Taproom — Phase 5 Spec

Sep 30, 2026 · @Pieter Venter

Phase 5 gives Taproom a real home page. A new Dashboard shows the whole network at once — WAN bandwidth, the busiest devices, and one live map of everything the house is talking to — and the current Overview page is renamed Clients, which is what it is.

## Scope

A new Dashboard becomes the app's home page, and the existing client list moves under a Clients menu item. Almost everything on the Dashboard reuses components and backend services from phases 3 and 4, fed network-wide instead of per client.

**In scope**

- Rename the Overview page to **Clients**, with no behaviour change
- New **Dashboard** page at `/`, with:
  - Summary tiles: clients, WAN rate, DNS activity, source status
  - Live WAN download/upload chart
  - Network-wide live connection map, arcs coloured by device
  - Top talkers list: busiest devices right now, each linking to its detail page

**Out of scope**

- Access Points and DNS pages (later)
- Actions such as pause or block (a later phase, on the client detail page)
- Stored history; the WAN chart lives in the browser like phase 4's

**Rules carried over**

- No persistence and no idle polling. Everything is fetched while the Dashboard is open and visible.
- Clients are identified by their full address set (IPv4 from ARP, IPv6 from NDP).
- Failed DNS lookups are counted as failed, not blocked.

## Navigation

The sidebar gains Dashboard at the top and renames Overview to Clients; client detail pages stay a drill-down, not a menu item.

| Sidebar item | Route | Icon (lucide) | Notes |
| --- | --- | --- | --- |
| Dashboard | `/` | `LayoutDashboard` | New home page |
| Clients | `/clients` | `Users` | The former Overview page, unchanged |
| (detail) | `/clients/:mac` | — | Unchanged; Clients stays highlighted in the sidebar while on it |

- If the phase 1 Overview page lived at `/`, any old bookmark to it now lands on the Dashboard, which is fine. If it had its own path (e.g. `/overview`), redirect that path to `/clients`.
- The detail page's back link changes its label to "Clients".
- Links from the Dashboard (top talkers, map tooltips) go to `/clients/:mac`, and their back link returns to the page they came from, using router history.

## Data sources

One new OPNsense call (WAN interface counters); the rest are phase 3 and 4 calls made without a client filter. No new privileges beyond what phase 4 granted, unless the WAN counters endpoint turns out to need its own.

| Need | Source | New? |
| --- | --- | --- |
| WAN download/upload rate | Interface byte counters behind Reporting → Traffic (the graph tab), expected `/api/diagnostics/traffic/interface`; rate = counter delta ÷ seconds between samples | New call |
| All connections | `query_states` with no `searchPhrase`, LAN-side states only | Same call, no filter |
| Busiest devices | Phase 4 top-talkers sample of the LAN interface | Reused as-is |
| DNS activity, last hour | Unbound totals behind Reporting → Unbound DNS → Overview, expected `/api/unbound/overview/totals/{n}` | New call, same privilege |
| Client names and addresses | Phase 2 snapshot provider, plus NDP | Reused |

Notes for Claude Code:

- Confirm both new endpoints in dev tools and save fixtures, as in earlier phases.
- **WAN direction:** on the WAN interface, bytes in are the house's download and bytes out are its upload. That's the opposite of the LAN-side mapping in phase 4, so confirm it with a large download.
- **Counter resets:** treat a counter that goes down (router reboot) as a new baseline, never a negative rate.
- **Unfiltered states** can run to thousands of rows on a busy evening. Request a generous `rowCount` (e.g. 5000) and log a warning if the total exceeds it.

## Backend

Four dashboard endpoints. The only genuinely new logic is attributing each connection to a device and naming IPs from the whole network's DNS activity; the rest reuses phase 3 enrichment and phase 4 sampling.

### Endpoints

| Endpoint | Returns | Server cache |
| --- | --- | --- |
| `GET /api/dashboard/summary` | Client counts (online, wireless, wired), DNS last hour (queries, failed, blocked), per-source status | 10 s |
| `GET /api/dashboard/wan` | `sampledAt`, `downBps`, `upBps` | 2 s, shared |
| `GET /api/dashboard/connections` | All enriched connections with device attribution, plus markers | 5 s, shared |
| `GET /api/dashboard/top-talkers?limit=5` | Busiest devices from the LAN sample: MAC, name, `downBps`, `upBps` | Phase 4 `InterfaceSampler` (2 s) |

### Device attribution

- Build an address → client map from the snapshot: every IPv4 and IPv6 address points to its client's MAC and name.
- Each connection's source address is looked up in that map and gains `mac` and `deviceName`.
- An address not in the map becomes "Unknown device" with the raw address, and is still drawn.
- Top talkers use the same map, summing a client's IPv4 and IPv6 rates into one row.

### Network-wide naming

- Rung 1 (DNS) uses **everyone's** queries from the last 60 minutes, fetched in one unfiltered log call capped at 10,000 rows. If the TV looked up a domain, a phone's connection to the same IP gets named too.
- Rungs 2–4 (PTR, ASN, bare IP) and GeoIP are unchanged and share phase 3's caches.
- The per-client detail page keeps its own 24-hour, per-client matching; the Dashboard's wider but shorter window is only for the Dashboard.

### Markers

- Grouped by location as in phase 3.
- Each marker also carries a per-device breakdown (`devices: [{ mac, name, connectionCount, downBps, upBps }]`) for the tooltip and colouring.
- Connection rates come from phase 4's `ConnectionRateTracker`, keyed the same way, so the Dashboard and detail pages share previous readings.

### Load

- All four endpoints use shared, single-flight caches, so several open Dashboards cost the same as one.
- With no Dashboard or detail page open, no OPNsense calls are made.

## Frontend

The Dashboard reads top to bottom: status at a glance, then the map as the centrepiece, then bandwidth and who's using it.

### Layout

1. **Tiles row** (four across; two by two on phones)
2. **Network map** (full width, about 480 px tall on desktop)
3. **WAN chart** (about two-thirds width) beside **Top talkers** (one-third). They stack on narrow screens.

### Tiles

| Tile | Big figure | Small text |
| --- | --- | --- |
| Clients | Online count | "of N · W wireless · D wired" |
| Internet | Live ↓ rate | Live ↑ rate |
| DNS, last hour | Query count | Failed and blocked counts, failed in amber |
| Sources | Green dots for Omada and OPNsense | Last update time; amber with a tooltip on error |

Clicking the Clients tile goes to `/clients`.

### Network map

- The phase 3 map component, fed by `/api/dashboard/connections`, with phase 4's rate-driven arc width and dash speed.
- **Colour by device:** the eight devices with the most connections when the page loads get distinct categorical colours. Every other device is neutral grey. A device keeps its colour for the rest of the session, so the map doesn't flicker as rankings shift.
- **Legend:** a row of device chips under the map, one per coloured device plus "Others". Clicking a chip shows only that device's arcs; clicking it again shows all.
- **Tooltip:** location, then the per-device breakdown (name, connection count, live rate). Each device name links to its detail page.
- **Performance:** if there are more than 400 connections, draw arcs per marker per device instead of per connection.

### WAN chart

- The phase 4 bandwidth card component, fed by `/api/dashboard/wan` every 2 seconds.
- Same 5-minute browser-only buffer, gap handling, Mbps / MB/s toggle and peaks.

### Top talkers

- The five busiest devices right now, refreshed every 3 seconds.
- Each row shows the device's colour dot (if it has one), its name, and live ↓ / ↑ rates with a small bar scaled to the busiest device.
- Clicking a row opens its detail page.
- When everything is idle, show "All quiet" instead of five rows of zeros.

### Refresh

| Data | Interval |
| --- | --- |
| Summary tiles | 10 s |
| WAN chart | 2 s |
| Top talkers | 3 s |
| Map | 5 s |

All polling stops when the tab is hidden.

## Build order and acceptance criteria

Do the rename first so the app stays usable throughout, then the backend endpoints, then the page.

1. Rename Overview to Clients, move it to `/clients`, add the Dashboard route with a placeholder, and update the sidebar and back links.
2. Capture fixtures for the WAN interface counters and Unbound totals endpoints; confirm WAN direction with a large download.
3. Address → client map and device attribution, with unit tests: IPv4 and IPv6 both attribute to the same MAC, unknown addresses become "Unknown device".
4. Network-wide naming using the 60-minute all-clients DNS window, with a unit test where one device's lookup names another device's connection.
5. The four dashboard endpoints with shared caches.
6. Tiles, then the WAN chart and top talkers, reusing phase 4 components.
7. The network map: device colouring, legend chips with solo mode, tooltip breakdown, and the per-marker fallback above 400 connections.

Phase 5 is done when:

- [ ] Opening Taproom lands on the Dashboard; Clients and client detail pages work as before
- [ ] Starting a download on brewhouse shows in the WAN chart, puts brewhouse at the top of Top talkers, and thickens its arcs, all within about 5 seconds
- [ ] A Netflix stream on the TV and a download on brewhouse show as two differently coloured sets of arcs
- [ ] Clicking a legend chip shows only that device's arcs; clicking again restores all
- [ ] Device colours stay the same for the whole session, even as rankings change
- [ ] A connection whose domain only another device looked up still gets a DNS name
- [ ] The DNS tile's failed count includes lookups like `codeasp.net` and its blocked count doesn't
- [ ] WAN download and upload aren't swapped
- [ ] With the Dashboard in a hidden tab, backend logs show no OPNsense calls
- [ ] Two Dashboards open at once make the same number of OPNsense calls as one
- [ ] Unit tests pass

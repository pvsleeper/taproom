# Taproom — Phase 3 Spec

Sep 28, 2026 · @Pieter Venter

Phase 3 adds a client detail page. Its centrepiece is a live world map of every connection the device has open right now, each labelled with the domain name it looked up, alongside a feed of its DNS activity.

## Scope

Clicking a row on the Overview page opens `/clients/:mac`, a detail page with three parts: a device header, a live connection map with its table, and a DNS activity panel. Like phase 2, everything is fetched on demand, and only while the page is open.

**In scope**

- Client detail page and route, reached from the Overview table
- Live connections from the OPNsense firewall state table, refreshed every 5 seconds
- Remote IPs labelled with domain names, country, city and network owner
- World map with arcs from home to each destination
- DNS panel: top domains, blocked count and a live query tail from Unbound's query log

**Out of scope**

- Stored history of connections or DNS beyond what Unbound already keeps
- Blocking or pausing the device (a later Actions phase)
- Alerts, such as a new country or a new domain
- Deep packet inspection or per-connection bandwidth graphs

**Prerequisites on OPNsense**

- Reporting → Settings: Unbound DNS reporting enabled, so the query log exists.
- The `taproom` API user gains the privileges listed under Data sources.

**Privacy:** this page shows the browsing activity of anyone on the network, so it stays behind Traefik's `local-only` middleware, and adding a login is a prerequisite for ever exposing Taproom beyond the LAN.

## Data sources

Both new sources are OPNsense APIs; Omada adds nothing new in this phase. The paths below are the expected ones. Claude Code must confirm each by opening the matching OPNsense page with browser dev tools on the Network tab, then capturing one real response as a test fixture before writing DTOs.

### Firewall states: live connections

- UI page: Firewall → Diagnostics → States.
- API: `POST /api/diagnostics/firewall/query_states`, bootgrid-style body (`current`, `rowCount`, `searchPhrase`). Use the client's IP as `searchPhrase` to filter server-side.
- Useful fields per state: protocol, source and destination address and port, direction, state, bytes, packets, age, interface.
- Keep only states where the source is the client's LAN IP and the destination is public, taken from the LAN-side interface so addresses are pre-NAT. Private and link-local destinations go to a separate "Local" list and never reach the map.
- Privilege: **Diagnostics: Show States**.

### Unbound DNS query log

- UI page: Reporting → Unbound DNS → Details.
- API: the query search endpoint behind that page (expected `/api/unbound/overview/search_queries`), filtered by client IP.
- Useful fields per query: time, client IP, domain, record type, action (pass or block), response code, blocklist that matched.
- The log records the domain queried, not the IP addresses returned. That is why the enrichment step re-resolves domains (next section).
- Privilege: the Unbound DNS reporting page; type "Unbound" in the privilege filter to find it.

### Local databases for GeoIP

- **MaxMind GeoLite2 City** and **GeoLite2 ASN** `.mmdb` files. They're free with a MaxMind account and licence key, and read locally with the `MaxMind.GeoIP2` NuGet package, so there are no per-lookup API calls.
- The files are mounted into the container at `/data/geoip`. A weekly refresh via MaxMind's `geoipupdate` container can run alongside.
- If a MaxMind account is unwanted, DB-IP Lite has the same `.mmdb` format and needs no account, but requires attribution in the UI.

### Config additions

```json
"Taproom": {
  "Home": { "Latitude": -27.47, "Longitude": 153.03, "Label": "Home" },
  "GeoIp": { "CityDbPath": "/data/geoip/GeoLite2-City.mmdb", "AsnDbPath": "/data/geoip/GeoLite2-ASN.mmdb" },
  "Opnsense": { "DnsServer": "10.0.1.1" }
}
```

## Enrichment

The backend turns raw firewall states into named, located connections. Its key trick is re-resolving the domains the device itself queried, so an IP like `142.250.66.206` shows as `youtube.com`.

&#91;embedded content: connection enrichment · 5 steps, 4-level naming fallback\]

**Domain matching (rung 1)**

- Take the client's distinct A and AAAA queries from the last 30 minutes.
- Resolve each one through the OPNsense resolver (`DnsServer` in config) using the `DnsClient` NuGet package. These are nearly all cache hits, so the answers match what the device received.
- Build an IP → domain map. Across CNAME chains, keep the domain the device actually asked for.
- If several domains map to the same IP, which is common on CDNs, show the most recently queried one plus "+N more" on hover.
- Cache each resolution for its TTL, clamped between 60 seconds and 10 minutes.

**Other rungs**

- PTR lookups use a 1-second timeout and are cached for 1 hour.
- ASN organisation names come from the local GeoLite2 ASN database.

**GeoIP and grouping**

- Each remote IP gets country, city, latitude and longitude, ASN and organisation.
- Table rows are grouped by remote IP and port.
- Map markers are grouped by location rounded to about city level, so 20 connections to one Sydney data centre become one marker with a count.
- IPs with no location go to an "Unknown location" list, not the map.

**Known limits**

- **Encrypted DNS:** devices using their own encrypted DNS (DNS over HTTPS in some browsers and TVs) bypass Unbound. They get no domain names and fall back to rungs 2–4. If a device has many public connections but few Unbound queries, show a hint: "This device may be using its own DNS".
- **CDN locations:** CDN and anycast IPs geolocate to the edge server, often Sydney or Brisbane, not the company's home country. The map shows where traffic really goes, which is correct but can surprise.
- **VPNs:** a device running its own VPN shows only the VPN endpoint.
- **IPv6:** works if the states include it, since GeoLite2 covers both address families.

## Backend API

Three new endpoints, keyed on the client's MAC so the page survives an IP change; the backend looks up the current IP from the phase 2 snapshot provider.

| Endpoint | Returns | Server cache |
| --- | --- | --- |
| `GET /api/clients/{mac}` | The one client from the snapshot, for the page header | Phase 2 snapshot cache |
| `GET /api/clients/{mac}/connections` | Enriched live connections, map markers, local and unknown lists | 3 s per client |
| `GET /api/clients/{mac}/dns?minutes=60&tail=50` | Top domains, pass and block counts, latest queries | 5 s per client |

A MAC not in the snapshot returns 404. A client with no current IP returns 200 with empty lists and `ip: null`.

Connections response:

```json
{
  "mac": "AA:BB:CC:DD:EE:FF",
  "ip": "10.0.1.52",
  "generatedAt": "2026-09-28T21:04:10+10:00",
  "home": { "lat": -27.47, "lon": 153.03 },
  "connections": [
    {
      "remoteIp": "142.250.66.206", "remotePort": 443, "protocol": "tcp",
      "name": "youtube.com", "nameSource": "dns", "otherNames": ["i.ytimg.com"],
      "country": "AU", "city": "Sydney", "lat": -33.87, "lon": 151.21,
      "asn": 15169, "org": "Google LLC",
      "bytes": 48200000, "packets": 36100, "ageSeconds": 412, "state": "ESTABLISHED"
    }
  ],
  "markers": [
    { "key": "-33.9,151.2", "lat": -33.87, "lon": 151.21, "label": "Sydney, AU", "connectionCount": 14, "bytes": 51000000 }
  ],
  "local": [ { "remoteIp": "10.0.1.10", "remotePort": 32400, "protocol": "tcp", "name": "brewhouse" } ],
  "unknownLocation": [],
  "hints": ["possibleOwnDns"],
  "sources": { "states": { "ok": true }, "dns": { "ok": true }, "geoip": { "ok": true } }
}
```

`nameSource` is one of `dns`, `ptr`, `asn`, or `ip`, so the UI can show how confident a label is.

DNS response:

```json
{
  "windowMinutes": 60,
  "totals": { "queries": 1840, "blocked": 212 },
  "topDomains": [ { "domain": "youtube.com", "count": 310, "blocked": false } ],
  "topBlocked": [ { "domain": "ads.example.net", "count": 96, "blocklist": "hagezi-pro" } ],
  "recent": [ { "time": "2026-09-28T21:04:08+10:00", "domain": "i.ytimg.com", "type": "A", "action": "pass", "rcode": "NOERROR" } ]
}
```

Behaviour:

- The per-client caches mean several tabs on one device cause one upstream call per window.
- Partial failure follows the phase 1 pattern. If Unbound's log is unavailable, connections still return with rungs 2–4 naming, and `sources.dns.ok` is false.
- Missing GeoIP databases don't fail the request. Connections return without locations, `sources.geoip.ok` is false, and `markers` is empty.
- New services: `StatesClient`, `UnboundLogClient`, `DomainResolver`, `GeoIpService`, `ConnectionEnricher`. The enricher is a pure function over their outputs so it can be unit-tested with fixtures.

## Frontend

The map is the hero: full width at the top of the page, with the connections table and DNS panel side by side beneath it (stacked on phones).

### Page layout

1. **Header card:** back link to Overview, device name and hostname, IP, MAC, connection type, AP and signal, online dot. Data comes from `/api/clients/{mac}`.
2. **Connection map:** about 420 px tall on desktop, 280 px on phones.
3. **Below the map:** connections table (about 60% width) and DNS panel (about 40%). Below 1024 px they stack, table first.

On the Overview page, rows become clickable (pointer cursor, hover highlight, Enter key) and open `/clients/:mac`.

### Connection map

- **Rendering:** SVG with `d3-geo`, `topojson-client` and the `world-atlas` `countries-110m` file bundled in the app. No map tiles and no external calls, so it works offline and restyles easily.
- **Projection:** Equal Earth, rotated so longitude 150°E sits in the centre. Australia then sits mid-map rather than on the edge.
- **Style:** dark ocean, countries a step lighter, hairline borders, all from the phase 1 theme tokens.
- **Home:** accent-coloured dot with a slow pulse ring at the configured home location.
- **Markers:** one per grouped location. Radius scales with the square root of bytes, with a count badge when there's more than one connection.
- **Arcs:** great-circle lines from home to each marker, drawn with `d3.geoInterpolate` so they curve naturally. Width scales with bytes. A moving dash pattern flows outward to show live traffic.
- **Colour:** arcs to names from DNS (`nameSource = dns`) use the accent colour. PTR, ASN or bare-IP names use amber, so unexplained connections stand out. A small legend explains this.
- **Live changes:** a new connection's arc draws in over about 600 ms. A closed connection fades out over 1 second. Items are keyed by remote IP and port, so refreshes animate rather than flash.
- **Interaction:**
  - Hovering a marker or arc shows a tooltip: location, names, connection count, bytes.
  - Clicking a marker filters the table to that location.
  - Hovering a table row highlights its arc.
  - Scroll to zoom and drag to pan, with `d3-zoom`. A "Fit connections" button zooms to the bounding box of all markers, which matters because many CDN endpoints sit in Sydney and Brisbane.

### Connections table

| Column | Content |
| --- | --- |
| Name | `name`, with a small badge for `nameSource` (DNS, PTR, ASN, IP); `otherNames` in a tooltip |
| Remote | `ip:port` in monospace, plus the protocol |
| Location | Country flag, city, country code |
| Network | Organisation and ASN |
| Traffic | Bytes, human-readable |
| Age | Connection age, e.g. "6m 52s" |

- Default sort is traffic, descending. Headers are sortable, and a small search box filters on name, IP and organisation.
- A collapsed "Local connections" section sits below the table, for LAN destinations such as brewhouse.

### DNS panel

- **Window selector:** 15 min / 1 h / 24 h, passed as `minutes`.
- **Tiles:** total queries, blocked count and blocked percentage.
- **Top domains:** top 10 with horizontal bars sized by count. Clicking a domain highlights that domain's arcs on the map.
- **Top blocked:** top 5, with the blocklist name in muted text.
- **Live tail:** latest 50 queries, newest on top, sliding in as they arrive. Blocked rows are tinted red. A pause button freezes the list while you read it.
- **Hint banner:** when `hints` includes `possibleOwnDns`, show: "This device may be using its own encrypted DNS, so names may be missing."

### Refresh

- Connections refetch every 5 seconds, and DNS every 10 seconds.
- Both stop when the tab is hidden (TanStack Query `refetchIntervalInBackground: false`), in keeping with phase 2's no-idle-polling rule.
- If a source reports `ok: false`, show a small amber note on the affected panel rather than an error page.

## Build order and acceptance criteria

Get real enriched data flowing and tested before building the map, because the map is only as good as its labels.

1. Grant the new OPNsense privileges, enable Unbound DNS reporting, and download the GeoLite2 City and ASN databases.
2. Capture real JSON from the states and Unbound log endpoints via browser dev tools, and save it as fixtures.
3. `StatesClient` and `UnboundLogClient` with DTOs built from the fixtures.
4. `DomainResolver` (with TTL cache) and `GeoIpService`.
5. `ConnectionEnricher` as a pure function, with unit tests: domain match, CNAME keeps the queried name, PTR/ASN/IP fallback, private IPs to Local, missing GeoIP to Unknown, marker grouping.
6. The three endpoints with per-client caches and partial-failure handling.
7. Detail page shell: route, clickable Overview rows, header card.
8. Connections table and DNS panel, since they're quick to verify against the raw data.
9. The map: static render first, then arcs and markers, then animation, tooltips, cross-highlighting, and zoom with "Fit connections".
10. Test with a phone, a smart TV, a browser with DNS over HTTPS on, and an idle IoT device.

Phase 3 is done when:

- [ ] Clicking any Overview row opens its detail page, and the URL can be bookmarked and reloaded
- [ ] Starting a YouTube video on a device makes new accent-coloured arcs appear within about 5 seconds, labelled with YouTube or Google domains
- [ ] Stopping it fades those arcs out as the states close
- [ ] A connection with no DNS match shows a PTR, ASN or bare-IP label in amber with the right badge
- [ ] LAN connections (e.g. to brewhouse) appear only under Local connections, never on the map
- [ ] The DNS panel's top domains and blocked counts match OPNsense's own Unbound report for the same client and window
- [ ] The live tail shows a new query within about 10 seconds of it happening
- [ ] A device using its own encrypted DNS shows the hint banner
- [ ] Stopping Unbound reporting or removing the GeoIP files degrades the page with amber notes instead of breaking it
- [ ] Hidden tabs stop polling, and backend logs show no states or Unbound calls with no detail page open
- [ ] The map stays smooth at 200 or more connections; group markers further if not
- [ ] Enricher unit tests pass

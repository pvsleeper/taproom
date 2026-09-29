# Taproom — Phase 4 Spec

Sep 29, 2026 · @Pieter Venter

Phase 4 adds a live bandwidth graph to the client detail page and makes the map's arcs grow with live traffic. As before, nothing is persisted: the graph's history lives in the browser while the page is open.

## Scope

The client detail page gains a live download/upload graph, and each connection gains a live rate that drives the map. Everything else from phase 3 stays as built.

**In scope**

- Live per-client download and upload rate, sampled every 2–3 seconds while the detail page is open
- A 5-minute rolling chart with current and peak rates, at the top of the detail page
- A live rate per connection in the connections table
- Map arcs whose width and flow speed follow the live rate instead of total bytes

**Out of scope**

- Any stored history. Closing the page drops the graph, and reopening starts it empty.
- Rates in the Overview table
- Whole-network or WAN graphs
- Alerts on bandwidth thresholds

**Rules carried over**

- No persistence. The only backend state is the previous sample kept in memory to compute deltas.
- No idle polling. Sampling happens only while at least one detail page is open and visible.
- Clients are matched on their full address set: IPv4 from ARP plus IPv6 from NDP.

## Data sources

The client's total rate comes from OPNsense's top talkers; each connection's rate comes from the change in its firewall-state byte counters between refreshes. Omada isn't used for rates, since its counters update too slowly for a smooth graph.

### Top talkers: per-client rate

- UI page: Reporting → Traffic → Top Talkers.
- API: the endpoint behind that page, expected to be `GET /api/diagnostics/traffic/top/{interfaces}`. Claude Code must confirm the path, parameters and response fields in dev tools before writing DTOs, and save one real response as a fixture.
- Each call samples the interface for a short period (about 1–2 seconds) and returns per-address rates in and out, plus cumulative bytes.
- Call it for the LAN interface only. The interface name goes in config (`Opnsense:LanInterface`, e.g. `lan`); confirm the identifier the API expects from the dev-tools request.
- **Direction:** on the LAN interface, traffic into the router from a client is that client's **upload**, and traffic out of the router to the client is its **download**. Confirm by starting a large download and checking which figure jumps.
- The response holds every active host on the interface, so one sample serves every open detail page.
- Privilege: the Reporting: Traffic page; type "Traffic" in the privilege filter to find it.

### Firewall state deltas: per-connection rate

- Source: the phase 3 `query_states` data already being fetched every 5 seconds.
- For each connection (keyed by client MAC, remote IP, remote port and protocol), the rate is the byte counter's increase divided by the seconds since the previous fetch.
- Keep the in and out counters separate, so each connection has a download and an upload rate.
- A connection seen for the first time has no rate until the next fetch; the UI shows "–".
- A counter that goes down means the connection was replaced. Treat it as new rather than showing a negative rate.
- Connections that open and close between fetches are invisible here. That's acceptable, because the per-client graph comes from top talkers, which does see them.

## Backend

One new endpoint for the client's rate, plus two new fields on each connection. A single shared top-talkers sample serves every client page open at the same moment.

### `GET /api/clients/{mac}/bandwidth`

```json
{
  "mac": "AA:BB:CC:DD:EE:FF",
  "sampledAt": "2026-09-29T21:10:04.512+10:00",
  "downBps": 338400000,
  "upBps": 8800000,
  "addresses": ["10.0.1.10", "2403:580a:ae97::1f91"],
  "source": { "ok": true, "error": null }
}
```

- Rates are in **bits per second**, summed across all of the client's addresses (IPv4 and IPv6).
- A client with no traffic in the sample returns `0` for both, not 404.
- An unknown MAC returns 404, as in phase 3.

### `InterfaceSampler` (singleton)

- Holds the latest top-talkers sample for the LAN interface and when it was taken.
- A sample younger than **2 seconds** is reused. Otherwise one new sample is taken and every waiting request shares it. Use `HybridCache` for the single-flight behaviour, or the same `SemaphoreSlim` pattern as phase 2.
- The sampling call uses its own timeout (5 seconds), not the caller's request token.
- If OPNsense fails, return the last sample if it's under 10 seconds old with `ok: false`. After that, return zeros with `ok: false`.
- No background timer. With no detail page open, no samples are taken.

### Connection rates

Each item in `/api/clients/{mac}/connections` gains two fields:

```json
{ "downBps": 41200000, "upBps": 950000 }
```

- Both are `null` when the connection has no previous reading.
- `ConnectionRateTracker` keeps the previous byte counters per connection key in `IMemoryCache`, with a 30-second sliding expiry so closed connections fall out on their own.
- Each `markers` item gains `downBps` and `upBps`, summed from its connections, for the map.

### Caching summary

| Data | Kept where | Lifetime |
| --- | --- | --- |
| Top-talkers sample | `InterfaceSampler` in memory | Reused for 2 s |
| Previous connection byte counters | `IMemoryCache` | 30 s sliding |
| Bandwidth chart history | Browser only | While the page is open |

## Frontend

A new bandwidth card sits between the header card and the map. The map and connections table pick up the live rates they already receive.

### Bandwidth card

- **Chart:** a 5-minute rolling area chart, built with Recharts. Download is drawn above the centre line in the accent colour, and upload is mirrored below it in a second colour. The Y axis scales to the visible data and is labelled in Mbps.
- **Current rates:** large figures to the left of the chart, e.g. "↓ 338 Mbps" and "↑ 8.8 Mbps", updating with each sample.
- **Peaks:** the highest download and upload in the visible window, in muted text under the current figures.
- **Units:** Mbps by default. A small toggle switches to MB/s, and the choice is remembered per browser in `localStorage`.
- **Status:** if `source.ok` is false, the chart freezes and shows a small amber "Sampling paused" note, without clearing what's already drawn.

### Live buffer (browser only)

- Poll `/api/clients/{mac}/bandwidth` every 2 seconds with TanStack Query, and stop when the tab is hidden.
- Keep up to 150 points (5 minutes at 2 seconds) in component state, dropping the oldest as new ones arrive.
- Use the server's `sampledAt` for the X position, so a slow response doesn't distort the timeline.
- After a gap longer than 10 seconds (hidden tab, network blip), draw a break in the line rather than joining across the gap.
- The buffer isn't saved anywhere. Leaving and returning to the page starts it empty.

### Map

- Arc width follows the marker's live `downBps + upBps` on a square-root scale, with a thin minimum so idle connections stay visible.
- The dash animation's speed follows the same rate: fast-moving dashes for an active download, near-still for keepalives.
- The marker tooltip adds the live rate, e.g. "↓ 41 Mbps ↑ 0.9 Mbps".
- The phase 3 colour rule (accent for DNS-named, amber for unexplained) is unchanged.

### Connections table

- The Traffic column becomes two columns: **Rate** (↓ / ↑, live, "–" when not yet known) and **Total** (the existing cumulative bytes).
- The default sort changes to Rate, descending, so active transfers float to the top.

## Build order and acceptance criteria

Confirm the direction of the top-talkers figures with a real download before building any UI on them.

1. Grant the Reporting: Traffic privilege to the `taproom` API user.
2. Capture a real top-talkers response in dev tools during a large download, and save it as a fixture. Note the interface identifier and which field jumped.
3. `InterfaceSampler` with the 2-second reuse, single-flight and fallback behaviour, plus unit tests over the fixture: address matching across IPv4 and IPv6, and the direction mapping.
4. `/api/clients/{mac}/bandwidth`.
5. `ConnectionRateTracker` and the `downBps` / `upBps` fields on connections and markers, with unit tests: first reading gives null, a counter reset is treated as new, keys expire.
6. Bandwidth card with the live buffer.
7. Map arcs and dash speed driven by rate, and the Rate and Total columns in the table.

Phase 4 is done when:

- [ ] Starting a large SABnzbd download makes brewhouse's download figure climb within about 4 seconds, and the chart shows it
- [ ] During that download, the port-563 rows show live rates, sort to the top, and their arcs thicken and speed up
- [ ] Download and upload aren't swapped: an upload test (e.g. copying a large file to a cloud drive) moves the upload figure
- [ ] A wired and a wireless client both show rates
- [ ] A dual-stack client's rate includes its IPv6 traffic
- [ ] Two detail pages open at once cause one top-talkers call per 2 seconds, not two
- [ ] With no detail page open, backend logs show no top-talkers calls
- [ ] Hiding the tab for 30 seconds and returning shows a break in the chart, not a straight line across the gap
- [ ] Stopping the OPNsense API mid-graph freezes the chart with the amber note, and it resumes when the API returns
- [ ] Unit tests pass

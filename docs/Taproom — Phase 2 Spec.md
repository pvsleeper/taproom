# Taproom — Phase 2 Spec

Sep 28, 2026 · @Pieter Venter

Phase 2 replaces phase 1's 30-second background poller with fetching on request plus a short cache. The API response shape, the client merger and the frontend stay as built in phase 1.

## Scope

Only the backend's data-refresh mechanism changes. Upstream APIs are called when someone asks for data, never on an idle timer.

**Changes**

- Remove `PollerService` and the startup poll.
- Replace `ClientSnapshotStore` with a cached provider that fetches both sources on demand.
- Replace the `PollIntervalSeconds` setting with `CacheSeconds`.
- `/api/health` stops depending on poll results.

**Stays the same**

- `OmadaClient` and `OpnsenseClient`, including Omada token caching
- `ClientMerger` and its unit tests
- The `/api/clients` response shape
- The whole frontend, including its 30-second refetch

**Trade-off accepted:** a stored-history feature in a later phase needs sampling while nobody has the page open, so it will bring back a background sampler of its own.

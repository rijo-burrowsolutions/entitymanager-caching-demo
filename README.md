# EntityManager Redis Caching — Runnable Demo

A working version of the caching design for ag-kit's EntityManager module — Cache-Aside + Stale-While-Revalidate, backed by Redis. **Read-only in shape** — no write/update endpoints exist for the real entities themselves, matching real production DB permissions. Every entity (`Agent`, `Office`, `Company`), its EF Core mapping, and every Application query is copied straight from the real `ag-kit` codebase, unmodified except for adding the caching opt-in (`ICacheableQuery`).

This repo is built around a **local sandbox** (a full read/write copy of real data on SQL Server LocalDB) so you can safely test real database writes triggering real invalidation, with zero risk to production.

## Folder structure (mirrors ag-kit)

```
EntityManager/
  Demo.slnx
  Host/
    EntityManager.Api/               <- entry point, Program.cs, appsettings.json
  Modules/
    EntityManager/
      EntityManager.Domain/          <- REAL Agent/Office/Company entities + repository interfaces
      EntityManager.Application/     <- REAL Queries (RawJson -> camelCase -> office/company join -> CDN picture), + caching opt-in
      EntityManager.Infrastructure/  <- REAL DbContext/EF configs/repositories + DI wiring
      EntityManager.Presentation/    <- Minimal API endpoints (GET only), internal cache-refresh endpoint, sandbox-only test-update endpoints
  Shared/
    Infrastructure/
      Ag.Cache/                     <- the caching library (CachingPipelineBehavior, cache keys) - new, doesn't exist in ag-kit yet
    BuildingBlocks/
      Ag.Abstractions/               <- copied from ag-kit - BaseEntity, PagedResult, pagination helpers
      Ag.Util/                       <- copied from ag-kit - camelCase JSON conversion, CDN default-picture helper
  Tools/
    SandboxSetup/                   <- one-time: copies real Agent/Office/Company data to a local LocalDB sandbox
    SandboxWatcher/                 <- watches the sandbox for changes + drains near-expiry refreshes
```

There is no `cache-worker/` (Node.js) folder in this repo — it was removed in favor of `Tools/SandboxWatcher`, a .NET equivalent that can actually reach SQL Server LocalDB (Node's `mssql` package cannot — LocalDB only speaks named pipes in a way its driver doesn't support).

## How caching works

`CachingPipelineBehavior` (in `Ag.Cache`) is a Mediator pipeline behavior registered once in `EntityManagerModuleServiceExtensions.cs`. It intercepts every request:

- Not implementing `ICacheableQuery`? Passes straight through, untouched.
- `BuildCacheKey()` returns `null` (e.g. missing `clientCode`)? Skips Redis entirely for that call — no read, no write. This is deliberate: an earlier version fell back to a placeholder key (`ety:default:...`) when `clientCode` was missing, which created cache entries nothing could ever reconstruct to invalidate. Returning `null` instead makes that failure mode structurally impossible.
- Cache HIT with more than `RefreshWindow` left? Return immediately.
- Cache HIT with less than `RefreshWindow` (2 min) left? Return immediately with the still-valid data, **and** push the key onto a Redis stream (`ety:refresh:queue`) for background refresh.
- Cache MISS? Run the real handler, store the result with the query's `Ttl`.

Cache keys are built by `CacheKeyBuilder.Build(clientCode, entity, operation, params)` → `ety:{CLIENTCODE}:{entity}:{operation}:{sorted params}`. `clientCode` is uppercased — SQL Server compares it case-insensitively, but a Redis key is just a string, so without this, `"vla"` and `"VLA"` would be two unrelated cache entries for the same real tenant.

## Two list-caching strategies, side by side

`/Agent/list` caches one big JSON blob per filter+page combination — fast and simple, but nothing can invalidate it precisely; a changed agent can keep showing stale data in list results until the whole entry's TTL expires.

`/Agent/list-fresh` (and its Office/Company equivalents) fixes this with an ID-list + per-record pattern:
1. `GetAgentIdListQuery` caches only the matching `AgentKey`s for a filter+page — a few bytes, not a full page of JSON.
2. For each ID, it sends `GetAgentQuery` — the exact same per-record query `/Agent/get` uses, with the exact same cache key the sandbox watcher invalidates precisely when that row changes.
3. The assembled page is never cached itself — all the speed comes from its two cached sub-queries.

**A real inconsistency this surfaced, not a bug in the pattern**: `GetAgentQuery`'s underlying repository call (`GetAgentDetail`) requires `IsDisplayedOnWebsite = true` and a resolvable Office+Company join. `GetAgentList` (backing plain `/Agent/list`) now applies the same two filters, so every ID a list ever returns is guaranteed to resolve via `/Agent/get` too — that wasn't always true; it originally only filtered `!IsDeleted`, which meant `list-fresh` could silently return fewer than `pageSize` items whenever a listed agent turned out to be excluded from individual lookup.

## All endpoints

| Endpoint | Purpose |
|---|---|
| `GET /Agent/get?agentKey=&seoName=&clientCode=` | Single agent, joined with office+company |
| `GET /Agent/list?...&pageNumber=&pageSize=` | Cached as one page-sized JSON blob |
| `GET /Agent/list-fresh?...&pageNumber=&pageSize=` | Same shape, but ID-list + per-record caching |
| `GET /Office/get?officeKey=&clientCode=` | Single office |
| `GET /Office/list?...&pageNumber=&pageSize=` | Cached as one page-sized JSON blob |
| `GET /Office/list-fresh?...&pageNumber=&pageSize=` | ID-list + per-record caching |
| `GET /Company/get?companyKey=&clientCode=` | Single company |
| `GET /Company/list?...&pageNumber=&pageSize=` | Cached as one page-sized JSON blob |
| `GET /Company/list-fresh?...&pageNumber=&pageSize=` | ID-list + per-record caching |
| `POST /internal/cache/refresh?key=...` | Internal only — rebuilds any of the 9 cacheable query types from its Redis key and refreshes it. Not meant to be called by hand. |
| `POST /Sandbox/agent/update?agentKey=&clientCode=&firstName=&lastName=&fullName=&emailAddress=` | **Sandbox only** (never mapped unless `UseSandboxDb: true`) — updates both the normalized columns and the matching keys in `RawJson`, case-insensitively, for testing writes without hand-written SQL |
| `POST /Sandbox/office/update?officeKey=&clientCode=&officeName=` | Sandbox only |
| `POST /Sandbox/company/update?companyKey=&clientCode=&companyName=` | Sandbox only |

No other write routes exist anywhere in this demo.

## Edge cases (verified)

| Input | Behavior |
|---|---|
| `/Agent/get` with neither `agentKey` nor `seoName` | `400 { "message": "Either AgentKey or SeoName is required" }` |
| `/Office/get?officeKey=0` or a negative key | `400` (same for Company) |
| `/Agent/get?agentKey=` for a key that doesn't exist, or is excluded by `IsDisplayedOnWebsite`/office-company join | `404`, empty body |
| `/Agent/list?fullName=` matching nothing | `200` with `{"items":[],"totalCount":0,...}` — never a 404 for "no results" |
| `pageSize` far above the max | Silently clamped (`PaginationExtension.MaxPageSize`) — no error |
| `pageNumber=0` or negative | Silently floored to 1 — no error |
| No `clientCode`/`clientID` on any cacheable query | Not cached at all for that call — real handler runs, no Redis read/write |
| Malformed `RawJson` in the database | `400 { "message": "Invalid JSON found in RawJson.", ... }` instead of an unhandled 500 |

## Prerequisites

- .NET 10 SDK
- A Redis server reachable at `127.0.0.1:6379`
- SQL Server LocalDB — check with `sqllocaldb info`, should list `MSSQLLocalDB`
- Network access to the real `idc_ety` SQL Server — **only needed once**, to run `SandboxSetup` and copy data locally; nothing else in this repo ever talks to real production

## Setup (one-time)

```
cd EntityManager
```

Set the real connection string as an environment variable — **never commit this anywhere**:
```powershell
$env:REAL_ETY_CONNECTION_STRING = "server=...;UID=...;PWD=...;database=idc_ety;Pooling=true;Min Pool Size=5;Max Pool Size=20;Connection Timeout=30;TrustServerCertificate=True;"
```

Then create the local sandbox:
```
dotnet run --project Tools/SandboxSetup
```

This creates `idc_ety_sandbox` on `(localdb)\MSSQLLocalDB` (schema built from the exact same EF Core entity model the API uses) and bulk-copies every real Agent/Office/Company row into it. **It only ever runs `SELECT` against real `idc_ety`** — nothing is written back. Safe to re-run any time you want a fresh copy (it wipes and re-copies each table).

## How to run

**Terminal 1 — the API:**
```
dotnet run --project Host/EntityManager.Api
```
Opens at **http://localhost:5080** — redirects `/` to Swagger UI. `appsettings.json` already has `"UseSandboxDb": true`, so this talks to your local sandbox by default.

**Terminal 2 — the sandbox watcher:**
```
dotnet run --project Tools/SandboxWatcher
```
Polls `idc_ety_sandbox` every 3 seconds for real changes, invalidates the matching Redis key, and immediately refills it via `/internal/cache/refresh` so the next reader gets a fast HIT instead of a MISS. It also drains `ety:refresh:queue` — the same near-expiry background-refresh mechanism real production would use, just running locally against the sandbox.

## Test scenarios

### 1. Find real data to test with
```
GET http://localhost:5080/Agent/list?pageSize=3&clientID=WRE
```
(swap `WRE` for whatever `clientCode` your sandbox data actually has) Note an `agentKey` and `clientCode` from the response.

### 2. Cache MISS then HIT
```
GET http://localhost:5080/Agent/get?agentKey={agentKey}&clientCode={clientCode}
```
Call it once — the API console prints `[cache] MISS ... [cache] STORE ...`. Call it again — `[cache] HIT ... (~9m59s left)`.

### 3. A real database write triggering real invalidation
Edit the row directly against `idc_ety_sandbox` (SSMS, Azure Data Studio, or `sqlcmd`) — the easiest way is the built-in test endpoint:
```
POST http://localhost:5080/Sandbox/agent/update?agentKey={agentKey}&clientCode={clientCode}&fullName=Test%20Name
```
Within ~3 seconds, `SandboxWatcher`'s console prints `invalidated ety:{CLIENTCODE}:agent:get:agentkey={agentKey}` followed by `refreshed ...`. Calling `/Agent/get` again shows the new data immediately — no manual MISS needed, since the watcher already refilled it.

### 4. `/Agent/list` vs `/Agent/list-fresh` after that same edit
Call `/Agent/list-fresh?...` for a page containing the agent you just edited — it reflects the change immediately (its per-record cache entry was precisely invalidated). Call `/Agent/list?...` for the same filter — it keeps showing the old data until that whole page's TTL expires, since list entries are never watcher-invalidated, only TTL-based.

### 5. Near-expiry background refresh
Call `/Agent/get` once, wait until it's within its last 2 minutes (TTL is 10 min), then call it again. The API console shows a HIT plus `near expiry - queuing background refresh`. Within a few seconds, `SandboxWatcher`'s console prints `near-expiry refresh for ... refreshed ...`. Check the key's TTL in Redis — it's back to a full 10 minutes without you calling anything else.

### 6. Multi-entity + multi-tenant check
Repeat the same cycle against `/Office/get`/`/Company/get`, and try the same agent/office/company under a *different* `clientCode` — confirm each tenant gets its own independent cache key and independent invalidation.

## Resetting the demo

Stop both terminals. To clear cached data: delete `ety:*` keys in Redis (or `FLUSHDB` if this Redis instance is only used for this demo). To reset the sandbox back to a fresh copy of real data: re-run `Tools/SandboxSetup` (wipes and re-copies), then clear Redis too — the watcher's checkpoints and any stale cache entries won't otherwise reconcile against reset data on their own.

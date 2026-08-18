# EntityManager Redis Caching — Runnable Demo

A working version of the caching design for ag-kit's EntityManager module — Cache-Aside + Stale-While-Revalidate, backed by Redis. Every entity (`Agent`, `Office`, `Company`), its EF Core mapping, and every Application query is copied straight from the real `ag-kit` codebase, unmodified except for adding the caching opt-in (`ICacheableQuery`). Presentation is file-per-endpoint, in per-entity folders, matching real ag-kit's actual layout exactly (down to filenames like `AgentQueryEndpoint.cs`/`MapAgentActionsEndpoint.cs`) — checked directly against the real `ag-kit` source, not assumed.

**Read-only against real production, always** — real ag-kit's EntityManager module has no update functionality at all (its one `Commands` file, `GetAgentCommand.cs`, is an empty placeholder). This demo adds real Update commands (`UpdateAgentCommand`, etc.) modeled on the Command/Handler convention used elsewhere in ag-kit (e.g. `ClientManager`'s `UpdateClientCommand`), but they are **sandbox-only** — never mapped at all unless `"UseSandboxDb": true`, so it's structurally impossible to reach them against real production, exactly like every other write path in this repo.

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
      EntityManager.Application/
        Queries/                     <- REAL Queries (RawJson -> camelCase -> office/company join -> CDN picture), + caching opt-in
        Commands/                    <- Update commands (sandbox-only) - Agent/, Office/, Company/, one file per command+handler
      EntityManager.Infrastructure/  <- REAL DbContext/EF configs/repositories (Get + Update) + DI wiring
      EntityManager.Presentation/
        Endpoints/
          Agent/                     <- one file per endpoint: AgentQueryEndpoint, AgentListFreshEndpoint,
                                         AgentSuggestEndpoint, AgentUpdateEndpoint, MapAgentActionsEndpoint (aggregator)
          Office/                    <- same shape as Agent/
          Company/                  <- same shape as Agent/
          InternalCacheEndpoints.cs  <- internal cache-refresh endpoint (demo-only, no ag-kit equivalent)
        Contracts/                   <- Update request DTOs (Agent/, Office/, Company/) - matches ag-kit's
                                         Presentation-Contracts convention (see ClientManager)
        EntityManagerEndpointsExtensions.cs  <- top-level route groups ("/Agent", "/Office", "/Company"),
                                                 gates each entity's Update endpoint behind UseSandboxDb
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

Cache keys are built by `CacheKeyBuilder.BuildFromObject(clientCode, entity, operation, this, ...excludeProperties)` → `ety:{CLIENTCODE}:{entity}:{operation}:{sorted params}`. This reflects over the query record's own public properties (excluding whichever property holds the tenant scope, plus anything declared on `ICacheableQuery` itself) instead of a hand-built dictionary — so a new property added to a query record is automatically part of its cache key, no `BuildCacheKey()` edit required. (`CacheKeyBuilder.Build(...)` — the older, manual-dictionary overload — still exists for reference but nothing in this demo uses it anymore.) `clientCode` is uppercased — SQL Server compares it case-insensitively, but a Redis key is just a string, so without this, `"vla"` and `"VLA"` would be two unrelated cache entries for the same real tenant.

## Why there's no plain `/Agent/list` endpoint

An earlier version of this demo had both a whole-page `/Agent/list` (one big cached JSON blob per filter+page) and `/Agent/list-fresh` (ID-list + per-record caching), side by side, specifically to demonstrate the difference. That whole-page endpoint has since been removed — it could never be invalidated precisely (a changed agent kept showing stale list results until the whole entry's TTL expired), and `list-fresh` gives the same response shape with exact invalidation, so there was no real reason to keep the weaker option around.

`/Agent/list-fresh` (and its Office/Company equivalents) works as an ID-list + per-record pattern:
1. `GetAgentIdListQuery` caches only the matching `AgentKey`s for a filter+page — a few bytes, not a full page of JSON.
2. For each ID, it sends `GetAgentQuery` — the exact same per-record query `/Agent/get` uses, with the exact same cache key the sandbox watcher invalidates precisely when that row changes.
3. The assembled page is never cached itself — all the speed comes from its two cached sub-queries.

The ID-list half is still only TTL-invalidated (see Test scenario 4 below for what that means in practice) — only the per-record half gets exact, watcher-driven invalidation.

**A real inconsistency this surfaced, not a bug in the pattern**: `GetAgentQuery`'s underlying repository call (`GetAgentDetail`) requires `IsDisplayedOnWebsite = true` and a resolvable Office+Company join. `GetAgentList` (the repository method backing the ID-list) now applies the same two filters, so every ID `list-fresh` ever returns is guaranteed to resolve via `/Agent/get` too — that wasn't always true; it originally only filtered `!IsDeleted`, which meant `list-fresh` could silently return fewer than `pageSize` items whenever a listed agent turned out to be excluded from individual lookup.

## All endpoints

| Endpoint | Purpose |
|---|---|
| `GET /Agent/get?agentKey=&seoName=&clientCode=&firstName=&lastName=&fullName=&email=` | Single agent, joined with office+company. Every extra filter supplied ANDs together (same convention as `agentKey`/`seoName`) — at least one of the six lookup fields is required |
| `GET /Agent/list-fresh?...&pageNumber=&pageSize=` | ID-list + per-record caching (see above) |
| `GET /Agent/suggest?name=&clientCode=&isTeam=&excludeKeys=&pageLimit=` | Typeahead over agent names — a differently-shaped cacheable Get for the same entity |
| `GET /Office/get?officeKey=&clientCode=&officeName=&city=&email=` | Single office — at least one of `officeKey`/`officeName`/`city`/`email` is required |
| `GET /Office/list-fresh?...&pageNumber=&pageSize=` | ID-list + per-record caching |
| `GET /Office/suggest?name=&clientCode=&excludeKeys=&pageLimit=` | Typeahead over office names |
| `GET /Company/get?companyKey=&clientCode=&companyName=&email=` | Single company — at least one of `companyKey`/`companyName`/`email` is required |
| `GET /Company/list-fresh?...&pageNumber=&pageSize=` | ID-list + per-record caching |
| `GET /Company/suggest?name=&clientCode=&excludeKeys=&pageLimit=` | Typeahead over company names |
| `POST /internal/cache/refresh?key=...` | Internal only — rebuilds any of the 9 cacheable query types from its Redis key and refreshes it. Not meant to be called by hand. |
| `PUT /Agent/update/{agentKey}?clientCode=` (body: `{ firstName, lastName, fullName, emailAddress }`) | **Sandbox only** (never mapped unless `UseSandboxDb: true`) — updates both the normalized columns and the matching keys in `RawJson`, case-insensitively, for testing writes without hand-written SQL |
| `PUT /Office/update/{officeKey}?clientCode=` (body: `{ officeName }`) | Sandbox only |
| `PUT /Company/update/{companyKey}?clientCode=` (body: `{ companyName }`) | Sandbox only |

No other write routes exist anywhere in this demo, and the three above don't exist in real ag-kit either — see the intro section. `suggest` results are cached the same way as everything else here (real ag-kit doesn't cache these today) — included as a second example of "a different-shaped Get API for the same entity," alongside `get`'s single-record lookup.

## Edge cases (verified)

| Input | Behavior |
|---|---|
| `/Agent/get` with none of `agentKey`/`seoName`/`firstName`/`lastName`/`fullName`/`email` | `400 { "message": "At least one of AgentKey, SeoName, FirstName, LastName, FullName or Email is required" }` |
| `/Office/get?officeKey=0` or a negative key | `400` (same for Company) |
| `/Agent/get?agentKey=` for a key that doesn't exist, or is excluded by `IsDisplayedOnWebsite`/office-company join | `404`, empty body |
| `/Agent/list-fresh?fullName=` matching nothing | `200` with `{"items":[],"totalCount":0,...}` — never a 404 for "no results" |
| `/Agent/suggest` (or Office/Company) with no `name` or no `clientCode` | `400 { "message": "Name is required" }` / `{ "message": "ClientCode is required" }` |
| `pageSize`/`pageLimit` far above the max | Silently clamped (`PaginationExtension.MaxPageSize` for lists, `SuggestionParams.MaxPageLimit` = 100 for suggest) — no error |
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
GET http://localhost:5080/Agent/list-fresh?pageSize=3&clientID=WRE
```
(swap `WRE` for whatever `clientCode` your sandbox data actually has) Note an `agentKey` and `clientCode` from the response.

### 2. Cache MISS then HIT
```
GET http://localhost:5080/Agent/get?agentKey={agentKey}&clientCode={clientCode}
```
Call it once — the API console prints `[cache] MISS ... [cache] STORE ...`. Call it again — `[cache] HIT ... (~9m59s left)`.

### 3. A real database write triggering real invalidation
Edit the row directly against `idc_ety_sandbox` (SSMS, Azure Data Studio, or `sqlcmd`) — the easiest way is the built-in Update endpoint (sandbox-only, real ag-kit Command/Handler shape — see the intro section):
```
PUT http://localhost:5080/Agent/update/{agentKey}?clientCode={clientCode}
Content-Type: application/json

{ "fullName": "Test Name" }
```
Within ~3 seconds, `SandboxWatcher`'s console prints `invalidated ety:{CLIENTCODE}:agent:get:agentkey={agentKey}` followed by `refreshed ...`. Calling `/Agent/get` again shows the new data immediately — no manual MISS needed, since the watcher already refilled it.

### 4. `list-fresh`'s two halves are invalidated differently
Call `/Agent/list-fresh?...` for a page containing the agent you just edited — the **content** (e.g. the new `fullName`) shows up immediately, because that agent's per-record cache entry was precisely invalidated by the watcher. But if instead you change something that affects *whether the agent matches the filter at all* (e.g. edit `fullName` so it no longer contains the `fullName` filter text you searched for), the agent keeps appearing in that `list-fresh` result until the ID-list's own TTL expires — the ID-list half of the pattern is TTL-only, same as the old whole-page `/Agent/list` was, since nothing watches for "does this row still match this filter."

### 5. Near-expiry background refresh
Call `/Agent/get` once, wait until it's within its last 2 minutes (TTL is 10 min), then call it again. The API console shows a HIT plus `near expiry - queuing background refresh`. Within a few seconds, `SandboxWatcher`'s console prints `near-expiry refresh for ... refreshed ...`. Check the key's TTL in Redis — it's back to a full 10 minutes without you calling anything else.

### 6. Multi-entity + multi-tenant check
Repeat the same cycle against `/Office/get`/`/Company/get`, and try the same agent/office/company under a *different* `clientCode` — confirm each tenant gets its own independent cache key and independent invalidation.

### 7. A differently-shaped Get API caches the same way
```
GET http://localhost:5080/Agent/suggest?name=jo&clientCode={clientCode}
```
Call it twice — same MISS-then-HIT behavior as `/Agent/get`, even though `suggest` has entirely different parameters (`name`/`isTeam`/`excludeKeys`/`pageLimit` instead of `agentKey`/`seoName`). This is the point of the reflection-based `BuildCacheKey()`: the caching mechanism didn't need to know anything specific about `suggest` to work correctly for it.

### 8. Alternate single-record lookups on `/Agent/get` (and Office/Company)
```
GET http://localhost:5080/Agent/get?email={emailAddress}&clientCode={clientCode}
```
Works the same as looking up by `agentKey` — MISS then HIT, watcher-invalidated the same way — even though no `agentKey` was ever supplied. Try `firstName`/`lastName`/`fullName` too, and combine two at once (e.g. `firstName` + `lastName`) to see them AND together, same convention as `agentKey`+`seoName`. `/Office/get?officeName=`/`city=`/`email=` and `/Company/get?companyName=`/`email=` work the same way.

## Resetting the demo

Stop both terminals. To clear cached data: delete `ety:*` keys in Redis (or `FLUSHDB` if this Redis instance is only used for this demo). To reset the sandbox back to a fresh copy of real data: re-run `Tools/SandboxSetup` (wipes and re-copies), then clear Redis too — the watcher's checkpoints and any stale cache entries won't otherwise reconcile against reset data on their own.

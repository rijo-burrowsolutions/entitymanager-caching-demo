# EntityManager Redis Caching — Runnable Demo

A working version of the caching design from `ag-kit/docs/redis/`, running against the **real `idc_ety` SQL Server database** — not a stand-in. **Read-only** — no write/update endpoints exist anywhere in this demo, matching real production DB permissions. Every entity (`Agent`, `Office`, `Company`), its EF Core mapping, and every Application query is copied straight from the real `ag-kit` codebase, unmodified except for adding the caching opt-in (`ICacheableQuery`).

## Folder structure (mirrors ag-kit)

```
demo/
  EntityManager/                     <- the .NET solution (mirrors ag-kit's root layout)
    Demo.slnx
    Host/
      EntityManager.Api/               <- entry point, Program.cs, appsettings.json (real IDC_ETY connection string)
    Modules/
      EntityManager/
        EntityManager.Domain/         <- REAL Agent/Office/Company entities + repository interfaces
        EntityManager.Application/    <- REAL Queries (RawJson -> camelCase -> office/company join -> CDN picture), + caching opt-in
        EntityManager.Infrastructure/ <- REAL DbContext/EF configs/repositories, now pointed at SQL Server, + DI wiring
        EntityManager.Presentation/   <- Minimal API endpoints (GET only) + one internal cache-refresh endpoint
    Shared/
      Infrastructure/
        Ag.Cache/                     <- the caching library (CachingPipelineBehavior, cache keys) - new, doesn't exist in ag-kit yet
      BuildingBlocks/
        Ag.Abstractions/              <- copied from ag-kit - BaseEntity, PagedResult, pagination helpers
        Ag.Util/                      <- copied from ag-kit - camelCase JSON conversion, CDN default-picture helper
  cache-worker/                     <- Node.js background worker (separate from the .sln)
```

## What's different from the real design (and why)

| Real design | This demo | Why |
|---|---|---|
| 60 min TTL for "get" / 10 min TTL for "list" (10 min / 2 min refresh windows) | **Flat 10 min TTL / 2 min refresh window for every cacheable query** | Simpler to reason about locally than two different tiers - happens to match real production's "list" TTL exactly, and is shorter than production's 60 min "get" TTL |
| SQL Server Change Tracking | **Polling the `LASTMODIFIED` column** | Change Tracking isn't enabled on `idc_ety` today; polling the same column the real entities already map (`AgentConfiguration.cs` etc.) gets the same result |
| No write/update endpoint | **None at all** | Matches your real DB permissions — read-only, GET-only, no CRUD |
| Refresh worker rebuilds the DTO itself | **Calls back into the .NET API** (`POST /internal/cache/refresh`) | The real response shape comes from RawJson + camelCase conversion + a 3-way join + a CDN picture default - reimplementing that in JavaScript would be a second copy of real business logic that could silently drift from the C# original. The worker just triggers the *same* real handler instead. |

Everything else — the real `Agent`/`Office`/`Company` entities and their EF mappings, the real `GetAgentQuery`/`GetAgentListQuery`/etc. handlers, `Ag.Cache`'s caching pipeline, the cache key format — is the real design (or copied verbatim from ag-kit), unmodified.

## Architecture: why the refresh worker calls back into the API

`CachingPipelineBehavior.cs` queues a background refresh whenever a cache hit is close to expiring (see `RefreshWindow`). The original prototype had `refreshWorker.js` query the database directly and hand-build a small DTO. That worked when queries returned clean typed fields. The real queries don't — they return a `RawJson` blob run through `CamelCaseConversion` and, for Agent, joined against Office and Company. To keep that logic in exactly one place, `EntityManager.Presentation/Endpoints/InternalCacheEndpoints.cs` exposes `POST /internal/cache/refresh?key=...`, which:

1. Parses the Redis key back into `{clientCode, entity, param, value}`.
2. Calls the real query handler directly (`GetAgentQueryHandler.Handle(...)`, etc.) — bypassing `CachingPipelineBehavior`'s cache check on purpose, since a refresh must always re-read.
3. Writes the fresh result back into Redis under the same key, using the query's own `.Ttl` — so the TTL never needs to be duplicated/kept in sync anywhere else.

`refreshWorker.js` now only does Redis Streams consumer-group plumbing (read the queue, lock, ack) and one HTTP call — zero duplicated business logic.

## `/Agent/list` vs `/Agent/list-fresh` — two different caching strategies, side by side

`/Agent/list` caches one big JSON blob per filter+page combination. It's fast and simple, but `watcher.js` can't invalidate it precisely — it only knows "AgentKey 6721 changed," not "which cached list pages currently contain agent 6721 somewhere in their results." So an updated agent can keep showing its old data in list results for up to that cache entry's full TTL (10 min), even though `/Agent/get` for that exact agent is already fresh.

`/Agent/list-fresh` (`GetAgentListFreshQuery.cs`) fixes this with the ID-list + per-record pattern Ajay suggested. The same pattern is implemented identically for `/Office/list-fresh` (`GetOfficeListFreshQuery.cs`) and `/Company/list-fresh` (`GetCompanyListFreshQuery.cs`):

1. **`GetAgentIdListQuery`** caches only the matching `AgentKey`s for a filter+page — a few bytes, not a full page of JSON. Even if this is briefly stale, the worst case is a newly added/removed agent showing up a little late — never wrong data for an agent still on the page.
2. For each ID, it sends a **`GetAgentQuery`** — the exact same per-record query `/Agent/get` uses, with the exact same cache key `watcher.js` already invalidates precisely when that one row's `LASTMODIFIED` changes.
3. The assembled page is **not itself cached** — caching the composed result again would just reintroduce the same staleness problem one level up. All of its speed comes from its two cached sub-queries; only a genuinely-changed record ever re-fetches.

Net effect: if one agent on the page was just updated, every *other* agent stays a fast cache hit, and only that one agent does a fresh read — no stale data survives, without needing to invalidate (or even identify) the whole page. The tradeoff is more Redis round-trips per request (one per agent on the page, done sequentially — see the comment in `GetAgentListFreshQueryHandler.cs` on why it can't parallelize these against the same EF Core `DbContext`), so it favors correctness over raw speed on a cache-cold page.

One real quirk worth knowing before comparing the two: `GetAgentQuery` (used by both `/Agent/get` and `/Agent/list-fresh`) requires `IsDisplayedOnWebsite = true` and a valid Office+Company join, while `GetAgentListQuery` (used by plain `/Agent/list`) doesn't filter on either. This is pre-existing real ag-kit behavior, not something this demo introduced — so `/Agent/list-fresh` can legitimately return *fewer* rows than `/Agent/list` for the same filter (any agent not displayed on the website, or missing a valid office/company link, silently drops out of the "fresh" version). Don't mistake that for a bug in the pattern itself.

## One correction this demo surfaced

Building this for real caught a mistake in the documented code: the actual `Mediator.Abstractions` package's `IPipelineBehavior<TRequest,TResponse>.Handle(...)` signature is:

```csharp
Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
```

`next` comes **before** `cancellationToken` — the opposite order shown in the design PDFs. This demo's `CachingPipelineBehavior.cs` uses the correct (compiler-verified) order. The PDFs should be corrected to match.

## All endpoints (GET only)

| Endpoint | Purpose |
|---|---|
| `GET /Agent/get?agentKey=&seoName=&clientCode=` | Single agent, joined with office+company |
| `GET /Agent/list?...&pageNumber=&pageSize=` | Cached as one page-sized JSON blob |
| `GET /Agent/list-fresh?...&pageNumber=&pageSize=` | Same shape, but ID-list + per-record caching (see above) |
| `GET /Office/get?officeKey=&clientCode=` | Single office |
| `GET /Office/list?...&pageNumber=&pageSize=` | Cached as one page-sized JSON blob |
| `GET /Office/list-fresh?...&pageNumber=&pageSize=` | ID-list + per-record caching |
| `GET /Company/get?companyKey=&clientCode=` | Single company |
| `GET /Company/list?...&pageNumber=&pageSize=` | Cached as one page-sized JSON blob |
| `GET /Company/list-fresh?...&pageNumber=&pageSize=` | ID-list + per-record caching |
| `POST /internal/cache/refresh?key=...` | Internal only - see "why the refresh worker calls back into the API" above. Not meant to be called by hand. |

No POST/PUT/DELETE routes exist anywhere in this demo.

## Edge cases (verified against real data)

| Input | Behavior |
|---|---|
| `/Agent/get` with neither `agentKey` nor `seoName` | `400 { "message": "Either AgentKey or SeoName is required" }` |
| `/Office/get?officeKey=0` or a negative key | `400 { "message": "OfficeKey is required" }` (same for Company) |
| `/Company/get` with no `companyKey` at all | `400` (ASP.NET Core's own model-binding rejects a missing required `int`) |
| `/Agent/get?agentKey=` for a key that doesn't exist | `404`, empty body |
| `/Agent/list?fullName=` matching nothing | `200` with `{"items":[],"totalCount":0,...}` — never a 404 for "no results" |
| `pageSize` far above the max (e.g. `999999`) | Silently clamped to 100 (`PaginationExtension.MaxPageSize`) — no error |
| `pageNumber=0` or negative | Silently floored to 1 — no error |
| `pageSize=0` or negative | Silently falls back to the default page size — no error |
| Malformed `RawJson` in the database | `400 { "message": "Invalid JSON found in RawJson.", "details": "..." }` instead of an unhandled 500 |

Anything not covered above (a genuinely unexpected failure, e.g. the database being unreachable) still surfaces as a `500` — this demo doesn't swallow real errors, it only turns the *expected* edge cases above into clean, predictable responses.

## Prerequisites

- .NET 10 SDK
- Node.js 22+
- A Redis server reachable at `127.0.0.1:6379`
- Network access to the real `idc_ety` SQL Server (internal, not listed here) — same connection string ag-kit itself uses
- Optional, for Sandbox mode only (see below): SQL Server LocalDB — usually already installed alongside Visual Studio/SSMS

## How to run

**Terminal 1 — the API:**
```
cd EntityManager/Host/EntityManager.Api
dotnet run
```
Opens at **http://localhost:5080** — redirects `/` to Swagger UI.

**Terminal 2 — the cache worker:**
```
cd cache-worker
copy .env.example .env
npm install
npm start
```

## Test scenarios

This demo talks to real production data, so there's no fixed seed table — grab real keys from a `/list` call first.

### 1. Find a real Agent/Office/Company to test with
```
GET http://localhost:5080/Agent/list?pageSize=3
GET http://localhost:5080/Office/list?pageSize=3
GET http://localhost:5080/Company/list?pageSize=3
```
Note an `agentKey`/`officeKey`/`companyKey` and its `clientCode` from the response for the tests below.

### 2. Cache MISS then HIT
```
GET http://localhost:5080/Agent/get?agentKey={agentKey}&clientCode={clientCode}
```
Call it once — the API console prints `[cache] MISS ... [cache] STORE ...`. Call it again immediately — it prints `[cache] HIT ... (~9m59s left)`. No database query the second time.

### 3. Near-expiry background refresh
Call the same request once, then **wait until it's within its last 2 minutes** (TTL is 10 min, refresh window is the last 2 min - so wait about 8 minutes), then call it again. The API console shows `HIT ... (~1m59s left) ... near expiry - queuing background refresh`. Within a few seconds, the cache-worker terminal prints `[refreshWorker] refreshed ety:{clientCode}:agent:get:agentkey={agentKey}`. Call the endpoint again — the API console shows a HIT with close to a full 10 minutes remaining again. (If 8 minutes is too long to wait, you can temporarily lower `Ttl`/`RefreshWindow` in `GetAgentQuery.cs` back down for a quick check, then rebuild.)

### 4. List endpoint + cache key uniqueness
```
GET http://localhost:5080/Agent/list?fullName=john&pageNumber=1&pageSize=20
```
Call it twice to see the list MISS then HIT. Try changing `pageSize` or `fullName` and notice each distinct combination gets its own separate cache key (visible in the console's key strings).

### 5. ID-list + per-record caching (`/Agent/list-fresh`) — no stale data after an update
```
GET http://localhost:5080/Agent/list-fresh?fullName=jacobson&pageSize=5
```
Call it once — the console shows one `idlist` MISS/STORE plus one `get` MISS/STORE **per agent on the page**. Call it again — everything is a HIT. Now simulate `watcher.js` catching a real update by deleting just one agent's per-record key directly in Redis (swap in a real `agentKey` from the response):
```
redis-cli DEL ety:default:agent:get:agentkey={agentKey}
```
Call `/Agent/list-fresh` again — only that one agent shows `MISS`/`STORE` in the console; every other agent on the page stays a `HIT`. Compare that against doing the same thing to `/Agent/list`'s cache key — there, the *entire page* would have to be deleted (or wait out its full TTL) to see fresh data, because the whole page is one cache entry.

`/Office/list-fresh` and `/Company/list-fresh` work identically — same test, just swap the entity and its key column (`officekey=`/`companykey=`).

### 6. Multi-entity check
Repeat the same MISS/HIT/refresh cycle against:
```
GET http://localhost:5080/Office/get?officeKey={officeKey}&clientCode={clientCode}
GET http://localhost:5080/Company/get?companyKey={companyKey}&clientCode={clientCode}
```

### 7. DB-change invalidation (watcher.js)
Since this demo has no write endpoint, there's no in-app way to change a row. `watcher.js` polls `LASTMODIFIED` on `Agent`/`Office`/`Company` every 3 seconds regardless — if a real edit happens elsewhere (e.g. someone updates their profile through the real app) while a cached entry for that row exists, you'll see `[watcher] N change(s) in Agent` / `[watcher] invalidated ety:...` in the cache-worker console, and the next request for that key will be a MISS.

## Sandbox mode — a fully local, full read/write copy for testing real writes

Everything above is read-only against real `idc_ety`, which means the write→invalidate cycle can only ever be *simulated* (manually deleting a Redis key). To test it for real — a genuine `UPDATE` triggering real detection and invalidation — this demo also has a local sandbox: a full copy of the real Agent/Office/Company data on **SQL Server LocalDB**, which you can freely read and write with zero risk to production.

**Why LocalDB:** it's typically already installed alongside Visual Studio/SSMS (nothing new to install), .NET's `SqlClient` has native support for it, and both SSMS and Azure Data Studio can connect to it like any other SQL Server instance.

**A real limitation to know about:** Node's `mssql` package (used by the real cache-worker) can't connect to LocalDB — LocalDB only speaks named pipes, and Node's driver doesn't support that the way .NET's `SqlClient` does. So the sandbox uses a small **C# equivalent** of `watcher.js` (`Tools/SandboxWatcher`) instead — same polling logic, same `CacheKeyBuilder` (referenced directly, not reimplemented), just able to actually reach LocalDB. The real Node cache-worker is untouched and still targets real production as designed.

### Setting it up

```
cd EntityManager
dotnet run --project Tools/SandboxSetup
```

This creates `idc_ety_sandbox` on `(localdb)\MSSQLLocalDB` (schema built from the exact same EF Core entity model the API uses — guaranteed to match) and bulk-copies every real Agent/Office/Company row into it. **It only ever runs `SELECT` against real `idc_ety`** — nothing is written back, and nothing about the real database is touched or modified. Safe to re-run any time you want a fresh copy.

### Connecting a GUI tool to browse/edit it

Both SSMS and Azure Data Studio work with zero extra configuration:
- **Server name:** `(localdb)\MSSQLLocalDB`
- **Authentication:** Windows Authentication (LocalDB only supports your own Windows login)
- Expand **Databases → idc_ety_sandbox → Tables** to browse or edit `Agent`/`Office`/`Company` directly.

### Pointing the API at the sandbox instead of real production

Set `"UseSandboxDb": true` in `appsettings.json` (or pass it as an environment variable when launching: `UseSandboxDb=true dotnet run`). Leave it `false` (the default) to keep using real `idc_ety` as before.

### Testing a real write end-to-end

```
Terminal 1: UseSandboxDb=true dotnet run --project Host/EntityManager.Api
Terminal 2: dotnet run --project Tools/SandboxWatcher
```
1. `GET /Agent/get?agentKey={agentKey}&clientCode={clientCode}` — MISS then STORE, served from the sandbox.
2. Make a real edit directly in the sandbox — via SSMS, Azure Data Studio, or `sqlcmd`:
   ```
   sqlcmd -S "(localdb)\MSSQLLocalDB" -d idc_ety_sandbox -Q "UPDATE Agent SET LASTMODIFIED = SYSUTCDATETIME() WHERE AGENTKEY = {agentKey}"
   ```
3. Within ~3 seconds, `SandboxWatcher`'s console prints `N change(s) in Agent` / `invalidated ety:...`.
4. Call the same `/Agent/get` again — it's a genuine MISS/STORE, triggered by a real write, not a simulated one.

<details><summary>A real bug this surfaced, worth knowing if you write similar polling code</summary>

The first version of `SandboxWatcher` kept re-detecting the same changed row on every single poll tick, forever. Root cause: `SqlCommand.Parameters.AddWithValue(...)` with a plain .NET `DateTime` defaults the parameter's SQL type to legacy `datetime` (≈3.33ms rounding precision), not `datetime2` (100ns precision, what the `LASTMODIFIED` column actually is). The rounded checkpoint parameter ended up sitting just *before* the true stored timestamp, so `WHERE LASTMODIFIED > @checkpoint` kept matching the same row indefinitely. Fixed by explicitly typing the parameter as `SqlDbType.DateTime2`. Lesson: when comparing against a `datetime2` column, always type the parameter explicitly — never let `AddWithValue` infer it.
</details>

**One thing this test won't visibly show:** the API's response text comes from a separate `RawJson` column, not from columns like `FullName` directly. Editing `FullName` (or just bumping `LASTMODIFIED`) correctly triggers invalidation and a fresh re-fetch — but the *displayed* content only changes if you also edit `RawJson` itself. That's expected, and matches real production: invalidation reacts to "this row changed," not to "the rendered output changed."

## Resetting the demo

Stop both terminals, and run `FLUSHALL` against your Redis instance (or just let old demo keys expire naturally — they all carry a 10 min TTL now, so give it a few minutes). There's no local database file to delete anymore — this demo doesn't own any data, it only reads real `idc_ety`.

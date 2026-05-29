# Report Fast Pagination Task

Updated: 2026-05-29

## Goal

Make report page loads fast by avoiding exact `COUNT` queries during normal report pagination.

The report API should fetch one page plus one extra row, use the extra row to determine whether a next page exists, and only calculate the exact total count when explicitly requested.

## Why This Is Needed

The current shared paginator runs `Count()` / `CountAsync()` before `Skip()` / `Take()`. For heavy report queries this makes a 10-row page wait for a full report scan.

Measured example from `TradeNetDBTest`:

- HSCode report page rows: about `0.1s - 0.6s`.
- HSCode report exact count for one month: about `50s`.

## Task Checklist

- [x] Create detailed task record before implementation.
- [x] Add an explicit `IncludeTotalCount` request flag.
- [x] Add fast report pagination that fetches `pageSize + 1`.
- [x] Use the extra row to calculate `HasNextPage`.
- [x] Return an estimated `TotalCount` when exact total count is not requested.
- [x] Preserve exact-count behavior for non-report API pagination.
- [x] Remove report API dynamic sort from normal page loads.
- [x] Use EF async directly when the query provider supports it.
- [x] Keep a safe synchronous fallback for in-memory/non-EF `IQueryable` report fragments.
- [x] Build backend.
- [x] Build frontend if frontend types/request shape change.
- [x] Update this task record with final status and verification.

## Implementation Notes For LLM

Use `Backend/Service/Reports/ReportQueryService.cs` as the report-only entry point. Do not change every report controller.

Use `Backend/Model/APIResult.cs` for shared response construction, but keep `CreateAsync` exact-count behavior for existing CRUD/list endpoints.

Preferred API behavior:

- `IncludeTotalCount = false`:
  - apply filter
  - apply no API-level dynamic sort for report pages
  - `Skip(pageIndex * pageSize)`
  - `Take(pageSize + 1)`
  - if extra row exists, `HasNextPage = true`
  - return only `pageSize` rows
  - return estimated total count as `pageIndex * pageSize + data.Count + (hasNext ? 1 : 0)`

- `IncludeTotalCount = true`:
  - use existing exact-count pagination behavior
  - useful for future UI actions that explicitly need exact totals

## Verification

- Backend: `dotnet build Backend/API.csproj -o artifacts/build-check/api`
- Frontend: `npm run build`

## Status

Completed.

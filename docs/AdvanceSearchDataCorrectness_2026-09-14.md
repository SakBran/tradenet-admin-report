# Advance Search — "the data does not match what I searched" (2026-09-14)

Follow-up to [AdvanceSearchPort_2026-09-14.md](AdvanceSearchPort_2026-09-14.md), which this
supersedes wherever the two disagree about current behaviour.

## The complaint

> Advance Search ကထွက်လာတဲ့ Data တွေကမမှန်ဘူး … ၂၀၂၆ ကိုရှာတာ ၂၀၂၅ data တွေပါ ပါလာပါတယ် …
> ကိုယ်မရွေးထားတဲ့ ရက်စွဲတွေပါ ပါနေတာပါ … Old report မှာက data က မထွက်တာလဲရှိပါတယ် …
> Old report မှာလဲ data ထွက်တာ မမှန်ပါဘူး

Four things, in the customer's order: a 2026 search returns 2025 data; dates they did not select
appear; sometimes nothing comes out at all; and **the old report is wrong too**.

That last clause is the licence for everything below. The usual rule on this project is bug-for-bug
parity with Tradenet 2.0 (`CLAUDE.md`), and the port deliberately kept several legacy defects on
that basis. The customer has now said the legacy behaviour is itself the problem and asked for
correctness and clarity instead — **for the Advance Search reports only**.

## Root cause of "2026 returns 2025"

The range filtered `IssuedDate`. The grid showed `LicenceDate` and `Last Date`. `IssuedDate` was
never projected at all, so the filtered column was invisible.

`ApplyType` is a column on the same row (New / Amend / Actual Amend / Extension / Cancel), and an
amended or extended licence keeps its **original** `LicenceDate` while carrying the **later**
`IssuedDate`. A licence dated 2025 and re-issued in 2026 therefore answered a 2026 search and
displayed 2025. Every visible date was outside the chosen range, by construction.

It was inherited, not introduced by the port: `tradenet-2.0-api/API/Business/AdvanceSearchRepository.cs:108-114`
filters `IssuedDate`, `:190` projects `LicenceDate`. Which is exactly why the old report was wrong too.

There was a second, wider problem with `IssuedDate`. Across `Backend/StoredProcedureToLinq/` there
are ~20 range predicates on `LicenceDate` — `sp_HSCodeReport` and every comparable listing report —
and only Advance Search and `sp_MemberRegistrationReport` used `IssuedDate`. So the same period gave
different rows in Advance Search than anywhere else in the app, and Advance Search was the odd one out.

## What changed

**The date range binds `LicenceDate`** (all eight branches, `sp_AdvanceSearch.cs`), so the searched
column is the displayed one and the report agrees with the rest of the app. `IssuedDate` is now
projected and shown beside it, because the difference between the two is the honest answer to
"why is this row here".

**The range is normalised server-side** to `[From.Date, To.Date + 1 day)`. The grid already widened
its To edge to 23:59:59, but the Excel endpoint and any direct caller posting a bare date used to
lose the whole last day.

**Dates print `DD/MM/YYYY` throughout, picker included.** The grid printed `MM/DD/YYYY HH:mm:ss`
(the legacy C# format) while the antd picker printed its default `YYYY-MM-DD`, so 3 February showed
as `02/03/2026` beside a box reading `2026-02-03` — read here as 2 March. New optional
`ReportFilterConfig.displayFormat`, set on Advance Search only; every other report is untouched.

**A subtitle echoes the applied range** (`Licence Date (03/02/2026) To (28/02/2026)`), and the
filter labels name the column. Nothing on the old screen said which range or which column produced
the rows on screen.

**The range opens on the current month.** It opened on today→today (`defaultDateRangeMonths: 0`,
faithful to the legacy `DateTime.Now` seeding), so the first search covered one day and usually came
back empty — a large part of "sometimes no data comes out".

### The four filters that silently matched nothing

The rest of "sometimes no data comes out". All were faithful ports of legacy defects.

| Box | Was | Now |
| --- | --- | --- |
| **Office (Sakhan)**, 4 Border screens | Filtered nothing at all — the legacy repository never referenced its own `data.Office` | Filters `SakhanId`. `Office` was not even on `sp_AdvanceSearchRequest`; it is now, and the four Border controllers pass it |
| **Country of Origin / Consigned Country** | Compared the whole picked string to the whole stored column. Even a *single* selection missed any row carrying more than one country; more than one selection on the two `int`-column types (Export Licence, Border Export Licence) resolved to `NoMatchId` and matched nothing | "Any of the selected". `IN (…)` on the two `int` columns; a delimiter-safe `OR` chain on the six comma-joined ones |
| **Mode of Transport** | "Carries *exactly* these modes, in any order" — the selection was permuted and the comma-joined column matched whole | "Carries any of them" |
| **Port Of Discharge** | Exact full-string match | Contains |

The comma-joined matching is delimiter-safe — `(',' + column + ',')` against `',5,'` — so id 5 never
matches a stored 15 or 52. `SplitCountry`/`ModeCombinations` are replaced by `SplitCountryIds`/`SplitModes`;
a selection that parses to no id at all still matches nothing rather than widening to "all".

**Row counts move**, on every one of these. That is the point, and it was signed off.

## Kept

Every join stays INNER, as the legacy query had it: a licence with no item lines, or an unresolvable
unit / currency / PaThaKa / Sakhan, still does not appear. This is load-bearing for any row-count
comparison against the old screen.

## Excel

`[ExcelFormatVersion(2)]` on all eight controllers, and the eight
`Backend.Tests/Fixtures/ExcelSpecs/AdvanceSearch*.json` fixtures updated. Both the layout and *which
rows an unchanged request returns* have changed, so without the bump the queue would keep serving
cached closed-period files built by the old code.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build` (API + tests) | 0 errors; no new warnings in the Advance Search files |
| `AdvanceSearchQueryTranslationTests` + `AdvanceSearchContractTests` | **119 passed.** This is the gate that matters: the `OR` chain, the `IN` list and the `LIKE` are built as expression trees, so nothing about them is compile-checked. `ToQueryString()` runs EF Core's whole translation pipeline **without opening a connection**, proving all eight branches still translate |
| `reportConfigs.advanceSearch.test.ts` + `multiSelectFilter.test.ts` | 57 passed |
| Backend suite (DB-skip filter) / `npx vitest run` / `npm run build` | see the session log; baselines are the 7 and 6 known failures |
| **Live data** | **not run.** Minting the `reportapi` JWT was blocked by a local permission rule, and the DB is CGNAT-internal |

## Still owed

1. **Row counts against the live API**, before and after, for the reply to the customer. The
   harness is in `[[report-api-jwt-harness]]`; query 2025, not 2026.
2. **`LicenceDate IS NULL` counts** on all eight tables. The column is nullable and `>= @From` is
   UNKNOWN for NULL, so a row with no Licence Date cannot be returned in any range. The customer's
   instruction was to measure before deciding whether to fall back to the other date.
3. **The grid's per-column date filter** (`Backend/Model/APIResult.cs:219-233`) uses a strict `>` on
   the low edge, dropping rows stored at exactly midnight, and `Convert.ToDateTime` throws on
   malformed input. A one-character fix, but shared by ~150 reports, so it needs its own go-ahead.
4. **The Excel button exports the live form values**, not the applied filters
   (`GenericReportPage.tsx:833-857`) — deliberate and app-wide, but it means the sheet can cover a
   different range than the grid if the boxes were edited without pressing Filter.
5. `initialSortColumn: 'Description'` is dead — `BasicTable` hardcodes `sortColumn: ''`. The
   ordering comes from the query itself, so the grid is correct, but the config line is misleading.

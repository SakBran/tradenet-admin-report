# Import Licence New — restore the date window — 2026-09-17

**Procedures only.** There is no application change in this release, and the auto-deploy
ships the application, never procedures. Nothing is fixed until these two are applied.

| Run order | Procedure | What it drives |
|---|---|---|
| `01` | `sp_NewReport_pagination` | Import Licence New Report — the grid + `TotalCount` |
| `02` | `sp_ImportLicenceListingCurrencyTotals` | the same report's footer (`Total No of License`, per-currency `Total Value`) |

Target database: **TradeNetDB** (not `ReportTemplateDB` — that one only holds the Excel
export job queue; deploying report procedures into it is a known trap).

---

## The complaint

> import licence company list report ကို ၁-၁-၂၀၂၅ မှ ၃၁-၁-၂၀၂၅ ထိ 101103471 ဖြင့်ရှာလျှင်
> total 34 စောင် ဖြစ်နေပါတယ်။ ဒါပေမဲ့ အဲ့ဒီကာလကို Import licence New report (New report) မှာ
> ဒီ Date နဲ့ ဒီ ပသကနဲ့ရှာလျှင် total 1385 စောင်ဖြစ်နေပါတယ်
> (စနစ်အစကတည်းက အခုရက်ထိကို ပြနေတာပါ)

Two reports, the same date range and the same ပသက, and the New report answers with every
licence since the system began. The customer's own diagnosis — *"Date က ပြဿနာ ဖြစ်နေတယ်"* —
is exactly right.

## Measured on the live PROD API before this release

`reportapi.myanmartradenet.com`, reg-no `101103471`, `2025-01-01 … 2025-01-31 23:59:59`:

| Query | `totalCount` | Dates returned |
|---|---|---|
| Import Licence **New**, with reg-no | **1386** | first rows `2021-01-07` — outside the window |
| Import Licence **New**, no reg-no | 5863 | all within Jan-2025 ✅ |
| Import Licence **Company List**, with reg-no | **34** (27 USD + 7 CNY) | ✅ |

The footer agreed with the broken grid rather than with the request: 1065 USD + 254 CNY +
… instead of 27 + 7.

So the date window works perfectly on its own, and is switched off the moment a Company
Registration No is supplied.

## Why it was broken

On 2026-06-19 a *different* complaint — "ပသက ဖြင့်ရှာပါက data မထွက်တော့ပါ" (a reg-no search
shows no data) — was patched by making the date predicate conditional:

```sql
AND (@CompanyRegistrationNo<>'' OR (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate))
```

That was an over-correction, for two reasons:

1. **The proc was never the cause.** The investigation notes from that round record that the
   deployed procedure and the frontend were already correct for the literal June query. The
   real cause was the page defaulting the range to the *current month*, which holds no data.
2. **There was nothing to inherit the skip from.** Where a date-skip is legitimate — Director
   List — Tradenet 2.0 really did ship a separate, date-independent *"By Company Registration
   No"* report, so folding it behind a date window genuinely broke it. Import Licence New has
   no such twin: legacy `Business/Reports.cs` → `dbo.sp_NewReport` applies
   `CreatedDate >= @FromDate AND CreatedDate <= @ToDate` as a plain unconditional conjunct,
   with both dates marked `required` on the form. **That is the discriminator for any future
   audit of this pattern** — a date-skip is parity only where a date-independent legacy
   report existed.

The footer proc was then deliberately given the same skip so its total would match the grid,
which is why both have to be reverted in one release.

## What changed

`sp_NewReport_pagination` branches on `@FormType`, and every other form type has its own
explicit branch (`Import Permit`, `Export Permit`, `Export Licence`, and the four Border
variants). The catch-all `ELSE` is reached **only** by `@FormType = 'Import Licence'`, which
is hard-coded in one controller,
`Backend/Controllers/Report/ImportLicenceNewReportNewReportController.cs`. So the blast
radius is exactly one report.

In that branch, both the `COUNT` and the paged `SELECT` (they must always stay in lockstep,
or `TotalCount` stops matching the rows shown):

```sql
-- before
AND (@CompanyRegistrationNo<>'' OR (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate))
-- after
AND (ImportLicence.CreatedDate>=@FromDate AND ImportLicence.CreatedDate<=@ToDate)
```

and the identical revert in the `New` branch of `sp_ImportLicenceListingCurrencyTotals`.

Nothing else moved: the reg-no predicate keeps its `CASE WHEN @CompanyRegistrationNo=''`
no-op-when-blank form, and `OPTION (RECOMPILE)` stays.

## Verified before shipping

Both procedures were applied to a local SQL Server 2022 against a dataset built to the shape
of the complaint — one company with 3 licences inside Jan-2025 and 5 outside it (2021, 2022,
2023, 2026), plus a second company inside the window:

| | grid `TotalCount` | rows outside the window | footer total |
|---|---|---|---|
| before | 8 (dated 2021-01-07 → 2026-05-07) | **5** | 8 |
| after | **3** (dated 2025-01-05 → 2025-01-28) | **0** | 3 |
| after, no reg-no | 4 across 2 companies | 0 | — |

The generated dynamic SQL was also dumped and measured at 2,750 characters, well clear of
the `nvarchar(4000)` truncation cliff that silently broke this same procedure once before,
and it terminates correctly at its closing `OPTION (RECOMPILE);`.

## Expected effect on the complaint

Import Licence New, reg-no `101103471`, Jan-2025: **1386 → 34**, matching the Company List
report, with every row inside the requested window. The plain date browse (no reg-no) must
stay at 5863.

Company List reads the same licences at item grain and additionally requires
`ImportLicenceNo <> ''` and at least one item, so it can only ever be ≤ the New report. If
the two do not land on the same number, section 3 of `VerifyDeployment.sql` names the
licences responsible — explain the gap rather than accepting it.

## A note for the next reg-no complaint

If a reg-no lookup comes back empty after this release, **that is the page's default
current-month range, not this predicate.** The fix is to widen the date range, not to
reintroduce the skip. Reverting it a second time would reopen this ticket.

## Run order

1. `CaptureRollback.sql` — save the grid; it is the rollback artifact. It also reports
   whether the server still carries the date-skip, i.e. whether this release is a no-op
   there.
2. `00_RunAll.sql` (or `01_…` then `02_…`).
3. `VerifyDeployment.sql` — section 1 must read OK on both rows; sections 2, 4 and 5 must
   read PASS.

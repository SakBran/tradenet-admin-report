# 2026-09-25 — Company Profile "11 ministries" layout

One procedure. Ships with the frontend/backend change that re-lays-out PaThaKa →
Company Profile (grid and Excel) in the format the customer now sends to the other
11 ministries:

`No | Company's Name | Address | EIR No. & Date | Type of Organization | လုပ်ငန်းရည်ရွယ်ချက် | Capital | Board of Director (Name | NRC No.) | Title`

| Run order | File | Procedure |
|---|---|---|
| 1 | `01_sp_CompanyProfileReport_pagination.sql` | `dbo.sp_CompanyProfileReport_pagination` |

`00_RunAll.sql` applies the same procedure in one script, with the `USE [TradeNetDB]`
target and `SET QUOTED_IDENTIFIER ON` the repository's other releases use.

## Sequence

1. `CaptureRollback.sql` — save the current definition (the rollback artifact).
2. `00_RunAll.sql` (or `01_…`).
3. `VerifyDeployment.sql`.
4. **Then** deploy the application (merge to `main`).

**Deploy before the application.** The new application maps two new result columns,
`StartDate` and `CapitalCurrency`. Against the old procedure EF Core's `SqlQueryRaw`
throws on the missing columns, and Company Profile fails with HTTP 500 on every request,
grid and Excel alike.

## What changed

Phase 2 of the procedure (the page of companies expanded to one row per director):

- `PaThaKa.StartDate` — the EIR validity is printed as `StartDate to EndDate`
  (`1-8-2026 to 31-7-2031`). `IssuedDate` is not the same date: it differs from
  `StartDate` on ~16,000 renewed registrations.
- `currency.Code AS CapitalCurrency`, via `LEFT JOIN Currency ON Currency.Id = PaThaKa.CurrencyId`
  (nullable). The application prints the capital as `K-10000000` for kyat (or no
  currency) and `USD-50000` etc. otherwise.
- The directors of each company are ordered by `PaThaKaDirectors.SortOrder` (NULLs last,
  then `Id`), ascending whatever the report's sort direction. Before, they were ordered by
  their GUID and reversed with a DESC sort — PROD listed sample company 145327946 as
  LI ZHEN, DAW YIN THIRI HLAING, WANG XINGANG; the customer's sample reads
  WANG XINGANG, LI ZHEN, DAW YIN THIRI HLAING. `VerifyDeployment.sql` §2 checks that
  `SortOrder` really gives that order.

Untouched: the filters, Phase 1 (which companies make the page, and `TotalCount`), the
sort whitelist, and the legacy `dbo.sp_CompanyProfileReport`.

## Proven before shipping

Local SQL Server 2022 (`customs-sql` container), seeded with the customer's three
companies plus a USD company, a no-currency company, a NULL `SortOrder` director and a
company with no directors. Old and new procedure on the same seed: identical companies,
paging and `TotalCount` (5 — the director-less company still excluded) in every call
(default sort, one registration no, `CompanyName DESC`, page 2 of size 2, Excel/no
paging); the new one adds `StartDate`/`CapitalCurrency` (MMK / USD / NULL) and lists the
directors in `SortOrder`, NULL last.

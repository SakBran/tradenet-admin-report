# MPU + Account Summary Performance And Data Accuracy Checklist

Created: 2026-06-09

Scope:

- `MPUReport`
- `MPUReportV3`
- `AccountSummaryReport`

Old source of truth:

- `C:\Data_D\Projects\Tradenet\admin\tradenet-2.0-admin\TradenetAdmin`
- RDLC columns:
  - `ReportControl\MPUReport.rdlc`
  - `ReportControl\AccountSummaryReport.rdlc`
- Old filter forms:
  - `Views\Reports\MPUReport.cshtml`
  - `Views\Reports\MPUReport_V3.cshtml`
  - `Views\Reports\AccountSummaryReport.cshtml`
- Old data calls:
  - `Business\Reports.cs` calls `dbo.sp_MPUReport`
  - `Business\Reports.cs` calls `dbo.sp_MPUReport_V3`
  - `Business\Reports.cs` calls `dbo.sp_AccountSummaryReport`

New implementation:

- Frontend config: `Frontend\src\Report\config\reportConfigs.ts`
- Pages:
  - `Frontend\src\Report\Page\MPUReport.tsx`
  - `Frontend\src\Report\Page\MPUReportV3.tsx`
  - `Frontend\src\Report\Page\AccountSummaryReport.tsx`
- Controllers:
  - `Backend\Controllers\Report\MPUReportController.cs`
  - `Backend\Controllers\Report\MPUReportV3Controller.cs`
  - `Backend\Controllers\Report\AccountSummaryReportController.cs`
- New stored procedure paths:
  - `dbo.sp_MPUReport_pagination`
  - `dbo.sp_MPUReport_V3_pagination`
  - `dbo.sp_AccountSummaryReport_pagination`

## 1. Filter Parity Checklist

| Report | Old filters | New filters | Result | Notes |
| --- | --- | --- | --- | --- |
| `MPUReport` | `FromTime`, `ToTime`, `PaymentType`, `FormType` | `FromDate/ToDate`, `PaymentType`, `FormType` | PARTIAL | Same filter concepts, but old `PaymentType` has only `MPU` and `Citizen Pay`; new adds `All`. Old `FormType` is a dropdown from card types plus four manually-added border types; new `FormType` is free text. |
| `MPUReportV3` | `FromTime`, `ToTime`, `PaymentType`, `FormType` | `FromDate/ToDate`, `PaymentType`, `FormType` | PARTIAL | Same as `MPUReport`. New `PaymentType` value is `CitizenPay`; old visible value is `Citizen Pay`. The new V3 controller does not normalize payment type, but the deployed pagination SQL accepts `CitizenPay` aliases. |
| `AccountSummaryReport` | `FromDate`, `ToDate`, `SakhanId`, `FormType` | `FromDate/ToDate`, `SakhanId`, `FormType` | PARTIAL | Same filter concepts, but old `SakhanId` and `FormType` are dropdowns. New config uses numeric/text inputs unless generic lookup inference supplies a Sakhan lookup. `FormType` remains free text, so old dropdown values are not enforced. |

Required filter tests:

- Confirm date defaults match old behavior:
  - Old MPU default: yesterday `23:00` to today `23:00`.
  - Old Account Summary default: not set in the visible GET action excerpt; verify actual model defaults if needed.
- For MPU reports, test `PaymentType=MPU`.
- For MPU reports, test `PaymentType=Citizen Pay` old vs `CitizenPay` new.
- For MPU reports, test new `PaymentType=''` / `All` as an intentional new behavior or remove it for strict old parity.
- For all three reports, replace `FormType` free text with old-style dropdown options if strict UI parity is required.
- For Account Summary, ensure `SakhanId` renders as a dropdown sourced from `ReportLookups/sakhans`.

## 2. Column Parity Checklist

| Report | Old RDLC columns | New columns | Result | Diff |
| --- | --- | --- | --- | --- |
| `MPUReport` | 19 columns | 19 columns | FAIL | Old header is `MPU`; new header is `MPU Amount`. |
| `MPUReportV3` | 19 columns | 19 columns | FAIL | Old header is `MPU`; new header is `MPU Amount`. |
| `AccountSummaryReport` | 8 columns | 8 columns | PASS | Headers match: `No`, `Entry Date`, `Company Registration No`, `Company Name`, `Voucher No`, `Transaction Title`, `Deducted Fees`, `Remark`. |

Old MPU RDLC columns:

`No.`, `Sakhan`, `Company Name`, `Company Registration No`, `Application No`, `Trxn Date`, `Form Type`, `Application Type`, `MID`, `Card No`, `Invoice No`, `APP Code`, `Trxn Ref No.`, `Trxn Amount`, `MOC`, `IM`, `MPU`, `Voucher No`, `Amount Diff`

New MPU columns:

`No`, `Sakhan`, `Company Name`, `Company Registration No`, `Application No`, `Trxn Date`, `Form Type`, `Application Type`, `MID`, `Card No`, `Invoice No`, `APP Code`, `Trxn Ref No.`, `Trxn Amount`, `MOC`, `IM`, `MPU Amount`, `Voucher No`, `Amount Diff`

Fix for strict parity:

- Change the visible title for the MPU amount column from `MPU Amount` to `MPU` for both `MPUReport` and `MPUReportV3`.

## 3. Data Accuracy Checklist

Use these checks against the same database and the same date window.

### MPUReport

- Compare old `dbo.sp_MPUReport` to new `dbo.sp_MPUReport_pagination`.
- Use identical parameters:
  - `@FromDate`
  - `@ToDate`
  - `@FormType`
  - `@PaymentType`
- Compare row count.
- Compare ordered keys:
  - `Id`
  - `TransactionDateTime`
  - `VoucherNo`
  - `FormType`
- Compare displayed fields:
  - `Sakhan`
  - `CompanyName`
  - `CompanyRegistrationNo`
  - `ApplicationNo`
  - `TransactionAmount`
  - `MOCAmount`
  - `IMAmount`
  - `MPUAmount`
  - `AmountDiff`
- Check old post-processing behavior:
  - Old C# formats `TransactionAmount`, `MOCAmount`, and `MPUAmount`.
  - Old C# de-duplicates rows by `VoucherNo + FormType` after sorting descending by `TransactionDateTime`.
  - New pagination SQL does not visibly reproduce that old C# de-duplication step in the controller; verify carefully with duplicate voucher data.

### MPUReportV3

- Compare old `dbo.sp_MPUReport_V3` to new `dbo.sp_MPUReport_V3_pagination`.
- Use identical parameters.
- Verify row pairing between `MPUPaymentTransaction` and `AccountTransaction`.
- Pay special attention to row-number pairing:
  - Old/new SQL pairs transaction rows by row number within `TransactionId`.
  - Any tie in transaction date or account created date can change pairing if ordering differs.
- Compare `VoucherNo`, `TotalAmount`, and `PaymentDate` in addition to visible RDLC columns.

### AccountSummaryReport

- Compare old `dbo.sp_AccountSummaryReport` to new `dbo.sp_AccountSummaryReport_pagination`.
- Use identical parameters:
  - `@FromDate`
  - `@ToDate`
  - `@FormType`
  - `@SakhanId`
- Compare row count.
- Compare ordered keys:
  - `Id`
  - `VoucherDate`
  - `VoucherNo`
  - `AccountTitleCode`
- Compare displayed fields:
  - `VoucherDate`
  - `CompanyRegistrationNo`
  - `CompanyName`
  - `VoucherNo`
  - `TransactionTitle`
  - `Amount`
- Verify branch coverage for:
  - `Member`
  - `Pa Tha Ka`
  - import/export licence and permit families
  - border licence/permit families
  - `Wine Imporation` spelling from the old SQL
  - sale/show room families
  - EV/EV cycle families

## 4. Performance Checklist

Target:

- First page should return under 3 seconds for narrow date windows.
- First page should return under 10 seconds for normal business date windows.
- No one-minute exact-window query should require a full-table memory grant or time out.
- JSON page path should avoid total count unless the UI explicitly requests it.
- Excel path can stream all rows, but must use background queue and chunking.

Checks:

- Verify controllers pass the frontend `IncludeTotalCount` value instead of forcing `true`.
- Verify stored procedures handle `@IncludeTotalCount = 0` with sentinel pagination.
- Test both:
  - `@IncludeTotalCount = 1`
  - `@IncludeTotalCount = 0`
- Capture:
  - elapsed milliseconds
  - returned rows
  - timeout/errors
  - first 3 rows
- For slow SQL:
  - check execution plan memory grant
  - check temp table/index usage
  - check non-sargable predicates
  - avoid full count on hot path

## 5. Live DB Test Results

Database used:

- `Backend/appsettings.json` -> `ConnectionStrings:TradeNetDBTest`
- Credentials were read locally and not copied into this document.

Procedure existence:

PASS. All six procedures exist in the DB:

- `sp_AccountSummaryReport`
- `sp_AccountSummaryReport_pagination`
- `sp_MPUReport`
- `sp_MPUReport_pagination`
- `sp_MPUReport_V3`
- `sp_MPUReport_V3_pagination`

Latest observed source rows:

- Latest MPU row found: `2026-06-03 11:35:12`, `PaymentType=Citizen Pay`, `FormType=Export Permit`.
- Latest Account Transaction voucher date found: `2026-06-03`.

### Live Test Table

| Test | Parameters | Result |
| --- | --- | --- |
| Old `sp_MPUReport` exact one-minute window | `2026-06-01 16:54:00` to `16:55:00`, `FormType=Import Licence`, `PaymentType=MPU` | PASS: returned 1 row in 202 ms. |
| New `sp_MPUReport_pagination`, same window, `IncludeTotalCount=1` | same as above | FAIL: failed after 25,069 ms with SQL memory resource timeout. |
| New `sp_MPUReport_pagination`, same window, `IncludeTotalCount=0` | same as above | PASS: returned 1 row in 147 ms. |
| Old `sp_MPUReport_V3` exact one-minute window | `2026-06-03 11:35:00` to `11:36:00`, `FormType=Export Permit`, `PaymentType=Citizen Pay` | FAIL: 60,019 ms SQL timeout. |
| New `sp_MPUReport_V3_pagination`, same window, `PaymentType=CitizenPay`, `IncludeTotalCount=1` | same as above | FAIL: 60,017 ms SQL timeout. |
| New `sp_MPUReport_V3_pagination`, same window, `PaymentType=CitizenPay`, `IncludeTotalCount=0` | same as above | FAIL: 60,005 ms SQL timeout. |
| Old `sp_AccountSummaryReport` one-day window | `2026-06-01`, all form types, all sakhans | FAIL: 60,016 ms SQL timeout. |
| New `sp_AccountSummaryReport_pagination`, one-day window, `IncludeTotalCount=1` | same as above | FAIL: 60,021 ms SQL timeout. |
| Old `sp_AccountSummaryReport` one-day `Import Licence` only | `2026-06-01`, `FormType=Import Licence`, `SakhanId=0` | FAIL: 60,060 ms SQL timeout. |
| New `sp_AccountSummaryReport_pagination`, one-day `Import Licence`, `IncludeTotalCount=0` | same as above | FAIL: 60,004 ms SQL timeout. |

Sample row from the successful old/new MPU exact-window test:

- `Id`: `2430919`
- `Sakhan`: `F`
- `TransactionDateTime`: `2026-06-01 16:54:04`
- `CompanyName`: `SD02Co., Ltd.`
- `CompanyRegistrationNo`: `000000005`
- `MerchantId`: `205104001204577`
- `AccountNo`: `950505xxxxxx3707`
- `InvoiceNo`: `IL010626041513000000`
- `ApprovalCode`: `173370`

## 6. Output Result Summary

| Area | `MPUReport` | `MPUReportV3` | `AccountSummaryReport` |
| --- | --- | --- | --- |
| RDLC column parity | FAIL: `MPU` renamed to `MPU Amount` | FAIL: `MPU` renamed to `MPU Amount` | PASS |
| Filter parity | PARTIAL: `PaymentType` has extra `All`; `FormType` is text instead of old dropdown | PARTIAL: same as MPU; payment value normalization should be verified | PARTIAL: same filters, but old dropdowns are not fully reproduced |
| Live data accuracy comparison | PARTIAL PASS: one-row exact-window old/new match when new uses `IncludeTotalCount=0`; controller currently uses `true` | BLOCKED: old and new exact-window calls timed out | BLOCKED: old and new one-day calls timed out |
| Live performance | FAIL in controller path because `includeTotalCount: true` hits memory timeout; fast path is OK | FAIL: exact-window timeout even without total count | FAIL: one-day timeout old and new |

## 7. Recommended Fix / Verification Order

1. DONE - Fix the visible MPU header text:
   - `MPU Amount` -> `MPU` in `MPUReport`
   - `MPU Amount` -> `MPU` in `MPUReportV3`
2. DONE - Change the MPU, MPU V3, and Account Summary controllers to respect `request.IncludeTotalCount` instead of forcing `includeTotalCount: true`.
3. DONE - When `IncludeTotalCount=false`, use `ApiResult<T>.CreateFastPageFromRows(...)` instead of `CreatePageFromRows(...)`.
4. TODO - Convert `FormType` filters from text inputs to old-equivalent dropdown values.
5. TODO - Decide whether new `PaymentType=All` for MPU reports is allowed. If strict parity is required, remove `All` and default to `MPU`.
6. TODO - Optimize `sp_MPUReport_V3_pagination`; exact one-minute windows timing out means the row-number/temp-table path is too broad.
7. TODO - Optimize `sp_AccountSummaryReport_pagination`; even a one-day `Import Licence` page with `IncludeTotalCount=0` timed out.
8. TODO - Re-run the live comparison table above after each fix.

## 8. Fix Task List

### Completed Quick Fixes

| Task | Status | Files |
| --- | --- | --- |
| Match old RDLC MPU header text | DONE | `Frontend\src\Report\config\reportConfigs.ts` |
| Stop forcing exact total count in `MPUReport` JSON endpoint | DONE | `Backend\Controllers\Report\MPUReportController.cs` |
| Stop forcing exact total count in `MPUReportV3` JSON endpoint | DONE | `Backend\Controllers\Report\MPUReportV3Controller.cs` |
| Stop forcing exact total count in `AccountSummaryReport` JSON endpoint | DONE | `Backend\Controllers\Report\AccountSummaryReportController.cs` |
| Use sentinel/fast paging when `IncludeTotalCount=false` | DONE | all three controllers above |

Expected improvement:

- Normal JSON report loads now pass `IncludeTotalCount=false` unless the caller explicitly requests exact counts.
- `sp_MPUReport_pagination` should now use the fast path that returned 1 row in 147 ms in the live DB test.
- Responses with fast paging return estimated total metadata through `ApiResult<T>.CreateFastPageFromRows(...)`.

### Pending Data Accuracy Tasks

| Priority | Task | Why |
| --- | --- | --- |
| P1 | Verify whether MPU old C# de-duplication by `VoucherNo + FormType` must be reproduced in the new API. | Old `Business\Reports.cs` performs this post-processing after the stored procedure result. Missing it can produce duplicate rows. |
| P1 | Re-run old/new MPU comparison after the controller fast-path change. | Confirms the endpoint now follows the successful `IncludeTotalCount=0` SQL path. |
| P1 | Add deterministic comparison script for `Id`, `VoucherNo`, `FormType`, amounts, and row count. | Needed for repeatable data-accuracy proof. |
| P2 | Convert MPU `FormType` from text input to dropdown matching old card type list plus four border types. | Prevents invalid free-text values and restores old filter behavior. |
| P2 | Convert Account Summary `FormType` from text input to dropdown matching old card type list. | Restores old filter behavior. |
| P2 | Confirm Account Summary `SakhanId` renders as dropdown through `ReportLookups/sakhans`. | Old UI used a Sakhan dropdown, not a raw number box. |
| P2 | Decide whether MPU `PaymentType=All` is allowed or should be removed for strict old parity. | Old UI did not expose All. |

### Pending Performance Tasks

| Priority | Task | Why |
| --- | --- | --- |
| P0 | Optimize `sp_MPUReport_V3_pagination`. | Exact one-minute test timed out even with `IncludeTotalCount=0`. |
| P0 | Optimize `sp_AccountSummaryReport_pagination`. | One-day `Import Licence` test timed out even with `IncludeTotalCount=0`. |
| P1 | Add targeted indexes or rewrite temp-table population for V3 row-number pairing. | Current path appears too broad before paging. |
| P1 | Page Account Summary before expanding all branch joins where possible. | Current procedure materializes many branch rows before returning a page. |
| P1 | Add cancellation token forwarding for JSON endpoints. | Client aborts should stop slow SQL work. |
| P2 | Add timing/logging around these three report endpoints. | Needed to detect regressions after SQL changes. |

### Verification Commands / Checks

Run after each fix:

1. `dotnet build Backend\API.csproj`
2. `npm run build` from `Frontend`
3. Live DB stored procedure comparison:
   - old `sp_MPUReport` vs new `sp_MPUReport_pagination`
   - old `sp_MPUReport_V3` vs new `sp_MPUReport_V3_pagination`
   - old `sp_AccountSummaryReport` vs new `sp_AccountSummaryReport_pagination`
4. API endpoint check:
   - POST `/api/MPUReport`
   - POST `/api/MPUReportV3`
   - POST `/api/AccountSummaryReport`
5. Confirm fast-page responses:
   - `isTotalCountExact=false`
   - `hasNextPage` is correct
   - first page returns without exact `COUNT`

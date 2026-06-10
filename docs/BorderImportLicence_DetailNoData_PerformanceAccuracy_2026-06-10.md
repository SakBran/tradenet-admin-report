# Border Import Licence Detail No-Data, Performance, and Accuracy Check

Date: 2026-06-10

## Reports Checked

- `BorderImportLicenceDetailReport`
- `BorderImportLicenceDetailReportPending`

## Current Symptom

The reports can show no data even when Border Import Licence records exist for the selected date.

## Data Accuracy Checklist

- [x] Confirm backend controllers force `Type = "Border"`.
- [x] Confirm backend uses Border Import Licence tables through `sp_ImportLicenceDetailReport_Fast` and `sp_ImportLicencePendingDetailReport_Fast`.
- [x] Confirm live DB has approved Border Import Licence data.
- [x] Confirm live DB has pending Border Import Licence data.
- [x] Confirm known approved licence has item rows.
- [x] Confirm no-data risk from date-only `ToDate`.
- [x] Add regression tests for inclusive date-only `ToDate`.
- [ ] Run same report from UI after deployment with known data dates.

## Performance Checklist

- [x] Check existing index coverage on `BorderImportLicence`.
- [x] Identify that full item-detail join can be slow and can time out on broad ranges.
- [x] Avoid touching other report families.
- [x] Fix the no-data bug before adding indexes.
- [ ] If broad ranges are still slow after the date fix, add a Border Import Licence item-detail indexed procedure or targeted index script after comparing execution plans.

## Live DB Evidence

Known approved data:

- Date: `2026-05-21`
- Licence: `MWDBIL32627000002`
- Status: `Approved`
- `CreatedDate`: `2026-05-21 15:33:49`
- Item rows: `2`

Known pending data:

- Date: `2026-05-19`
- Licences include `test 3` and `test 4`
- Status: `Pending`
- `ApplicationDate`: around `2026-05-19 02:26` to `02:31`

## Root Cause

The frontend date range sends a date-only `ToDate`, for example:

- `2026-05-21 00:00:00`

The backend detail query used:

- approved: `CreatedDate <= ToDate`
- pending: `ApplicationDate <= ToDate`

That excludes records later on the same selected day, such as:

- `2026-05-21 15:33:49`
- `2026-05-19 02:26:35`

So a same-day filter can incorrectly return no data.

## Fix Applied

Only these two controllers were changed:

- `Backend/Controllers/Report/BorderImportLicenceDetailReportController.cs`
- `Backend/Controllers/Report/BorderImportLicenceDetailReportPendingController.cs`

When `ToDate` is date-only, it is normalized to the end of the same day:

- from `2026-05-21 00:00:00`
- to `2026-05-21 23:59:59.9999999`

This preserves existing request shape and does not change other reports or existing stored procedures.

## Index Decision

No index was added yet.

Reason: the immediate no-data issue is a date-boundary accuracy bug, not missing data. The database already has several indexes on `BorderImportLicence`, including status/date/filter combinations. The broad full item-detail join can still be slow, but index changes should be made only after the report returns correct data and an execution plan confirms the missing index or bad join path.

## Next Performance Step If Still Slow

If the fixed reports still perform badly on large date ranges:

- capture execution plan for approved item-detail query;
- capture execution plan for pending item-detail query;
- compare against Border Export Licence detail query pattern;
- add only Border Import Licence specific indexes or a new Border Import Licence specific stored procedure;
- do not modify Import Licence, Border Export Licence, or existing shared procedures.

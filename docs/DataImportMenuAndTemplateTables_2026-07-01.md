# Data Import Menu and Template Table Implementation

Date: 2026-07-01

## Request

Move the data import menu to the same navigation level as `Exports` and `Activity Log`, add data import support for these licence and permit flows, keep each flow in a separate Template DB table, and change the UI from a specific date to a date range with a `Licence Type` filter that includes `All`.

## Implemented Scope

- Added a top-level `Data Import` report menu item beside `Exports`.
- Preserved the old `/Report/ImportLicenceDataImport` route, but the UI now posts to the new generic `DataImport` API.
- Added `Licence Type` options:
  - `All`
  - `ImportLicence`
  - `ExportLicence`
  - `BorderImportLicence`
  - `BorderExportLicence`
  - `ImportPermit`
  - `ExportPermit`
  - `BorderImportPermit`
  - `BorderExportPermit`
- Changed the UI date input from a single date to a `Date Range`.
- Added result output as a table showing saved rows by licence type and date.
- Added a backend scheduled worker that runs every day at 1:00 AM local server time.
- The scheduled run imports `All` licence and permit types for yesterday only.
- Added a compact yearly schedule checklist calendar on the Data Import page.

## Backend Flow

New endpoint:

- `GET api/DataImport/LicenceTypes`
- `GET api/DataImport/Status?date=2025-01-01`
- `GET api/DataImport/CalendarStatus?year=2025`
- `POST api/DataImport`

The HTTP API and the daily schedule both use the same `DataImportService`, so manual and automatic runs follow the same table creation, counting, USD conversion, and upsert rules.

The POST request accepts:

```json
{
  "licenceType": "All",
  "startDate": "2025-05-01T00:00:00",
  "endDate": "2025-05-31T00:00:00"
}
```

For each selected type and each date in the range, the API:

- filters source TradeNet rows by `ApplyType = New`, `Status = Approved`, and `CreatedDate` inside the selected date range;
- counts distinct licence or permit records per day;
- sums item `Amount` values and converts to USD using the existing exchange-rate logic;
- upserts one daily aggregate row into that type's Template DB table.

The status endpoint checks each Template DB table for one exact `LicenceDate` and returns:

- `Imported` when a row exists, including rows with `TotalCount = 0`;
- `Missing` when the table does not exist or no row exists for that date.

The calendar status endpoint checks one year at a time. It returns every day from `2021-01-01` through today, with each day marked complete only when all eight Template DB tables have a row for that date.

## Scheduled Run

The API registers `DataImportScheduleWorker` as a hosted background service.

- Run time: `1:00 AM` local server time.
- Licence type: `All`.
- Date range: yesterday to yesterday.
- Example: if the schedule wakes on `2021-01-06 01:00`, it imports all licence and permit data for `2021-01-05`.
- The scheduled run is idempotent because each target table has a unique `LicenceDate` row and the worker updates existing rows.

## Schedule Checklist Calendar

The Data Import page now shows a compact calendar-style checklist. Users select a year from `2021` through the current year.

For example, if the `2025-01-02 01:00` schedule should have imported `2025-01-01`, select year `2025`:

- A green check means all eight Template DB tables have a row for that date.
- A red cross means one or more Template DB rows are missing for that date.
- A saved zero-count row is treated as successful, because it proves the schedule ran and wrote the date.
- Future dates in the current year are shown neutral, not as missing.

## Template DB Tables

The API creates each table on demand if it does not already exist:

- `dbo.ImportLicence`
- `dbo.ExportLicence`
- `dbo.BorderImportLicence`
- `dbo.BorderExportLicence`
- `dbo.ImportPermit`
- `dbo.ExportPermit`
- `dbo.BorderImportPermit`
- `dbo.BorderExportPermit`

Each table uses the same shape:

```sql
Id int identity primary key,
TotalCount int not null,
TotalAmount decimal(18, 4) not null,
LicenceDate date null,
CreatedDate date not null
```

Each table also gets a unique filtered index on `LicenceDate`, so rerunning the same date updates the existing daily row instead of inserting duplicates.

## Files Changed

- `Backend/Controllers/Report/DataImportController.cs`
- `Backend/Service/Reports/DataImportService.cs`
- `Backend/Service/Reports/DataImportScheduleWorker.cs`
- `Backend/Program.cs`
- `Frontend/src/Report/Page/ImportLicenceDataImport.tsx`
- `Frontend/src/Report/config/reportConfigs.ts`
- `Frontend/src/Report/reportNavItems.tsx`
- `Frontend/src/Report/reportRoutes.tsx`

## Verification

- Frontend production build passed: `npm run build`
- Backend compile passed using a temporary output folder: `dotnet build Backend/API.csproj --no-restore -o outputs/backend-calendar-build-check`

The normal backend build output path was locked by the already-running `API` process, so the build was redirected to a temporary folder for verification.

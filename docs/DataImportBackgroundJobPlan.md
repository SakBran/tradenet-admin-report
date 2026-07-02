# Data Import Background Job Plan

## Current Problem

`POST /api/DataImport` currently runs the full import inside the API request.

For a 365-day import, the request can timeout because the API waits until all data is imported before returning.

## Goal

Change data import to a background job.

The API will only:

1. Create the import job request.
2. Return a `jobId`.
3. Let the frontend show progress.

The actual 365-day import will run in a backend worker, not inside the API request.

## Existing Code To Reuse

- Current import logic: `Backend/Service/Reports/DataImportService.cs`
- Current API: `Backend/Controllers/Report/DataImportController.cs`
- Existing background worker example: `Backend/Service/ExcelExport/ExcelExportWorker.cs`
- Existing durable job pattern:
  - `Backend/Model/ExcelExport/ExcelExportJob.cs`
  - `Backend/DBContext/ApplicationDBContext.cs`
- Current UI: `Frontend/src/Report/Page/ImportLicenceDataImport.tsx`

## Backend Changes

### 1. Add Data Import Job Table

Create a new model named `DataImportJob`.

Fields:

- `Id`
- `LicenceType`
- `StartDate`
- `EndDate`
- `Status`
- `TotalDays`
- `ProcessedDays`
- `TotalRows`
- `ErrorMessage`
- `RequestedByUserName`
- `CreatedAtUtc`
- `StartedAtUtc`
- `CompletedAtUtc`
- `LeaseOwner`
- `LeaseExpiresAtUtc`
- `AttemptCount`

Statuses:

- `Queued`
- `Processing`
- `Completed`
- `Failed`

This table will be stored in TemplateDB.

### 2. Add EF DbSet And Migration

Update `ApplicationDbContext`.

Add:

```csharp
public DbSet<DataImportJob> DataImportJobs { get; set; }
```

Add indexes for:

- `Status + CreatedAtUtc`
- `CreatedAtUtc`
- `LeaseExpiresAtUtc`

### 3. Change Data Import API

Current behavior:

```http
POST /api/DataImport
```

This waits until the full import finishes.

New behavior:

```http
POST /api/DataImport/jobs
```

This returns immediately:

```json
{
  "jobId": "...",
  "status": "Queued",
  "message": "Data import queued."
}
```

Add status endpoint:

```http
GET /api/DataImport/jobs/{jobId}
```

Example response:

```json
{
  "id": "...",
  "status": "Processing",
  "licenceType": "All",
  "startDate": "2025-01-01",
  "endDate": "2025-12-31",
  "totalDays": 365,
  "processedDays": 120,
  "progressPercent": 32,
  "totalRows": 960,
  "errorMessage": null
}
```

Add list endpoint:

```http
GET /api/DataImport/jobs
```

This will show previous import jobs.

### 4. Add Background Worker

Create `DataImportWorker`.

The worker will:

1. Poll `DataImportJobs`.
2. Claim one queued job.
3. Mark it as `Processing`.
4. Import day by day.
5. Update `ProcessedDays` after each day.
6. Mark the job as `Completed` or `Failed`.

The import itself will not run inside the API request.

### 5. Refactor Import Service For Progress

Current service imports the whole range at once.

Add a smaller method:

```csharp
ImportDayAsync(licenceType, date, cancellationToken)
```

The worker can call this for each day from start date to end date.

This gives accurate progress:

```text
processedDays / totalDays
```

For `All`, one day still imports all licence and permit types.

## Frontend Changes

### 1. Submit Job Instead Of Waiting

Current Save button waits for the whole import.

New Save button will:

1. Call `POST /api/DataImport/jobs`.
2. Receive `jobId`.
3. Start showing progress.

### 2. Progress Bar UI

Add Ant Design `Progress`.

Example display:

```text
Status: Processing
120 / 365 days completed
Progress: 32%
```

### 3. Poll Job Status

Frontend will poll:

```http
GET /api/DataImport/jobs/{jobId}
```

Poll every 3-5 seconds while status is:

- `Queued`
- `Processing`

When complete:

- Stop polling.
- Show success message.
- Refresh calendar status.

### 4. Job History Table

Show recent import jobs:

- Date range
- Licence type
- Status
- Progress
- Requested by
- Created date
- Completed date
- Error message if failed

## Important Note About API Usage

The import work will not use the API.

The API is only used for:

- Enqueueing a job.
- Reading job progress and status.

The heavy import runs inside the backend background worker.

## Result

A 365-day import will no longer timeout.

The user can start the import, leave the page open, and see progress while the server continues importing in the background.

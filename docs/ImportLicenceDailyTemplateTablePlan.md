# Import Licence Daily Summary Table Plan

## Goal

Create a daily summary table in **TemplateDB** for Import Licence data from **TradeNetDB**.

The table will store one row per day. Each row represents approved new import licences created during that day.

Required columns:

- `Id`
- `TotalCount`
- `TotalAmount`
- `LicenceDate`
- `CreatedDate`

## Source Database

Source database: `TradeNetDB`

Source tables:

- `ImportLicence`
- `ImportLicenceItem`
- `Currency`
- `ExchangeRate`

Relevant fields:

```text
ImportLicence.Id
ImportLicence.ApplyType
ImportLicence.Status
ImportLicence.CreatedDate
ImportLicence.LicenceDate

ImportLicenceItem.ImportLicenceId
ImportLicenceItem.Amount
ImportLicenceItem.CurrencyId

Currency.Id
Currency.Code

ExchangeRate.CurrencyId
ExchangeRate.Rate
ExchangeRate.Date
```

## Target Database

Target database: `TemplateDB`

Target table name proposal:

```text
ImportLicence
```

Because `TemplateDB` is separate from `TradeNetDB`, this table name is possible. To avoid confusion in code, a clearer name would be:

```text
ImportLicenceDailySummary
```

Recommended table:

```sql
CREATE TABLE dbo.ImportLicenceDailySummary
(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    TotalCount int NOT NULL,
    TotalAmount decimal(38, 4) NOT NULL,
    LicenceDate date NULL,
    CreatedDate date NOT NULL,
    SyncedAt datetime2(0) NOT NULL CONSTRAINT DF_ImportLicenceDailySummary_SyncedAt DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX UX_ImportLicenceDailySummary_CreatedDate
ON dbo.ImportLicenceDailySummary (CreatedDate);
```

If the table must be exactly named `ImportLicence`, use:

```sql
CREATE TABLE dbo.ImportLicence
(
    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    TotalCount int NOT NULL,
    TotalAmount decimal(38, 4) NOT NULL,
    LicenceDate date NULL,
    CreatedDate date NOT NULL,
    SyncedAt datetime2(0) NOT NULL CONSTRAINT DF_TemplateImportLicence_SyncedAt DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX UX_TemplateImportLicence_CreatedDate
ON dbo.ImportLicence (CreatedDate);
```

## Daily Date Rule

For each day, calculate from start of day to before next day.

Example for `2025-01-01`:

```text
From: 2025-01-01 00:00:00.000
To:   2025-01-02 00:00:00.000 exclusive
```

Use this condition:

```sql
ImportLicence.CreatedDate >= @Date
AND ImportLicence.CreatedDate < DATEADD(day, 1, @Date)
```

This is safer than:

```sql
BETWEEN '2025-01-01 00:00:00.000' AND '2025-01-01 23:59:00.000'
```

Reason: `23:59:00.000` misses records from `23:59:00.001` to `23:59:59.999`.

## Filter Rule

Only count licences where:

```sql
ImportLicence.ApplyType = 'New'
AND ImportLicence.Status = 'Approved'
AND ImportLicence.CreatedDate is within the target day
```

## TotalCount Calculation

`TotalCount` is the number of approved new import licences created during the day.

Formula:

```sql
COUNT(DISTINCT ImportLicence.Id)
```

Example:

```sql
SELECT COUNT(DISTINCT il.Id) AS TotalCount
FROM TradeNetDB.dbo.ImportLicence il
WHERE il.ApplyType = 'New'
  AND il.Status = 'Approved'
  AND il.CreatedDate >= @Date
  AND il.CreatedDate < DATEADD(day, 1, @Date);
```

## TotalAmount Calculation

`TotalAmount` is calculated from all items under the filtered licences.

Relationship:

```text
ImportLicence.Id = ImportLicenceItem.ImportLicenceId
```

Each item has:

```text
Amount
CurrencyId
```

Each item amount must be converted to USD before summing.

## USD Conversion Formula

The existing report code uses this formula:

```text
USD amount = Item Amount * (Currency Rate / USD Rate)
```

Special rules from current backend code:

```text
If item currency is USD:
    USD amount = Item Amount

If item currency is JPY or KRW:
    USD amount = Item Amount * ((Currency Rate / USD Rate) / 100)

For other currencies:
    USD amount = Item Amount * (Currency Rate / USD Rate)
```

Exchange rate lookup:

```text
ExchangeRate.Date = ImportLicence.CreatedDate date
ExchangeRate.CurrencyId = ImportLicenceItem.CurrencyId
USD Rate = ExchangeRate row where Currency.Code = 'USD' for the same day
```

If the business wants MMK instead of USD, the formula is different:

```text
MMK amount = Item Amount * Currency Rate
```

For this daily TemplateDB table, the stated requirement says converted to **USD value**, so use:

```text
Item Amount * (Currency Rate / USD Rate)
```

## Recommended Daily Upsert SQL

This is the logical SQL for one day.

```sql
DECLARE @Date date = '2025-01-01';

WITH FilteredLicences AS
(
    SELECT
        il.Id
    FROM TradeNetDB.dbo.ImportLicence il
    WHERE il.ApplyType = 'New'
      AND il.Status = 'Approved'
      AND il.CreatedDate >= @Date
      AND il.CreatedDate < DATEADD(day, 1, @Date)
),
UsdRate AS
(
    SELECT TOP (1)
        er.Rate
    FROM TradeNetDB.dbo.ExchangeRate er
    INNER JOIN TradeNetDB.dbo.Currency c ON c.Id = er.CurrencyId
    WHERE c.Code = 'USD'
      AND er.Date >= @Date
      AND er.Date < DATEADD(day, 1, @Date)
    ORDER BY er.Id
),
ItemUsdAmounts AS
(
    SELECT
        fl.Id AS ImportLicenceId,
        CASE
            WHEN c.Code = 'USD' THEN ili.Amount
            WHEN c.Code IN ('JPY', 'KRW') THEN ili.Amount * ((ISNULL(er.Rate, 1) / NULLIF(ISNULL(usd.Rate, 1), 0)) / 100)
            ELSE ili.Amount * (ISNULL(er.Rate, 1) / NULLIF(ISNULL(usd.Rate, 1), 0))
        END AS UsdAmount
    FROM FilteredLicences fl
    INNER JOIN TradeNetDB.dbo.ImportLicenceItem ili ON ili.ImportLicenceId = fl.Id
    INNER JOIN TradeNetDB.dbo.Currency c ON c.Id = ili.CurrencyId
    CROSS JOIN UsdRate usd
    OUTER APPLY
    (
        SELECT TOP (1)
            er.Rate
        FROM TradeNetDB.dbo.ExchangeRate er
        WHERE er.CurrencyId = ili.CurrencyId
          AND er.Date >= @Date
          AND er.Date < DATEADD(day, 1, @Date)
        ORDER BY er.Id
    ) er
),
DailySummary AS
(
    SELECT
        COUNT(DISTINCT fl.Id) AS TotalCount,
        ISNULL(SUM(iua.UsdAmount), 0) AS TotalAmount,
        CAST(NULL AS date) AS LicenceDate,
        @Date AS CreatedDate
    FROM FilteredLicences fl
    LEFT JOIN ItemUsdAmounts iua ON iua.ImportLicenceId = fl.Id
)
MERGE TemplateDB.dbo.ImportLicenceDailySummary AS target
USING DailySummary AS source
    ON target.CreatedDate = source.CreatedDate
WHEN MATCHED THEN
    UPDATE SET
        target.TotalCount = source.TotalCount,
        target.TotalAmount = source.TotalAmount,
        target.LicenceDate = source.LicenceDate,
        target.SyncedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (TotalCount, TotalAmount, LicenceDate, CreatedDate)
    VALUES (source.TotalCount, source.TotalAmount, source.LicenceDate, source.CreatedDate);
```

## Important Decision: LicenceDate

The requested target table has only one `LicenceDate` per day.

But a single CreatedDate day can contain many licences, and each licence can have a different `LicenceDate`.

Recommended options:

1. Store `CreatedDate` as the daily grouping date and remove `LicenceDate`.
2. Store `LicenceDate = MIN(ImportLicence.LicenceDate)` for that CreatedDate day.
3. Store separate rows by both `CreatedDate` and `LicenceDate`.

Best reporting design:

```text
One row per CreatedDate only
```

Because the requested `TotalCount` is based on `CreatedDate`.

In the simplified version, `FilteredLicences` only needs the licence IDs for counting and item joins. If `LicenceDate` is not needed for business output, it should be removed from the target table. If it must remain for schema reasons, save it as `NULL` or define an explicit rule such as `MIN(LicenceDate)`.

## Simplified Rule

If your requirement is only:

- daily licence count
- daily total USD amount
- grouped by `ImportLicence.CreatedDate`

then `FilteredLicences` should only return:

```sql
SELECT il.Id
```

That is enough for:

- `TotalCount`
- joining to `ImportLicenceItem`
- calculating `TotalAmount`

No extra `CreatedDate` or `LicenceDate` columns are needed inside `FilteredLicences`.

## Suggested Job Flow

Run a daily sync job.

For example, every day at `00:10`, sync yesterday:

```text
@Date = CAST(DATEADD(day, -1, GETDATE()) AS date)
```

Process:

1. Set target day.
2. Read approved new ImportLicence rows from TradeNetDB for that day.
3. Count distinct ImportLicence IDs.
4. Join ImportLicenceItem.
5. Convert each item amount to USD using ExchangeRate.
6. Sum USD values.
7. Upsert one row into TemplateDB.

## Backfill Flow

For first-time data migration, loop day by day:

```text
StartDate = earliest ImportLicence.CreatedDate
EndDate = today
For each day:
    calculate daily summary
    upsert into TemplateDB
```

## Validation Query

For one day, verify count:

```sql
DECLARE @Date date = '2025-01-01';

SELECT COUNT(DISTINCT il.Id) AS ExpectedTotalCount
FROM TradeNetDB.dbo.ImportLicence il
WHERE il.ApplyType = 'New'
  AND il.Status = 'Approved'
  AND il.CreatedDate >= @Date
  AND il.CreatedDate < DATEADD(day, 1, @Date);
```

Verify saved row:

```sql
SELECT *
FROM TemplateDB.dbo.ImportLicenceDailySummary
WHERE CreatedDate = '2025-01-01';
```

## Open Questions Before Implementation

1. Should the TemplateDB table name be exactly `ImportLicence`, or can it be clearer as `ImportLicenceDailySummary`?
2. Should `LicenceDate` be stored as `MIN(LicenceDate)`, `MAX(LicenceDate)`, or should rows be grouped by `LicenceDate` too?
3. If an exchange rate is missing, should the system use the current report behavior and default missing rate to `1`, or should it mark the day as failed?
4. Should `TotalAmount` mean USD amount, or MMK amount?

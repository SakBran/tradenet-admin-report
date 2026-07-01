using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace API.Service.Reports
{
    public interface IDataImportService
    {
        IReadOnlyList<DataImportLicenceTypeOption> GetLicenceTypes();

        Task<DataImportStatusResult> GetStatusAsync(
            DateTime date,
            CancellationToken cancellationToken = default);

        Task<DataImportCalendarStatusResult> GetCalendarStatusAsync(
            int year,
            CancellationToken cancellationToken = default);

        Task<DataImportResult> ImportAsync(
            string? licenceType,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);
    }

    public sealed class DataImportService : IDataImportService
    {
        public const string All = "All";
        private const string Approved = "Approved";
        private const string New = "New";
        private const string Usd = "USD";

        private static readonly IReadOnlyList<ImportTarget> Targets = new[]
        {
            new ImportTarget("ImportLicence", "Import Licence"),
            new ImportTarget("ExportLicence", "Export Licence"),
            new ImportTarget("BorderImportLicence", "Border Import Licence"),
            new ImportTarget("BorderExportLicence", "Border Export Licence"),
            new ImportTarget("ImportPermit", "Import Permit"),
            new ImportTarget("ExportPermit", "Export Permit"),
            new ImportTarget("BorderImportPermit", "Border Import Permit"),
            new ImportTarget("BorderExportPermit", "Border Export Permit"),
        };

        private readonly ApplicationDbContext _templateDb;
        private readonly TradeNetDbContext _tradeNetDb;

        public DataImportService(ApplicationDbContext templateDb, TradeNetDbContext tradeNetDb)
        {
            _templateDb = templateDb;
            _tradeNetDb = tradeNetDb;
        }

        public IReadOnlyList<DataImportLicenceTypeOption> GetLicenceTypes() =>
            new[] { new DataImportLicenceTypeOption(All, All) }
                .Concat(Targets.Select(target => new DataImportLicenceTypeOption(target.Key, target.Label)))
                .ToList();

        public async Task<DataImportStatusResult> GetStatusAsync(
            DateTime date,
            CancellationToken cancellationToken = default)
        {
            var importDate = date.Date;
            var rows = new List<DataImportStatusRow>();

            foreach (var target in Targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(await GetStatusRowAsync(target, importDate, cancellationToken));
            }

            return new DataImportStatusResult
            {
                Date = importDate,
                IsComplete = rows.All(row => row.IsImported),
                Rows = rows,
            };
        }

        public async Task<DataImportResult> ImportAsync(
            string? licenceType,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            var fromDate = startDate.Date;
            var toDate = endDate.Date;
            if (toDate < fromDate)
            {
                throw new ArgumentException("End date must be greater than or equal to start date.");
            }

            var selectedTargets = ResolveTargets(licenceType);
            if (selectedTargets.Count == 0)
            {
                throw new ArgumentException("Unknown licence type.");
            }

            var rows = new List<DataImportSavedRow>();
            foreach (var target in selectedTargets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await EnsureTemplateTableAsync(target.Key, cancellationToken);
                var aggregates = await BuildDailyAggregatesAsync(target.Key, fromDate, toDate, cancellationToken);

                foreach (var aggregate in aggregates.OrderBy(row => row.LicenceDate))
                {
                    var id = await UpsertTemplateRowAsync(target.Key, aggregate, cancellationToken);
                    rows.Add(new DataImportSavedRow
                    {
                        Id = id,
                        LicenceType = target.Key,
                        LicenceTypeLabel = target.Label,
                        TotalCount = aggregate.TotalCount,
                        TotalAmount = aggregate.TotalAmount,
                        LicenceDate = aggregate.LicenceDate,
                        CreatedDate = aggregate.CreatedDate,
                    });
                }
            }

            return new DataImportResult
            {
                LicenceType = string.IsNullOrWhiteSpace(licenceType) ? All : licenceType,
                StartDate = fromDate,
                EndDate = toDate,
                Rows = rows,
            };
        }

        public async Task<DataImportCalendarStatusResult> GetCalendarStatusAsync(
            int year,
            CancellationToken cancellationToken = default)
        {
            var currentYear = DateTime.Today.Year;
            if (year < 2021 || year > currentYear)
            {
                throw new ArgumentException($"Year must be between 2021 and {currentYear}.");
            }

            var startDate = new DateTime(year, 1, 1);
            var endDate = year == currentYear ? DateTime.Today : new DateTime(year, 12, 31);
            var importedTargetCountByDate = new Dictionary<DateTime, int>();

            foreach (var target in Targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var importedDates = await GetImportedDatesAsync(target.Key, startDate, endDate, cancellationToken);
                foreach (var importedDate in importedDates)
                {
                    importedTargetCountByDate[importedDate] = importedTargetCountByDate.GetValueOrDefault(importedDate) + 1;
                }
            }

            var days = new List<DataImportCalendarDayStatus>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var importedTypeCount = importedTargetCountByDate.GetValueOrDefault(date);
                days.Add(new DataImportCalendarDayStatus
                {
                    Date = date,
                    IsComplete = importedTypeCount == Targets.Count,
                    ImportedTypeCount = importedTypeCount,
                    RequiredTypeCount = Targets.Count,
                });
            }

            return new DataImportCalendarStatusResult
            {
                Year = year,
                StartDate = startDate,
                EndDate = endDate,
                Days = days,
            };
        }

        private async Task<DataImportStatusRow> GetStatusRowAsync(
            ImportTarget target,
            DateTime importDate,
            CancellationToken cancellationToken)
        {
            var tableExists = await TemplateTableExistsAsync(target.Key, cancellationToken);
            if (!tableExists)
            {
                return new DataImportStatusRow
                {
                    LicenceType = target.Key,
                    LicenceTypeLabel = target.Label,
                    IsImported = false,
                    Message = "Template table has not been created.",
                };
            }

            var sql = $@"
SELECT TOP (1)
    Id,
    TotalCount,
    TotalAmount,
    LicenceDate,
    CreatedDate
FROM dbo.{target.Key}
WHERE LicenceDate = @LicenceDate;";

            var connection = _templateDb.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@LicenceDate", importDate));

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new DataImportStatusRow
                {
                    LicenceType = target.Key,
                    LicenceTypeLabel = target.Label,
                    IsImported = false,
                    Message = "No Template DB row for this date.",
                };
            }

            return new DataImportStatusRow
            {
                Id = reader.GetInt32(0),
                LicenceType = target.Key,
                LicenceTypeLabel = target.Label,
                IsImported = true,
                TotalCount = reader.GetInt32(1),
                TotalAmount = reader.GetDecimal(2),
                LicenceDate = reader.GetDateTime(3),
                CreatedDate = reader.GetDateTime(4),
                Message = "Imported.",
            };
        }

        private async Task<List<DateTime>> GetImportedDatesAsync(
            string tableName,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            var tableExists = await TemplateTableExistsAsync(tableName, cancellationToken);
            if (!tableExists)
            {
                return new List<DateTime>();
            }

            var sql = $@"
SELECT LicenceDate
FROM dbo.{tableName}
WHERE LicenceDate >= @StartDate
  AND LicenceDate <= @EndDate;";

            var connection = _templateDb.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@StartDate", startDate));
            command.Parameters.Add(new SqlParameter("@EndDate", endDate));

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var dates = new List<DateTime>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                dates.Add(reader.GetDateTime(0).Date);
            }

            return dates;
        }

        private async Task<bool> TemplateTableExistsAsync(
            string tableName,
            CancellationToken cancellationToken)
        {
            const string sql = "SELECT CASE WHEN OBJECT_ID(@TableName, N'U') IS NULL THEN 0 ELSE 1 END;";
            var connection = _templateDb.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@TableName", $"dbo.{tableName}"));

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }

        private static List<ImportTarget> ResolveTargets(string? licenceType)
        {
            if (string.IsNullOrWhiteSpace(licenceType) ||
                string.Equals(licenceType, All, StringComparison.OrdinalIgnoreCase))
            {
                return Targets.ToList();
            }

            return Targets
                .Where(target => string.Equals(target.Key, licenceType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private async Task<List<DailyImportAggregate>> BuildDailyAggregatesAsync(
            string targetKey,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            var nextDate = endDate.AddDays(1);
            var licenceRows = await GetLicenceRowsAsync(targetKey, startDate, nextDate, cancellationToken);
            var licenceIds = licenceRows.Select(row => row.Id).Distinct().ToList();
            var licenceDateById = licenceRows
                .GroupBy(row => row.Id)
                .ToDictionary(group => group.Key, group => group.First().LicenceDate);

            var itemRows = licenceIds.Count == 0
                ? new List<ImportItemRow>()
                : await GetItemRowsAsync(targetKey, licenceIds, cancellationToken);

            var currencyIds = itemRows.Select(row => row.CurrencyId).Distinct().ToList();
            var currencyMap = currencyIds.Count == 0
                ? new Dictionary<int, string>()
                : await _tradeNetDb.Currencies
                    .AsNoTracking()
                    .Where(currency => currencyIds.Contains(currency.Id))
                    .ToDictionaryAsync(currency => currency.Id, currency => currency.Code ?? string.Empty, cancellationToken);

            var rateCurrencyIds = currencyIds.ToHashSet();
            var usdCurrencyId = await _tradeNetDb.Currencies
                .AsNoTracking()
                .Where(currency => currency.Code == Usd)
                .Select(currency => (int?)currency.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (usdCurrencyId.HasValue)
            {
                rateCurrencyIds.Add(usdCurrencyId.Value);
            }

            var rateRows = rateCurrencyIds.Count == 0
                ? new List<ExchangeRateRow>()
                : await _tradeNetDb.ExchangeRates
                    .AsNoTracking()
                    .Where(rate =>
                        rateCurrencyIds.Contains(rate.CurrencyId) &&
                        rate.Date >= startDate &&
                        rate.Date < nextDate)
                    .OrderBy(rate => rate.Id)
                    .Select(rate => new ExchangeRateRow(rate.CurrencyId, rate.Date.Date, rate.Rate))
                    .ToListAsync(cancellationToken);

            var rateByCurrencyAndDate = rateRows
                .GroupBy(rate => (rate.CurrencyId, rate.Date))
                .ToDictionary(group => group.Key, group => group.First().Rate);

            var amountByDate = new Dictionary<DateTime, decimal>();
            foreach (var item in itemRows)
            {
                if (!licenceDateById.TryGetValue(item.LicenceId, out var licenceDate))
                {
                    continue;
                }

                var currencyCode = currencyMap.GetValueOrDefault(item.CurrencyId, string.Empty);
                var usdRate = usdCurrencyId.HasValue &&
                              rateByCurrencyAndDate.TryGetValue((usdCurrencyId.Value, licenceDate), out var foundUsdRate)
                    ? foundUsdRate
                    : 1m;

                var amount = ConvertToUsd(
                    item.Amount,
                    currencyCode,
                    item.CurrencyId,
                    licenceDate,
                    rateByCurrencyAndDate,
                    usdRate);

                amountByDate[licenceDate] = amountByDate.GetValueOrDefault(licenceDate) + amount;
            }

            var countByDate = licenceRows
                .GroupBy(row => row.LicenceDate)
                .ToDictionary(group => group.Key, group => group.Select(row => row.Id).Distinct().Count());

            var aggregates = new List<DailyImportAggregate>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                aggregates.Add(new DailyImportAggregate
                {
                    TotalCount = countByDate.GetValueOrDefault(date),
                    TotalAmount = decimal.Round(amountByDate.GetValueOrDefault(date), 4),
                    LicenceDate = date,
                    CreatedDate = DateTime.Today,
                });
            }

            return aggregates;
        }

        private Task<List<ImportLicenceRow>> GetLicenceRowsAsync(
            string targetKey,
            DateTime startDate,
            DateTime nextDate,
            CancellationToken cancellationToken) =>
            targetKey switch
            {
                "ImportLicence" => _tradeNetDb.ImportLicences
                    .AsNoTracking()
                    .Where(row => row.ApplyType == New && row.Status == Approved && row.CreatedDate >= startDate && row.CreatedDate < nextDate)
                    .Select(row => new ImportLicenceRow(row.Id, row.CreatedDate!.Value.Date))
                    .ToListAsync(cancellationToken),
                "ExportLicence" => _tradeNetDb.ExportLicences
                    .AsNoTracking()
                    .Where(row => row.ApplyType == New && row.Status == Approved && row.CreatedDate >= startDate && row.CreatedDate < nextDate)
                    .Select(row => new ImportLicenceRow(row.Id, row.CreatedDate!.Value.Date))
                    .ToListAsync(cancellationToken),
                "BorderImportLicence" => _tradeNetDb.BorderImportLicences
                    .AsNoTracking()
                    .Where(row => row.ApplyType == New && row.Status == Approved && row.CreatedDate >= startDate && row.CreatedDate < nextDate)
                    .Select(row => new ImportLicenceRow(row.Id, row.CreatedDate!.Value.Date))
                    .ToListAsync(cancellationToken),
                "BorderExportLicence" => _tradeNetDb.BorderExportLicences
                    .AsNoTracking()
                    .Where(row => row.ApplyType == New && row.Status == Approved && row.CreatedDate >= startDate && row.CreatedDate < nextDate)
                    .Select(row => new ImportLicenceRow(row.Id, row.CreatedDate!.Value.Date))
                    .ToListAsync(cancellationToken),
                "ImportPermit" => _tradeNetDb.ImportPermits
                    .AsNoTracking()
                    .Where(row => row.ApplyType == New && row.Status == Approved && row.CreatedDate >= startDate && row.CreatedDate < nextDate)
                    .Select(row => new ImportLicenceRow(row.Id, row.CreatedDate!.Value.Date))
                    .ToListAsync(cancellationToken),
                "ExportPermit" => _tradeNetDb.ExportPermits
                    .AsNoTracking()
                    .Where(row => row.ApplyType == New && row.Status == Approved && row.CreatedDate >= startDate && row.CreatedDate < nextDate)
                    .Select(row => new ImportLicenceRow(row.Id, row.CreatedDate!.Value.Date))
                    .ToListAsync(cancellationToken),
                "BorderImportPermit" => _tradeNetDb.BorderImportPermits
                    .AsNoTracking()
                    .Where(row => row.ApplyType == New && row.Status == Approved && row.CreatedDate >= startDate && row.CreatedDate < nextDate)
                    .Select(row => new ImportLicenceRow(row.Id, row.CreatedDate!.Value.Date))
                    .ToListAsync(cancellationToken),
                "BorderExportPermit" => _tradeNetDb.BorderExportPermits
                    .AsNoTracking()
                    .Where(row => row.ApplyType == New && row.Status == Approved && row.CreatedDate >= startDate && row.CreatedDate < nextDate)
                    .Select(row => new ImportLicenceRow(row.Id, row.CreatedDate!.Value.Date))
                    .ToListAsync(cancellationToken),
                _ => Task.FromResult(new List<ImportLicenceRow>()),
            };

        private Task<List<ImportItemRow>> GetItemRowsAsync(
            string targetKey,
            IReadOnlyCollection<string> licenceIds,
            CancellationToken cancellationToken) =>
            targetKey switch
            {
                "ImportLicence" => _tradeNetDb.ImportLicenceItems
                    .AsNoTracking()
                    .Where(row => licenceIds.Contains(row.ImportLicenceId))
                    .Select(row => new ImportItemRow(row.ImportLicenceId, row.Amount, row.CurrencyId))
                    .ToListAsync(cancellationToken),
                "ExportLicence" => _tradeNetDb.ExportLicenceItems
                    .AsNoTracking()
                    .Where(row => licenceIds.Contains(row.ExportLicenceId))
                    .Select(row => new ImportItemRow(row.ExportLicenceId, row.Amount, row.CurrencyId))
                    .ToListAsync(cancellationToken),
                "BorderImportLicence" => _tradeNetDb.BorderImportLicenceItems
                    .AsNoTracking()
                    .Where(row => licenceIds.Contains(row.BorderImportLicenceId))
                    .Select(row => new ImportItemRow(row.BorderImportLicenceId, row.Amount, row.CurrencyId))
                    .ToListAsync(cancellationToken),
                "BorderExportLicence" => _tradeNetDb.BorderExportLicenceItems
                    .AsNoTracking()
                    .Where(row => licenceIds.Contains(row.BorderExportLicenceId))
                    .Select(row => new ImportItemRow(row.BorderExportLicenceId, row.Amount, row.CurrencyId))
                    .ToListAsync(cancellationToken),
                "ImportPermit" => _tradeNetDb.ImportPermitItems
                    .AsNoTracking()
                    .Where(row => licenceIds.Contains(row.ImportPermitId))
                    .Select(row => new ImportItemRow(row.ImportPermitId, row.Amount, row.CurrencyId))
                    .ToListAsync(cancellationToken),
                "ExportPermit" => _tradeNetDb.ExportPermitItems
                    .AsNoTracking()
                    .Where(row => licenceIds.Contains(row.ExportPermitId))
                    .Select(row => new ImportItemRow(row.ExportPermitId, row.Amount, row.CurrencyId))
                    .ToListAsync(cancellationToken),
                "BorderImportPermit" => _tradeNetDb.BorderImportPermitItems
                    .AsNoTracking()
                    .Where(row => licenceIds.Contains(row.BorderImportPermitId))
                    .Select(row => new ImportItemRow(row.BorderImportPermitId, row.Amount, row.CurrencyId))
                    .ToListAsync(cancellationToken),
                "BorderExportPermit" => _tradeNetDb.BorderExportPermitItems
                    .AsNoTracking()
                    .Where(row => licenceIds.Contains(row.BorderExportPermitId))
                    .Select(row => new ImportItemRow(row.BorderExportPermitId, row.Amount, row.CurrencyId))
                    .ToListAsync(cancellationToken),
                _ => Task.FromResult(new List<ImportItemRow>()),
            };

        private async Task EnsureTemplateTableAsync(string tableName, CancellationToken cancellationToken)
        {
            var sql = $@"
IF OBJECT_ID(N'dbo.{tableName}', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.{tableName}
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Template{tableName} PRIMARY KEY,
        TotalCount int NOT NULL,
        TotalAmount decimal(18, 4) NOT NULL,
        LicenceDate date NULL,
        CreatedDate date NOT NULL
    );
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Template{tableName}_LicenceDate'
      AND object_id = OBJECT_ID(N'dbo.{tableName}')
)
BEGIN
    CREATE UNIQUE INDEX UX_Template{tableName}_LicenceDate
        ON dbo.{tableName} (LicenceDate)
        WHERE LicenceDate IS NOT NULL;
END";

            await _templateDb.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        private async Task<int> UpsertTemplateRowAsync(
            string tableName,
            DailyImportAggregate aggregate,
            CancellationToken cancellationToken)
        {
            var sql = $@"
DECLARE @SavedIds TABLE (Id int);

MERGE dbo.{tableName} AS target
USING (SELECT @LicenceDate AS LicenceDate) AS source
ON target.LicenceDate = source.LicenceDate
WHEN MATCHED THEN
    UPDATE SET
        TotalCount = @TotalCount,
        TotalAmount = @TotalAmount,
        CreatedDate = @CreatedDate
WHEN NOT MATCHED THEN
    INSERT (TotalCount, TotalAmount, LicenceDate, CreatedDate)
    VALUES (@TotalCount, @TotalAmount, @LicenceDate, @CreatedDate)
OUTPUT inserted.Id INTO @SavedIds;

SELECT TOP (1) Id FROM @SavedIds;";

            var connection = _templateDb.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@TotalCount", aggregate.TotalCount));
            command.Parameters.Add(new SqlParameter("@TotalAmount", aggregate.TotalAmount));
            command.Parameters.Add(new SqlParameter("@LicenceDate", aggregate.LicenceDate));
            command.Parameters.Add(new SqlParameter("@CreatedDate", aggregate.CreatedDate));

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var id = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(id);
        }

        private static decimal ConvertToUsd(
            decimal amount,
            string currencyCode,
            int currencyId,
            DateTime licenceDate,
            IReadOnlyDictionary<(int CurrencyId, DateTime Date), decimal> rateByCurrencyAndDate,
            decimal usdRate)
        {
            if (string.Equals(currencyCode, Usd, StringComparison.OrdinalIgnoreCase))
            {
                return decimal.Round(amount, 4);
            }

            if (usdRate == 0m)
            {
                return 0m;
            }

            var currencyRate = rateByCurrencyAndDate.TryGetValue((currencyId, licenceDate), out var foundRate)
                ? foundRate
                : 1m;

            var factor = currencyRate / usdRate;
            if (string.Equals(currencyCode, "JPY", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currencyCode, "KRW", StringComparison.OrdinalIgnoreCase))
            {
                factor /= 100m;
            }

            return decimal.Round(amount * factor, 4);
        }
    }

    public sealed class DataImportRequest
    {
        public string? LicenceType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public sealed class DataImportResult
    {
        public string LicenceType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<DataImportSavedRow> Rows { get; set; } = new();
    }

    public sealed class DataImportSavedRow
    {
        public int Id { get; set; }
        public string LicenceType { get; set; } = string.Empty;
        public string LicenceTypeLabel { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime LicenceDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public sealed class DataImportStatusResult
    {
        public DateTime Date { get; set; }
        public bool IsComplete { get; set; }
        public List<DataImportStatusRow> Rows { get; set; } = new();
    }

    public sealed class DataImportStatusRow
    {
        public int? Id { get; set; }
        public string LicenceType { get; set; } = string.Empty;
        public string LicenceTypeLabel { get; set; } = string.Empty;
        public bool IsImported { get; set; }
        public int? TotalCount { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? LicenceDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class DataImportCalendarStatusResult
    {
        public int Year { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<DataImportCalendarDayStatus> Days { get; set; } = new();
    }

    public sealed class DataImportCalendarDayStatus
    {
        public DateTime Date { get; set; }
        public bool IsComplete { get; set; }
        public int ImportedTypeCount { get; set; }
        public int RequiredTypeCount { get; set; }
    }

    public sealed record DataImportLicenceTypeOption(string Value, string Label);

    internal sealed record ImportTarget(string Key, string Label);

    internal sealed record ImportLicenceRow(string Id, DateTime LicenceDate);

    internal sealed record ImportItemRow(string LicenceId, decimal Amount, int CurrencyId);

    internal sealed record ExchangeRateRow(int CurrencyId, DateTime Date, decimal Rate);

    internal sealed class DailyImportAggregate
    {
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime LicenceDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}

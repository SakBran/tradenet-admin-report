using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using API.Model.TradeNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ImportLicenceDataImportController : ControllerBase
    {
        private const string Approved = "Approved";
        private const string New = "New";
        private const string Usd = "USD";

        private readonly ApplicationDbContext _templateDb;
        private readonly TradeNetDbContext _tradeNetDb;

        public ImportLicenceDataImportController(
            ApplicationDbContext templateDb,
            TradeNetDbContext tradeNetDb)
        {
            _templateDb = templateDb;
            _tradeNetDb = tradeNetDb;
        }

        [HttpPost]
        public async Task<ActionResult<ImportLicenceDataImportResult>> Post(
            [FromBody] ImportLicenceDataImportRequest? request)
        {
            if (request is null || request.Date == default)
            {
                return BadRequest("Date is required.");
            }

            await EnsureTemplateTableAsync();

            var reportDate = request.Date.Date;
            var nextDate = reportDate.AddDays(1);
            var createdDate = DateTime.Today;

            var licenceIds = await _tradeNetDb.ImportLicences
                .AsNoTracking()
                .Where(licence =>
                    licence.ApplyType == New &&
                    licence.Status == Approved &&
                    licence.CreatedDate >= reportDate &&
                    licence.CreatedDate < nextDate)
                .Select(licence => licence.Id)
                .ToListAsync();

            var totalCount = licenceIds.Count;
            var totalAmount = 0m;

            if (licenceIds.Count > 0)
            {
                var items = await _tradeNetDb.ImportLicenceItems
                    .AsNoTracking()
                    .Where(item => licenceIds.Contains(item.ImportLicenceId))
                    .Select(item => new
                    {
                        item.Amount,
                        item.CurrencyId,
                    })
                    .ToListAsync();

                var currencyIds = items
                    .Select(item => item.CurrencyId)
                    .Distinct()
                    .ToList();

                var currencyMap = await _tradeNetDb.Currencies
                    .AsNoTracking()
                    .Where(currency => currencyIds.Contains(currency.Id))
                    .ToDictionaryAsync(currency => currency.Id, currency => currency.Code ?? string.Empty);

                var rateCurrencyIds = currencyIds.ToHashSet();
                var usdCurrencyId = await _tradeNetDb.Currencies
                    .AsNoTracking()
                    .Where(currency => currency.Code == Usd)
                    .Select(currency => (int?)currency.Id)
                    .FirstOrDefaultAsync();

                if (usdCurrencyId.HasValue)
                {
                    rateCurrencyIds.Add(usdCurrencyId.Value);
                }

                var rateRows = await _tradeNetDb.ExchangeRates
                    .AsNoTracking()
                    .Where(rate =>
                        rateCurrencyIds.Contains(rate.CurrencyId) &&
                        rate.Date >= reportDate &&
                        rate.Date < nextDate)
                    .OrderBy(rate => rate.Id)
                    .Select(rate => new
                    {
                        rate.CurrencyId,
                        rate.Rate,
                    })
                    .ToListAsync();

                var rateByCurrencyId = new Dictionary<int, decimal>();
                foreach (var rateRow in rateRows)
                {
                    if (!rateByCurrencyId.ContainsKey(rateRow.CurrencyId))
                    {
                        rateByCurrencyId[rateRow.CurrencyId] = rateRow.Rate;
                    }
                }

                var usdRate = usdCurrencyId.HasValue && rateByCurrencyId.TryGetValue(usdCurrencyId.Value, out var foundUsdRate)
                    ? foundUsdRate
                    : 1m;

                foreach (var item in items)
                {
                    var currencyCode = currencyMap.GetValueOrDefault(item.CurrencyId, string.Empty);
                    totalAmount += ConvertToUsd(item.Amount, currencyCode, item.CurrencyId, rateByCurrencyId, usdRate);
                }

                totalAmount = decimal.Round(totalAmount, 4);
            }

            var existing = await _templateDb.ImportLicenceDailyImports
                .FirstOrDefaultAsync(row => row.LicenceDate == reportDate);

            if (existing == null)
            {
                existing = new Backend.Model.ImportLicenceDailyImport
                {
                    CreatedDate = createdDate,
                };
                _templateDb.ImportLicenceDailyImports.Add(existing);
            }

            existing.TotalCount = totalCount;
            existing.TotalAmount = totalAmount;
            existing.LicenceDate = reportDate;
            existing.CreatedDate = createdDate;

            await _templateDb.SaveChangesAsync();

            return Ok(new ImportLicenceDataImportResult
            {
                Id = existing.Id,
                TotalCount = existing.TotalCount,
                TotalAmount = existing.TotalAmount,
                LicenceDate = existing.LicenceDate,
                CreatedDate = existing.CreatedDate,
            });
        }

        private async Task EnsureTemplateTableAsync()
        {
            const string sql = @"
IF OBJECT_ID(N'dbo.ImportLicence', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ImportLicence
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TemplateImportLicence PRIMARY KEY,
        TotalCount int NOT NULL,
        TotalAmount decimal(18, 4) NOT NULL,
        LicenceDate date NULL,
        CreatedDate date NOT NULL
    );
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_TemplateImportLicence_CreatedDate'
      AND object_id = OBJECT_ID(N'dbo.ImportLicence')
)
BEGIN
    DROP INDEX UX_TemplateImportLicence_CreatedDate ON dbo.ImportLicence;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_TemplateImportLicence_LicenceDate'
      AND object_id = OBJECT_ID(N'dbo.ImportLicence')
)
BEGIN
    CREATE UNIQUE INDEX UX_TemplateImportLicence_LicenceDate
        ON dbo.ImportLicence (LicenceDate)
        WHERE LicenceDate IS NOT NULL;
END";

            await _templateDb.Database.ExecuteSqlRawAsync(sql);
        }

        private static decimal ConvertToUsd(
            decimal amount,
            string currencyCode,
            int currencyId,
            IReadOnlyDictionary<int, decimal> rateByCurrencyId,
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

            var currencyRate = rateByCurrencyId.TryGetValue(currencyId, out var foundRate)
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

    public sealed class ImportLicenceDataImportRequest
    {
        public DateTime Date { get; set; }
    }

    public sealed class ImportLicenceDataImportResult
    {
        public int Id { get; set; }
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? LicenceDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}

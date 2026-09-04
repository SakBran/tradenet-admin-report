using API.DBContext;
using API.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class BorderImportLicenceListingCurrencyTotalRow
{
    public string? Currency { get; set; }
    public int NoOfLicences { get; set; }
    public decimal TotalValue { get; set; }
}

/// <summary>
/// Builds the legacy BorderNewReport.rdlc currency and grand-total footer.
/// The query mirrors the Border Import Licence branch of sp_NewReport_pagination,
/// including both Pa Tha Ka and Individual Trading card holders.
/// </summary>
public static class BorderImportLicenceListingCurrencyTotals
{
    public static async Task<ReportCurrencyTotalsSummary> ExecuteAsync(
        TradeNetDbContext db,
        DateTime fromDate,
        DateTime toDate,
        int exportImportSectionId,
        string? companyRegistrationNo,
        int sakhanId)
    {
        ArgumentNullException.ThrowIfNull(db);

        var parameters = new[]
        {
            new SqlParameter("@FromDate", fromDate),
            new SqlParameter("@ToDate", toDate.Date),
            new SqlParameter("@ExportImportSectionId", exportImportSectionId),
            new SqlParameter("@CompanyRegistrationNo", companyRegistrationNo?.Trim() ?? string.Empty),
            new SqlParameter("@SakhanId", sakhanId),
        };

        const string sql = """
            SELECT
                ISNULL(reportRows.Currency, N'') AS Currency,
                COUNT(*) AS NoOfLicences,
                ISNULL(SUM(reportRows.Amount), 0) AS TotalValue
            FROM (
                SELECT
                    (SELECT TOP 1 currency.Code
                     FROM BorderImportLicenceItem item
                     INNER JOIN Currency currency ON item.CurrencyId = currency.Id
                     WHERE item.BorderImportLicenceId = licence.Id) AS Currency,
                    (SELECT ISNULL(SUM(item.Amount), 0)
                     FROM BorderImportLicenceItem item
                     WHERE item.BorderImportLicenceId = licence.Id) AS Amount
                FROM BorderImportLicence licence
                INNER JOIN PaThaKa company ON licence.PaThaKaId = company.Id
                INNER JOIN ExportImportSection section ON licence.ExportImportSectionId = section.Id
                INNER JOIN Sakhan sakhan ON licence.SakhanId = sakhan.Id
                WHERE licence.ApplyType = N'New'
                  AND licence.Status = N'Approved'
                  AND licence.CardType = N'Pa Tha Ka'
                  AND licence.CreatedDate >= @FromDate
                  AND licence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))
                  AND (@ExportImportSectionId = 0 OR licence.ExportImportSectionId = @ExportImportSectionId)
                  AND (@CompanyRegistrationNo = N'' OR company.CompanyRegistrationNo = @CompanyRegistrationNo)
                  AND (@SakhanId = 0 OR licence.SakhanId = @SakhanId)

                UNION ALL

                SELECT
                    (SELECT TOP 1 currency.Code
                     FROM BorderImportLicenceItem item
                     INNER JOIN Currency currency ON item.CurrencyId = currency.Id
                     WHERE item.BorderImportLicenceId = licence.Id) AS Currency,
                    (SELECT ISNULL(SUM(item.Amount), 0)
                     FROM BorderImportLicenceItem item
                     WHERE item.BorderImportLicenceId = licence.Id) AS Amount
                FROM BorderImportLicence licence
                INNER JOIN IndividualTrading company ON licence.IndividualTradingId = company.Id
                INNER JOIN ExportImportSection section ON licence.ExportImportSectionId = section.Id
                INNER JOIN Sakhan sakhan ON licence.SakhanId = sakhan.Id
                WHERE licence.ApplyType = N'New'
                  AND licence.Status = N'Approved'
                  AND licence.CardType = N'Individual Trading'
                  AND licence.CreatedDate >= @FromDate
                  AND licence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))
                  AND (@ExportImportSectionId = 0 OR licence.ExportImportSectionId = @ExportImportSectionId)
                  AND (@CompanyRegistrationNo = N'' OR company.TINNo = @CompanyRegistrationNo)
                  AND (@SakhanId = 0 OR licence.SakhanId = @SakhanId)
            ) reportRows
            GROUP BY ISNULL(reportRows.Currency, N'')
            """;

        var rows = await db.Database
            .SqlQueryRaw<BorderImportLicenceListingCurrencyTotalRow>(sql, parameters)
            .ToListAsync();

        return ToSummary(rows);
    }

    internal static ReportCurrencyTotalsSummary ToSummary(
        IEnumerable<BorderImportLicenceListingCurrencyTotalRow> rows)
    {
        var currencies = rows
            .OrderByDescending(row => row.NoOfLicences)
            .ThenBy(row => row.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(row => new ReportCurrencyTotal
            {
                Currency = row.Currency ?? string.Empty,
                NoOfLicences = row.NoOfLicences,
                TotalValue = row.TotalValue,
            })
            .ToList();

        return new ReportCurrencyTotalsSummary
        {
            Currencies = currencies,
            GrandTotalLicences = currencies.Sum(currency => currency.NoOfLicences),
        };
    }
}

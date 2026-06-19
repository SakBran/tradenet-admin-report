using API.DBContext;
using API.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class ImportLicenceListingCurrencyTotalRow
{
    public string? Currency { get; set; }
    public int NoOfLicences { get; set; }
    public decimal TotalValue { get; set; }
}

/// <summary>
/// Currency-grouped summary footer for the Import Licence New / Amendment listing
/// reports (legacy AmendReport.rdlc "Currency" group: per-currency licence count +
/// summed value, plus a grand total). The proc's filtering and per-licence projection
/// (TOP 1 item currency + TOP 1 item amount) mirror the grid query
/// (<c>sp_AmendReport_pagination</c> / <c>sp_AmendReport</c>) so the footer lines up with
/// the rows shown:
///   * Amend -> sp_AmendReport_pagination (ApplyType='Amend' + AmendRemarkId)
///   * New   -> sp_NewReport.ImportLicenceQuery (ApplyType='New', no AmendRemarkId)
/// </summary>
public static class ImportLicenceListingCurrencyTotals
{
    /// <summary>New / Amendment listing footer (<c>dbo.sp_ImportLicenceListingCurrencyTotals</c>).</summary>
    public static Task<ReportCurrencyTotalsSummary> ExecuteAsync(
        TradeNetDbContext db,
        string applyType,
        DateTime fromDate,
        DateTime toDate,
        int exportImportSectionId,
        string? companyRegistrationNo,
        int amendRemarkId)
    {
        ArgumentNullException.ThrowIfNull(db);

        var parameters = new[]
        {
            new SqlParameter("@ApplyType", applyType ?? string.Empty),
            new SqlParameter("@FromDate", fromDate),
            new SqlParameter("@ToDate", toDate),
            new SqlParameter("@ExportImportSectionId", exportImportSectionId),
            new SqlParameter("@CompanyRegistrationNo", companyRegistrationNo ?? string.Empty),
            new SqlParameter("@AmendRemarkId", amendRemarkId),
        };

        const string sql =
            "EXEC dbo.sp_ImportLicenceListingCurrencyTotals @ApplyType, @FromDate, @ToDate, " +
            "@ExportImportSectionId, @CompanyRegistrationNo, @AmendRemarkId";

        return RunAsync(db, sql, parameters);
    }

    private static async Task<ReportCurrencyTotalsSummary> RunAsync(
        TradeNetDbContext db,
        string sql,
        SqlParameter[] parameters)
    {
        List<ImportLicenceListingCurrencyTotalRow> rows;
        try
        {
            rows = await db.Database
                .SqlQueryRaw<ImportLicenceListingCurrencyTotalRow>(sql, parameters)
                .ToListAsync();
        }
        catch (SqlException)
        {
            // The footer is an enhancement on top of an already-working report. If the
            // proc has not been deployed yet (the .sql migrations are applied by hand),
            // degrade to "no footer" rather than 500-ing the whole report.
            return new ReportCurrencyTotalsSummary();
        }

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

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
/// (TOP 1 item currency + TOP 1 item amount) mirror the grid query so the footer lines up
/// with the rows shown. <c>applyType</c> selects the branch in
/// <c>dbo.sp_ImportLicenceListingCurrencyTotals</c>:
///   * "Amend"       -> sp_AmendReport_pagination (ApplyType='Amend' + AmendRemarkId)
///   * "Cancel"      -> sp_CancelReport_pagination (ApplyType='Cancel', no AmendRemarkId, &lt;= ToDate)
///   * "ActualAmend" -> sp_ActualAmendReport_pagination (ApplyType='Actual Amend' + AmendRemarkId)
///   * "New" (else)  -> sp_NewReport.ImportLicenceQuery (ApplyType='New', no AmendRemarkId)
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
        int amendRemarkId,
        string? auto = null,
        string? quota = null)
    {
        ArgumentNullException.ThrowIfNull(db);

        // @auto / @quota are only used by the New branch (the New grid filters on them); the
        // Amend / ActualAmend / Cancel branches have no such grid filter, so those callers
        // leave them at '' and the proc ignores them.
        var parameters = new[]
        {
            new SqlParameter("@ApplyType", applyType ?? string.Empty),
            new SqlParameter("@FromDate", fromDate),
            new SqlParameter("@ToDate", toDate),
            new SqlParameter("@ExportImportSectionId", exportImportSectionId),
            new SqlParameter("@CompanyRegistrationNo", companyRegistrationNo ?? string.Empty),
            new SqlParameter("@AmendRemarkId", amendRemarkId),
            new SqlParameter("@auto", auto ?? string.Empty),
            new SqlParameter("@quota", quota ?? string.Empty),
        };

        const string sql =
            "EXEC dbo.sp_ImportLicenceListingCurrencyTotals @ApplyType, @FromDate, @ToDate, " +
            "@ExportImportSectionId, @CompanyRegistrationNo, @AmendRemarkId, @auto, @quota";

        return RunAsync(db, sql, parameters);
    }

    /// <summary>Voucher report footer (<c>dbo.sp_ImportLicenceVoucherCurrencyTotals</c>).</summary>
    public static Task<ReportCurrencyTotalsSummary> ExecuteVoucherAsync(
        TradeNetDbContext db,
        DateTime fromDate,
        DateTime toDate,
        int exportImportSectionId,
        string? paymentType,
        string? applyType,
        string? companyRegistrationNo)
    {
        ArgumentNullException.ThrowIfNull(db);

        var parameters = new[]
        {
            new SqlParameter("@FromDate", fromDate),
            new SqlParameter("@ToDate", toDate),
            new SqlParameter("@ExportImportSectionId", exportImportSectionId),
            new SqlParameter("@PaymentType", paymentType ?? string.Empty),
            new SqlParameter("@ApplyType", applyType ?? string.Empty),
            new SqlParameter("@CompanyRegistrationNo", companyRegistrationNo ?? string.Empty),
        };

        const string sql =
            "EXEC dbo.sp_ImportLicenceVoucherCurrencyTotals @FromDate, @ToDate, " +
            "@ExportImportSectionId, @PaymentType, @ApplyType, @CompanyRegistrationNo";

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

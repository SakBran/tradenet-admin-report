using API.DBContext;
using API.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class ExportLicenceListingCurrencyTotalRow
{
    public string? Currency { get; set; }
    public int NoOfLicences { get; set; }
    public decimal TotalValue { get; set; }
}

/// <summary>
/// Currency-grouped summary footer for the Export Licence / Border Export Licence New / Amendment /
/// Actual Amendment / Cancellation / Voucher listing reports (legacy RDLC "Currency" group:
/// per-currency count + summed/first item value, plus a grand total). The Licence twin of
/// <see cref="ExportPermitListingCurrencyTotals"/>; keyed by FormType so it serves BOTH the
/// non-border (ExportLicence) and border (BorderExportLicence, Pa Tha Ka + Individual Trading card
/// types) families. The procs' filtering mirrors each grid query so the footer lines up with the rows:
///   * New                 -> sp_NewReport (ApplyType='New', SUM item amount)
///   * Amend / ActualAmend  -> sp_AmendReport / sp_ActualAmendReport (ApplyType match + AmendRemarkId, first item amount)
///   * Cancel               -> sp_CancelReport (ApplyType='Cancel', first item amount, no AmendRemarkId)
///   * Voucher              -> sp_VoucherReport (payment vouchers, PaymentDate/PaymentType/ApplyType, SUM item amount)
/// The ApplyType literal for Actual Amendment is 'Actual Amend' WITH a space.
/// </summary>
public static class ExportLicenceListingCurrencyTotals
{
    /// <summary>New / Amendment / Actual Amendment / Cancellation listing footer (<c>dbo.sp_ExportLicenceListingCurrencyTotals</c>).</summary>
    public static Task<ReportCurrencyTotalsSummary> ExecuteAsync(
        TradeNetDbContext db,
        string formType,
        string applyType,
        DateTime fromDate,
        DateTime toDate,
        int exportImportSectionId,
        string? companyRegistrationNo,
        int amendRemarkId,
        int sakhanId)
    {
        ArgumentNullException.ThrowIfNull(db);

        var parameters = new[]
        {
            new SqlParameter("@FormType", formType ?? string.Empty),
            new SqlParameter("@ApplyType", applyType ?? string.Empty),
            new SqlParameter("@FromDate", fromDate),
            new SqlParameter("@ToDate", toDate),
            new SqlParameter("@ExportImportSectionId", exportImportSectionId),
            new SqlParameter("@CompanyRegistrationNo", companyRegistrationNo ?? string.Empty),
            new SqlParameter("@AmendRemarkId", amendRemarkId),
            new SqlParameter("@SakhanId", sakhanId),
        };

        const string sql =
            "EXEC dbo.sp_ExportLicenceListingCurrencyTotals @FormType, @ApplyType, @FromDate, @ToDate, " +
            "@ExportImportSectionId, @CompanyRegistrationNo, @AmendRemarkId, @SakhanId";

        return RunAsync(db, sql, parameters);
    }

    /// <summary>Voucher report footer (<c>dbo.sp_ExportLicenceVoucherCurrencyTotals</c>).</summary>
    public static Task<ReportCurrencyTotalsSummary> ExecuteVoucherAsync(
        TradeNetDbContext db,
        string formType,
        DateTime fromDate,
        DateTime toDate,
        int exportImportSectionId,
        string? paymentType,
        string? applyType,
        string? companyRegistrationNo,
        int sakhanId)
    {
        ArgumentNullException.ThrowIfNull(db);

        var parameters = new[]
        {
            new SqlParameter("@FormType", formType ?? string.Empty),
            new SqlParameter("@FromDate", fromDate),
            new SqlParameter("@ToDate", toDate),
            new SqlParameter("@ExportImportSectionId", exportImportSectionId),
            new SqlParameter("@PaymentType", paymentType ?? string.Empty),
            new SqlParameter("@ApplyType", applyType ?? string.Empty),
            new SqlParameter("@CompanyRegistrationNo", companyRegistrationNo ?? string.Empty),
            new SqlParameter("@SakhanId", sakhanId),
        };

        const string sql =
            "EXEC dbo.sp_ExportLicenceVoucherCurrencyTotals @FormType, @FromDate, @ToDate, " +
            "@ExportImportSectionId, @PaymentType, @ApplyType, @CompanyRegistrationNo, @SakhanId";

        return RunAsync(db, sql, parameters);
    }

    private static async Task<ReportCurrencyTotalsSummary> RunAsync(
        TradeNetDbContext db,
        string sql,
        SqlParameter[] parameters)
    {
        List<ExportLicenceListingCurrencyTotalRow> rows;
        try
        {
            rows = await db.Database
                .SqlQueryRaw<ExportLicenceListingCurrencyTotalRow>(sql, parameters)
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

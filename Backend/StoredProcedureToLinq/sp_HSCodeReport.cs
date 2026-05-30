using API.DBContext;
using API.Model;
using API.Service.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_HSCodeReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string FilterType { get; set; } = string.Empty;
    public string HSCode { get; set; } = string.Empty;
    public int SakhanId { get; set; }
}

public sealed class sp_HSCodeReportResult
{
    public int? SakhanId { get; set; }
    public string? SectionCode { get; set; }
    public int HSCodeId { get; set; }
    public string HSCode { get; set; } = null!;
    public string? HSDescription { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string LicenceNo { get; set; } = null!;
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
}

public sealed class sp_HSCodeReportRow
{
    public string? SectionCode { get; set; }
    public int? HSCodeId { get; set; }
    public string? HSCode { get; set; }
    public string? HSDescription { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? LicenceNo { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? CompanyName { get; set; }
    public int TotalCount { get; set; }
}

public static class sp_HSCodeReport
{
    private const string New = "New";
    private const string Approved = "Approved";
    private const string PaThaKaCardType = "Pa Tha Ka";
    private const string IndividualTradingCardType = "Individual Trading";

    public static IQueryable<sp_HSCodeReportResult> Query(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return request.FormType switch
        {
            "Export Licence" => ExportLicenceRows(db, request),
            "Import Licence" => ImportLicenceRows(db, request),
            "Export Permit" => ExportPermitRows(db, request),
            "Import Permit" => ImportPermitRows(db, request),
            "Border Export Licence" => BorderExportLicenceRows(db, request),
            "Border Import Licence" => BorderImportLicenceRows(db, request),
            "Border Export Permit" => BorderExportPermitRows(db, request),
            "Border Import Permit" => BorderImportPermitRows(db, request),
            _ => EmptyRows(db)
        };
    }

    public static async Task<ApiResult<ReportAggregateResult>> CreateAggregateResultAsync(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request,
        ReportQueryRequest pagingRequest)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var source = await AggregateSourceRowsAsync(db, request);
        return ReportAggregationService.CreatePagedResult(
            source, ReportAggregateDimension.HSCode, includeSakhan: false, pagingRequest);
    }

    public static async Task<byte[]> CreateAggregateExcelWorkbookAsync(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request,
        ReportQueryRequest pagingRequest,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var source = await AggregateSourceRowsAsync(db, request);
        return await ReportAggregationService.CreateExcelWorkbookAsync(
            source, ReportAggregateDimension.HSCode, includeSakhan: false, pagingRequest, worksheetName);
    }

    /// <summary>
    /// Executes <c>dbo.sp_HSCodeReport_pagination</c> (DB-side wrapper over the untouched
    /// original). Used by the stored-procedure-truth aggregate path below.
    /// </summary>
    public static async Task<List<sp_HSCodeReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new[]
        {
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@FormType", request.FormType ?? string.Empty),
            new SqlParameter("@FilterType", request.FilterType ?? string.Empty),
            new SqlParameter("@HSCode", request.HSCode ?? string.Empty),
            new SqlParameter("@SakhanId", request.SakhanId),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        const string sql =
            "EXEC dbo.sp_HSCodeReport_pagination @FromDate, @ToDate, @FormType, @FilterType, @HSCode, " +
            "@SakhanId, @SortColumn, @SortOrder, @PageIndex, @PageSize";

        return await db.Database
            .SqlQueryRaw<sp_HSCodeReportRow>(sql, parameters)
            .ToListAsync();
    }

    /// <summary>
    /// Stored-procedure-truth aggregate (HSCode dimension), sourced from
    /// <c>sp_HSCodeReport_pagination</c> rather than the LINQ <see cref="Query"/>.
    /// </summary>
    public static async Task<ApiResult<ReportAggregateResult>> CreateAggregateResultFromProcAsync(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request,
        ReportQueryRequest pagingRequest)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var source = await ProcAggregateSourceRowsAsync(db, request);
        return ReportAggregationService.CreatePagedResult(
            source, ReportAggregateDimension.HSCode, includeSakhan: false, pagingRequest);
    }

    public static async Task<byte[]> CreateAggregateExcelWorkbookFromProcAsync(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request,
        ReportQueryRequest pagingRequest,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var source = await ProcAggregateSourceRowsAsync(db, request);
        return await ReportAggregationService.CreateExcelWorkbookAsync(
            source, ReportAggregateDimension.HSCode, includeSakhan: false, pagingRequest, worksheetName);
    }

    private static async Task<List<AggregateSourceRow>> ProcAggregateSourceRowsAsync(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        var rows = await ExecuteAsync(db, request, sortColumn: null, sortOrder: null, pageIndex: null, pageSize: null);

        return rows
            .Select(row => new AggregateSourceRow
            {
                HSCode = row.HSCode,
                HSDescription = row.HSDescription,
                CompanyName = row.CompanyName,
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                LicenceNo = row.LicenceNo ?? string.Empty,
                Amount = row.Amount ?? 0m,
                Currency = row.Currency,
            })
            .ToList();
    }

    private static async Task<List<AggregateSourceRow>> AggregateSourceRowsAsync(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        var rows = await Query(db, request).ToListAsync();

        return rows
            .Select(row => new AggregateSourceRow
            {
                HSCode = row.HSCode,
                HSDescription = row.HSDescription,
                CompanyName = row.CompanyName,
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                LicenceNo = row.LicenceNo,
                Amount = row.Amount,
                Currency = row.Currency,
            })
            .ToList();
    }

    private static IQueryable<sp_HSCodeReportResult> ExportLicenceRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from licence in db.ExportLicences
            join item in db.ExportLicenceItems on licence.Id equals item.ExportLicenceId
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ExportLicenceNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> ImportLicenceRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from licence in db.ImportLicences
            join item in db.ImportLicenceItems on licence.Id equals item.ImportLicenceId
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ImportLicenceNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> ExportPermitRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from permit in db.ExportPermits
            join item in db.ExportPermitItems on permit.Id equals item.ExportPermitId
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.LicenceDate >= request.FromDate
                && permit.LicenceDate <= request.ToDate
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = permit.ExportPermitNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> ImportPermitRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from permit in db.ImportPermits
            join item in db.ImportPermitItems on permit.Id equals item.ImportPermitId
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.LicenceDate >= request.FromDate
                && permit.LicenceDate <= request.ToDate
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = permit.ImportPermitNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> BorderExportLicenceRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        var paThaKaRows =
            from licence in db.BorderExportLicences
            join item in db.BorderExportLicenceItems on licence.Id equals item.BorderExportLicenceId
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == PaThaKaCardType
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            select new sp_HSCodeReportResult
            {
                SakhanId = licence.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ExportLicenceNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };

        var individualRows =
            from licence in db.BorderExportLicences
            join item in db.BorderExportLicenceItems on licence.Id equals item.BorderExportLicenceId
            join individualTrading in db.IndividualTradings on licence.IndividualTradingId equals individualTrading.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == IndividualTradingCardType
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            select new sp_HSCodeReportResult
            {
                SakhanId = licence.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ExportLicenceNo,
                CompanyRegistrationNo = individualTrading.Tinno,
                CompanyName = individualTrading.Name
            };

        return paThaKaRows.Concat(individualRows).OrderBy(row => row.HSCodeId);
    }

    private static IQueryable<sp_HSCodeReportResult> BorderImportLicenceRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        var paThaKaRows =
            from licence in db.BorderImportLicences
            join item in db.BorderImportLicenceItems on licence.Id equals item.BorderImportLicenceId
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == PaThaKaCardType
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            select new sp_HSCodeReportResult
            {
                SakhanId = licence.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ImportLicenceNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };

        var individualRows =
            from licence in db.BorderImportLicences
            join item in db.BorderImportLicenceItems on licence.Id equals item.BorderImportLicenceId
            join individualTrading in db.IndividualTradings on licence.IndividualTradingId equals individualTrading.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == IndividualTradingCardType
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            select new sp_HSCodeReportResult
            {
                SakhanId = licence.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ImportLicenceNo,
                CompanyRegistrationNo = individualTrading.Tinno,
                CompanyName = individualTrading.Name
            };

        return paThaKaRows.Concat(individualRows).OrderBy(row => row.HSCodeId);
    }

    private static IQueryable<sp_HSCodeReportResult> BorderExportPermitRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from permit in db.BorderExportPermits
            join item in db.BorderExportPermitItems on permit.Id equals item.BorderExportPermitId
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.LicenceDate >= request.FromDate
                && permit.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || permit.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SakhanId = permit.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = permit.ExportPermitNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> BorderImportPermitRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from permit in db.BorderImportPermits
            join item in db.BorderImportPermitItems on permit.Id equals item.BorderImportPermitId
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.LicenceDate >= request.FromDate
                && permit.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || permit.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SakhanId = permit.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = permit.ImportPermitNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> EmptyRows(TradeNetDbContext db)
    {
        return db.Hscodes
            .Where(_ => false)
            .Select(hsCode => new sp_HSCodeReportResult
            {
                SakhanId = hsCode.Id,
                SectionCode = hsCode.Code,
                HSCodeId = hsCode.Id,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = 0m,
                Currency = hsCode.Code,
                LicenceNo = hsCode.Code,
                CompanyRegistrationNo = hsCode.Code,
                CompanyName = hsCode.Description
            });
    }
}

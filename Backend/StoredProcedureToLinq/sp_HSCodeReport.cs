using API.DBContext;
using API.Model;
using API.Service.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_HSCodeReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string FilterType { get; set; } = string.Empty;
    public string HSCode { get; set; } = string.Empty;
    public int ExportImportSectionId { get; set; }
    public int SakhanId { get; set; }

    /// <summary>
    /// Group on (HS code, company) instead of (HS code, currency) -- the shape of the legacy
    /// HSCodeDetailReport.rdlc drill (rdlc:1263-1264). The old system decided this in the RDLC,
    /// not in SQL, so the summary and its drill ran the very same query; here the caller has to
    /// say which shape it wants, because a summary with an HS-code filter typed and a drill for
    /// that HS code arrive as the same parameters. Forces the LINQ path.
    /// </summary>
    public bool GroupByCompany { get; set; }

    /// <summary>
    /// Print the groups the way the legacy screen did (owner decision 2026-09-06, Border Import
    /// Permit By HS Code: byte-identical to the old report). Legacy dbo.sp_HSCodeReport ends with
    /// <c>ORDER BY HSCode.Id</c> and BorderHSCodeReport.rdlc / HSCodeDetailReport.rdlc group with
    /// no SortExpressions, so the old rows are in HS code ID order with each ID's groups in the
    /// order their first permit row arrived -- not in HS code string / currency / company-name
    /// order. Forces the LINQ path (the deployed procedure orders by the HS code string) and, for
    /// <see cref="GroupByCompany"/>, an in-memory first-appearance grouping.
    /// </summary>
    public bool LegacyOrder { get; set; }
}

public sealed class sp_HSCodeReportResult
{
    public int? SakhanId { get; set; }

    /// <summary>
    /// Ordering keys for <see cref="sp_HSCodeReportRequest.LegacyOrder"/>. Only the two oversea permit
    /// sources fill them (Import Permit and Export Permit -- the ones the Border By HS Code screens run
    /// bug-for-bug with Tradenet 2.0); every other source leaves them null.
    /// </summary>
    public DateTime? PermitCreatedDate { get; set; }
    public string? PermitId { get; set; }
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

public static partial class sp_HSCodeReport
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

        // The deployed sp_HSCodeReport_pagination trims @HSCode (LTRIM/RTRIM) before its
        // LIKE, so trim here too -- otherwise the LINQ footer below would match on a
        // different (padded) token than the grid rows and the Total could disagree.
        request.HSCode = request.HSCode?.Trim() ?? string.Empty;

        // Grand "Total No of License" footer: distinct licences across the whole filtered
        // set. A licence can span several HS codes, so this is deliberately NOT a sum of the
        // per-row NoOfLicences -- it ties out to the HSCode detail record count.
        var grandLicences = await Query(db, request)
            .Select(row => row.LicenceNo)
            .Distinct()
            .CountAsync();
        var columnTotals = new Dictionary<string, decimal> { ["noOfLicences"] = grandLicences };

        if (UsesAggregateStoredProcedure(request))
        {
            var pageIndex = Math.Max(0, pagingRequest.PageIndex);
            var pageSize = pagingRequest.PageSize <= 0 ? 10 : Math.Min(pagingRequest.PageSize, 1000);
            // Pass the REAL page size. The "one row more than a page" sentinel that
            // CreateFastPageFromRows needs is added inside the procedure (@FetchSize), because
            // inflating @PageSize here also inflated its OFFSET -- page 2 started at row 12 of a
            // 10-row page, so one row vanished at every page boundary and a 31-row report only
            // ever showed 29.
            var rows = await ExecuteAggregateStoredProcedureAsync(
                db,
                request,
                pageIndex,
                pageSize,
                pagingRequest.IncludeTotalCount);

            var data = rows.Select(row => new ReportAggregateResult
            {
                HSCode = row.HSCode,
                HSDescription = row.HSDescription,
                CompanyName = row.CompanyName,
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                Currency = row.Currency,
                NoOfLicences = row.NoOfLicences,
                TotalValue = row.TotalValue,
                TotalUSDValue = null,
            }).ToList();

            // Every branch except Export Licence computes COUNT(*) OVER() whether or not the
            // caller asked for it, so the exact total is usually sitting in the rows already and
            // the grid may as well have a working pager instead of a lower-bound estimate. It
            // also makes the page independent of the next-page sentinel, so this code is correct
            // against a database that has not had @FetchSize deployed yet.
            var totalCount = rows.FirstOrDefault()?.TotalCount;
            if (totalCount.HasValue)
            {
                var aggregateResult = ApiResult<ReportAggregateResult>.CreatePageFromRows(
                    // Trim the sentinel row a @FetchSize procedure adds to a fast page.
                    data.Count > pageSize ? data.Take(pageSize).ToList() : data,
                    totalCount.Value,
                    pageIndex,
                    pageSize,
                    null,
                    null,
                    pagingRequest.FilterColumn,
                    pagingRequest.FilterQuery);
                aggregateResult.ColumnTotals = columnTotals;
                return aggregateResult;
            }

            // Export Licence's @IncludeTotalCount=0 branch returns no count; fall back to the
            // sentinel, which that branch supplies via @FetchSize.
            var fastAggregateResult = ApiResult<ReportAggregateResult>.CreateFastPageFromRows(
                data,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
            fastAggregateResult.ColumnTotals = columnTotals;
            return fastAggregateResult;
        }

        if (request.LegacyOrder && request.GroupByCompany)
        {
            // The legacy HS Code detail drill: one HS code's rows, grouped in memory so the
            // groups keep first-appearance order and show the first row's company name
            // (HSCodeDetailReport.rdlc prints Fields!CompanyName.Value of the group = First()).
            var legacyGroups = await LegacyCompanyGroupsAsync(db, request);
            var legacyPageIndex = Math.Max(0, pagingRequest.PageIndex);
            var legacyPageSize = pagingRequest.PageSize <= 0 ? 10 : Math.Min(pagingRequest.PageSize, 1000);
            var legacyResult = ApiResult<ReportAggregateResult>.CreatePageFromRows(
                legacyGroups.Skip(legacyPageIndex * legacyPageSize).Take(legacyPageSize).ToList(),
                legacyGroups.Count,
                legacyPageIndex,
                legacyPageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
            legacyResult.ColumnTotals = columnTotals;
            return legacyResult;
        }

        var query = AggregateQuery(db, request);
        var sortColumn = GridSortColumnOrNull(pagingRequest.SortColumn);
        var fastResult = await ApiResult<ReportAggregateResult>.CreateFastPageAsync(
            query,
            pagingRequest.PageIndex,
            pagingRequest.PageSize,
            sortColumn,
            sortColumn == null ? null : pagingRequest.SortOrder,
            pagingRequest.FilterColumn,
            pagingRequest.FilterQuery,
            pagingRequest.IncludeTotalCount);
        fastResult.ColumnTotals = columnTotals;
        return fastResult;
    }

    /// <summary>
    /// The grid posts its config's <c>initialSortColumn</c> with every request, and the HS Code configs
    /// send 'SakhanId' -- a column <see cref="ReportAggregateResult"/> does not have. ApiResult.ApplySort
    /// throws NotSupportedException for an unknown property, so on the LINQ paths (a section filter, or
    /// <see cref="sp_HSCodeReportRequest.LegacyOrder"/>) that was an HTTP 500 for the report's very
    /// first page -- measured on production on 2026-09-07 for Border Import Permit By HS Code, whose
    /// legacy-order path is LINQ-only. An unknown column means "no explicit sort": the legacy order (or
    /// the query's own ORDER BY) stands, exactly as the procedure path has always ignored the column.
    /// A real column (a header the user clicked) is still honoured.
    /// </summary>
    private static string? GridSortColumnOrNull(string? sortColumn)
        => !string.IsNullOrWhiteSpace(sortColumn)
           && typeof(ReportAggregateResult).GetProperty(
               sortColumn, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase) != null
            ? sortColumn
            : null;

    private static bool UsesAggregateStoredProcedure(sp_HSCodeReportRequest request)
    {
        // The procedure cannot produce the (HS code, company) drill grouping for every
        // FormType, so a drill always takes the LINQ twin. It is a single HS code's rows.
        if (request.GroupByCompany)
        {
            return false;
        }

        // The deployed procedure orders by the HS code string; the legacy order is by HS code ID
        // with first-appearance ties, which only the LINQ twin produces.
        if (request.LegacyOrder)
        {
            return false;
        }

        return request.ExportImportSectionId == 0
            && request.FormType is ("Export Licence"
            or "Import Licence"
            or "Export Permit"
            or "Import Permit"
            or "Border Export Licence"
            or "Border Import Licence"
            or "Border Export Permit"
            or "Border Import Permit");
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

        var query = AggregateQuery(db, request);
        return await ExcelGenerator.CreateWorkbookAsync(query, pagingRequest, worksheetName);
    }

    public static async Task<List<ReportAggregateResult>> GetAggregateRowsAsync(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.LegacyOrder && request.GroupByCompany)
        {
            return await LegacyCompanyGroupsAsync(db, request);
        }

        return await AggregateQuery(db, request).ToListAsync();
    }

    /// <summary>
    /// The legacy HS Code detail drill's rows: the detail rows in legacy order (HS code ID, then
    /// the permits as created) grouped on (HSCodeId, CompanyRegistrationNo) -- HSCodeDetailReport.rdlc's
    /// key (rdlc:1263-1264) -- in first-appearance order. A drill is one HS code (or one prefix),
    /// so the rows are few; the grouping is done in memory to keep the RDLC's First() semantics.
    /// </summary>
    public static async Task<List<ReportAggregateResult>> LegacyCompanyGroupsAsync(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var rows = await Query(db, request)
            .OrderBy(row => row.HSCodeId)
            .ThenBy(row => row.PermitCreatedDate)
            .ThenBy(row => row.PermitId)
            .ToListAsync();

        return GroupLegacyCompanies(rows);
    }

    /// <summary>
    /// Pure grouping for <see cref="LegacyCompanyGroupsAsync"/>: <paramref name="rows"/> must
    /// already be in legacy order. Enumerable.GroupBy keeps first-appearance order of the keys.
    /// </summary>
    public static List<ReportAggregateResult> GroupLegacyCompanies(IEnumerable<sp_HSCodeReportResult> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return rows
            .GroupBy(row => new { row.HSCodeId, row.CompanyRegistrationNo })
            .Select(group => new ReportAggregateResult
            {
                HSCode = group.First().HSCode,
                HSDescription = group.First().HSDescription,
                CompanyName = group.First().CompanyName,
                CompanyRegistrationNo = group.Key.CompanyRegistrationNo,
                Currency = null,
                NoOfLicences = group.Select(row => row.LicenceNo).Distinct().Count(),
                TotalValue = null,
                TotalUSDValue = null,
            })
            .ToList();
    }

    /// <summary>
    /// The LINQ twin of sp_HSCodeReport_pagination: used for the Excel streaming path and
    /// whenever a section filter takes the report off the aggregate procedure. It must group
    /// exactly as the procedure does, or the grid and the .xlsx disagree.
    /// Public so tests can pin the grouping key with <c>ToQueryString()</c>.
    /// </summary>
    public static IQueryable<ReportAggregateResult> AggregateQuery(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.GroupByCompany)
        {
            // The legacy HS Code DETAIL drill: HSCodeDetailReport.rdlc groups on HSCodeId +
            // CompanyRegistrationNo only (rdlc:1263-1264) and renders HS Code / Description /
            // Company Name / No of Licences -- no Currency, no Total Value. Keying on the
            // registration number (not the name) is what the RDLC does, so a company whose
            // name was re-spelled between permits stays one row; leaving Currency out of the
            // key is what keeps a company with items in two currencies from appearing twice.
            return Query(db, request)
                .GroupBy(row => new
                {
                    row.HSCodeId,
                    row.HSCode,
                    row.CompanyRegistrationNo
                })
                .Select(group => new ReportAggregateResult
                {
                    HSCode = group.Key.HSCode,
                    HSDescription = group.Max(row => row.HSDescription),
                    CompanyName = group.Max(row => row.CompanyName),
                    CompanyRegistrationNo = group.Key.CompanyRegistrationNo,
                    Currency = null,
                    NoOfLicences = group
                        .Select(row => row.LicenceNo)
                        .Distinct()
                        .Count(),
                    TotalValue = null,
                    TotalUSDValue = null,
                })
                .OrderBy(row => row.HSCode)
                .ThenBy(row => row.CompanyName);
        }

        if (request.LegacyOrder)
        {
            // BorderHSCodeReport.rdlc groups on (HSCodeId, Currency) with no sort over rows the
            // legacy procedure returns ORDER BY HSCode.Id: groups in HS code ID order, and an
            // ID's currencies in the order their first permit row arrived (permits as created).
            // Decided BEFORE GroupsByCompany on purpose: the Border Export Permit screen runs the
            // oversea 'Export Permit' source, whose default (non-legacy) shape is the company split
            // that ExportPermitHSCodeDetailReport renders -- the legacy summary must not inherit it.
            return Query(db, request)
                .GroupBy(row => new
                {
                    row.HSCodeId,
                    row.HSCode,
                    row.HSDescription,
                    row.Currency
                })
                .Select(group => new
                {
                    group.Key,
                    NoOfLicences = group.Select(row => row.LicenceNo).Distinct().Count(),
                    TotalValue = group.Sum(row => row.Amount),
                    FirstCreated = group.Min(row => row.PermitCreatedDate),
                    FirstPermit = group.Min(row => row.PermitId),
                })
                .OrderBy(group => group.Key.HSCodeId)
                .ThenBy(group => group.FirstCreated)
                .ThenBy(group => group.FirstPermit)
                .Select(group => new ReportAggregateResult
                {
                    HSCode = group.Key.HSCode,
                    HSDescription = group.Key.HSDescription,
                    CompanyName = null,
                    CompanyRegistrationNo = null,
                    Currency = group.Key.Currency,
                    NoOfLicences = group.NoOfLicences,
                    TotalValue = group.TotalValue,
                    TotalUSDValue = null,
                });
        }

        if (GroupsByCompany(request))
        {
            return Query(db, request)
                .GroupBy(row => new
                {
                    row.HSCode,
                    row.HSDescription,
                    row.CompanyName,
                    row.CompanyRegistrationNo,
                    row.Currency
                })
                .Select(group => new ReportAggregateResult
                {
                    HSCode = group.Key.HSCode,
                    HSDescription = group.Key.HSDescription,
                    CompanyName = group.Key.CompanyName,
                    CompanyRegistrationNo = group.Key.CompanyRegistrationNo,
                    Currency = group.Key.Currency,
                    NoOfLicences = group
                        .Select(row => row.LicenceNo)
                        .Distinct()
                        .Count(),
                    TotalValue = group.Sum(row => row.Amount),
                    TotalUSDValue = null,
                })
                .OrderBy(row => row.HSCode)
                .ThenBy(row => row.CompanyName)
                .ThenBy(row => row.Currency);
        }

        return Query(db, request)
            .GroupBy(row => new
            {
                row.HSCodeId,
                row.HSCode,
                row.HSDescription,
                row.Currency
            })
            .Select(group => new ReportAggregateResult
            {
                HSCode = group.Key.HSCode,
                HSDescription = group.Key.HSDescription,
                CompanyName = null,
                CompanyRegistrationNo = null,
                Currency = group.Key.Currency,
                NoOfLicences = group
                    .Select(row => row.LicenceNo)
                    .Distinct()
                    .Count(),
                TotalValue = group.Sum(row => row.Amount),
                TotalUSDValue = null,
            })
            .OrderBy(row => row.HSCode)
            .ThenBy(row => row.Currency);
    }

    /// <summary>
    /// Whether the buyer company belongs in the grouping key. The legacy HSCodeReport.rdlc row
    /// group is (HSCodeId, Currency) — no company (rdlc:1152-1153) — while HSCodeDetailReport.rdlc
    /// adds the company (rdlc:1263-1264). Keeping the company in the Import Permit key split one
    /// HS code into one invisible row per buyer, each with a partial Total Value; the oversea
    /// ImportPermitByHSCodeReport has no separate drill, and the Border Import Permit drill (which
    /// also runs this FormType, bug-for-bug with Tradenet 2.0) asks for its shape explicitly via
    /// <see cref="sp_HSCodeReportRequest.GroupByCompany"/>. The remaining form types always need
    /// it: their *HSCodeDetailReport configs render Company Name off this same query.
    /// </summary>
    private static bool GroupsByCompany(sp_HSCodeReportRequest request)
    {
        if (request.GroupByCompany)
        {
            return true;
        }

        if (string.Equals(request.FormType, "Import Permit", StringComparison.Ordinal))
        {
            return false;
        }

        // Border Import Permit serves BOTH surfaces off this one query: the By HS Code summary
        // (BorderHSCodeReport.rdlc, grouped on HSCodeId+Currency, no company column) and the HS
        // Code detail drill (BorderImportPermitHSCodeDetailReport -> HSCodeDetailReport.rdlc,
        // which does render Company Name). The drill always carries an HS code, so an empty
        // HSCode is the summary. sp_HSCodeReport_pagination's Border Import Permit branch makes
        // exactly the same split -- keep the two in step or the grid and the .xlsx disagree.
        if (string.Equals(request.FormType, "Border Import Permit", StringComparison.Ordinal))
        {
            return !string.IsNullOrEmpty(request.HSCode);
        }

        return true;
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
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
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
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
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
                && (request.ExportImportSectionId == 0 || permit.ExportImportSectionId == request.ExportImportSectionId)
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
                CompanyName = paThaKa.CompanyName,
                PermitCreatedDate = permit.CreatedDate,
                PermitId = permit.Id
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
                && (request.ExportImportSectionId == 0 || permit.ExportImportSectionId == request.ExportImportSectionId)
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
                CompanyName = paThaKa.CompanyName,
                PermitCreatedDate = permit.CreatedDate,
                PermitId = permit.Id
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
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
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
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
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
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
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
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
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
                && (request.ExportImportSectionId == 0 || permit.ExportImportSectionId == request.ExportImportSectionId)
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
                && (request.ExportImportSectionId == 0 || permit.ExportImportSectionId == request.ExportImportSectionId)
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

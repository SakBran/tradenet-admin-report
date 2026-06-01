using API.DBContext;
using API.Model;
using API.Service.Reports;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public static class sp_ImportLicenceDetailReport_Fast
{
    private const string New = "New";
    private const string Approved = "Approved";
    private const string PaThaKaCardType = "Pa Tha Ka";
    private const string IndividualTradingCardType = "Individual Trading";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    public static async Task<ApiResult<sp_ImportLicenceDetailReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        ICountryCache countryCache,
        sp_ImportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(countryCache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        await countryCache.EnsureLoadedAsync();
        var countryNames = countryCache.Countries;
        var pageIndex = Math.Max(0, pagingRequest.PageIndex);
        var pageSize = pagingRequest.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(pagingRequest.PageSize, MaxPageSize);

        var rows = Rows(db, request);
        var totalCount = pagingRequest.IncludeTotalCount
            ? await CountRowsAsync(db, request)
            : (int?)null;

        var pageRows = await rows
            .Skip(pageIndex * pageSize)
            .Take(pageSize + (totalCount.HasValue ? 0 : 1))
            .ToListAsync();

        var results = pageRows
            .Select(row => row.ToResult(countryNames))
            .ToList();

        if (totalCount.HasValue)
        {
            return ApiResult<sp_ImportLicenceDetailReportResult>.CreatePageFromRows(
                results,
                totalCount.Value,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<sp_ImportLicenceDetailReportResult>.CreateFastPageFromRows(
            results,
            pageIndex,
            pageSize,
            null,
            null,
            pagingRequest.FilterColumn,
            pagingRequest.FilterQuery);
    }

    public static async Task<byte[]> CreateExcelWorkbookAsync(
        TradeNetDbContext db,
        ICountryCache countryCache,
        sp_ImportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(countryCache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        await countryCache.EnsureLoadedAsync();
        var countryNames = countryCache.Countries;
        var rows = await Rows(db, request).ToListAsync();

        var resolved = rows
            .Select(row => row.ToResult(countryNames))
            .ToList();

        return await ExcelGenerator.CreateWorkbookAsync(resolved.AsQueryable(), pagingRequest, worksheetName);
    }

    public static async Task<ApiResult<ReportAggregateResult>> CreateAggregateResultAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        ReportAggregateDimension dimension,
        bool includeSakhan)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var groups = await AggregateInSqlAsync(db, request, dimension, includeSakhan);
        return ReportAggregationService.CreatePagedResultFromGroups(groups, dimension, includeSakhan, pagingRequest);
    }

    public static async Task<byte[]> CreateAggregateExcelWorkbookAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        ReportAggregateDimension dimension,
        bool includeSakhan,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var groups = await AggregateInSqlAsync(db, request, dimension, includeSakhan);
        return await ReportAggregationService.CreateExcelWorkbookFromGroupsAsync(
            groups, dimension, includeSakhan, pagingRequest, worksheetName);
    }

    /// <summary>
    /// Import Licence By Section report. Groups the filtered detail rows by Section (+ Currency)
    /// entirely in SQL — GROUP BY, ORDER BY and OFFSET/FETCH paging all run on the server, so only
    /// one page of grouped rows is returned. Output columns: Section, No of Licences (distinct
    /// non-empty licence numbers), Total Value (summed amount) and Currency. Honours all the
    /// standard filters (date range, PaThaKa type, section, method, incoterm, seller country,
    /// company registration no, sakhan) via the shared <see cref="RowsUnordered"/> source.
    /// </summary>
    public static async Task<ApiResult<ReportAggregateResult>> CreateSectionPagedResultAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var groups = SectionGroups(db, request);

        var pageIndex = Math.Max(0, pagingRequest.PageIndex);
        var pageSize = pagingRequest.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(pagingRequest.PageSize, MaxPageSize);

        var totalCount = pagingRequest.IncludeTotalCount
            ? await groups.CountAsync()
            : (int?)null;

        var pageRows = await groups
            .Skip(pageIndex * pageSize)
            .Take(pageSize + (totalCount.HasValue ? 0 : 1))
            .Select(group => new ReportAggregateResult
            {
                SectionName = group.SectionName,
                NoOfLicences = group.NoOfLicences,
                TotalValue = group.TotalValue,
                Currency = group.Currency,
            })
            .ToListAsync();

        return totalCount.HasValue
            ? ApiResult<ReportAggregateResult>.CreatePageFromRows(
                pageRows, totalCount.Value, pageIndex, pageSize, null, null,
                pagingRequest.FilterColumn, pagingRequest.FilterQuery)
            : ApiResult<ReportAggregateResult>.CreateFastPageFromRows(
                pageRows, pageIndex, pageSize, null, null,
                pagingRequest.FilterColumn, pagingRequest.FilterQuery);
    }

    /// <summary>Excel export of the By Section report: all section groups (no paging), grouped in SQL.</summary>
    public static async Task<byte[]> CreateSectionExcelWorkbookAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var rows = await SectionGroups(db, request)
            .Select(group => new ReportAggregateResult
            {
                SectionName = group.SectionName,
                NoOfLicences = group.NoOfLicences,
                TotalValue = group.TotalValue,
                Currency = group.Currency,
            })
            .ToListAsync();

        return await ExcelGenerator.CreateWorkbookAsync(rows.AsQueryable(), pagingRequest, worksheetName);
    }

    /// <summary>
    /// The By Section grouped queryable: distinct non-empty licence count and summed amount per
    /// (Section, Currency), ordered by Section then Currency. Fully translated to SQL.
    /// </summary>
    private static IQueryable<SectionAggregateRow> SectionGroups(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return RowsUnordered(db, request)
            .GroupBy(row => new { row.SectionName, row.Currency })
            .Select(group => new SectionAggregateRow
            {
                SectionName = group.Key.SectionName,
                Currency = group.Key.Currency,
                NoOfLicences = group
                    .Select(x => x.LicenceNo == string.Empty ? null : x.LicenceNo)
                    .Distinct()
                    .Count(),
                TotalValue = group.Sum(x => x.Amount),
            })
            .OrderBy(row => row.SectionName)
            .ThenBy(row => row.Currency);
    }

    /// <summary>
    /// Groups the detail rows in SQL (GROUP BY) so only the grouped rows are returned, instead of
    /// materializing the entire un-paged detail set in memory and grouping in C#. Counts and sums
    /// match <see cref="ReportAggregationService.Aggregate"/>: distinct non-empty licence numbers
    /// and summed amounts per group. Country names are not needed by aggregates, so they are never
    /// resolved.
    /// </summary>
    private static async Task<List<ReportAggregateResult>> AggregateInSqlAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        ReportAggregateDimension dimension,
        bool includeSakhan)
    {
        var source = RowsUnordered(db, request);

        List<AggregateGroupRow> groups = dimension switch
        {
            ReportAggregateDimension.Section => await source
                .GroupBy(row => new { Label = row.SectionName, Sakhan = includeSakhan ? row.SakhanCode : null, row.Currency })
                .Select(g => new AggregateGroupRow
                {
                    Label = g.Key.Label,
                    SakhanCode = g.Key.Sakhan,
                    Currency = g.Key.Currency,
                    NoOfLicences = g.Select(x => x.LicenceNo == string.Empty ? null : x.LicenceNo).Distinct().Count(),
                    TotalValue = g.Sum(x => x.Amount),
                })
                .ToListAsync(),

            ReportAggregateDimension.Method => await source
                .GroupBy(row => new { Label = row.MethodName, Sakhan = includeSakhan ? row.SakhanCode : null, row.Currency })
                .Select(g => new AggregateGroupRow
                {
                    Label = g.Key.Label,
                    SakhanCode = g.Key.Sakhan,
                    Currency = g.Key.Currency,
                    NoOfLicences = g.Select(x => x.LicenceNo == string.Empty ? null : x.LicenceNo).Distinct().Count(),
                    TotalValue = g.Sum(x => x.Amount),
                })
                .ToListAsync(),

            ReportAggregateDimension.Country => await source
                .GroupBy(row => new { Label = row.SellerCountry, Sakhan = includeSakhan ? row.SakhanCode : null, row.Currency })
                .Select(g => new AggregateGroupRow
                {
                    Label = g.Key.Label,
                    SakhanCode = g.Key.Sakhan,
                    Currency = g.Key.Currency,
                    NoOfLicences = g.Select(x => x.LicenceNo == string.Empty ? null : x.LicenceNo).Distinct().Count(),
                    TotalValue = g.Sum(x => x.Amount),
                })
                .ToListAsync(),

            ReportAggregateDimension.Company => await source
                .GroupBy(row => new { row.CompanyName, row.CompanyRegistrationNo, Sakhan = includeSakhan ? row.SakhanCode : null, row.Currency })
                .Select(g => new AggregateGroupRow
                {
                    CompanyName = g.Key.CompanyName,
                    CompanyRegistrationNo = g.Key.CompanyRegistrationNo,
                    SakhanCode = g.Key.Sakhan,
                    Currency = g.Key.Currency,
                    NoOfLicences = g.Select(x => x.LicenceNo == string.Empty ? null : x.LicenceNo).Distinct().Count(),
                    TotalValue = g.Sum(x => x.Amount),
                })
                .ToListAsync(),

            ReportAggregateDimension.HSCode => await source
                .GroupBy(row => new { Label = row.HSCode, row.CompanyName, row.CompanyRegistrationNo, row.HSDescription, Sakhan = includeSakhan ? row.SakhanCode : null, row.Currency })
                .Select(g => new AggregateGroupRow
                {
                    Label = g.Key.Label,
                    CompanyName = g.Key.CompanyName,
                    CompanyRegistrationNo = g.Key.CompanyRegistrationNo,
                    HSDescription = g.Key.HSDescription,
                    SakhanCode = g.Key.Sakhan,
                    Currency = g.Key.Currency,
                    NoOfLicences = g.Select(x => x.LicenceNo == string.Empty ? null : x.LicenceNo).Distinct().Count(),
                    TotalValue = g.Sum(x => x.Amount),
                })
                .ToListAsync(),

            ReportAggregateDimension.Daily => await source
                .GroupBy(row => new { Date = (DateTime?)row.LicenceDate!.Value.Date, Sakhan = includeSakhan ? row.SakhanCode : null, row.Currency })
                .Select(g => new AggregateGroupRow
                {
                    Date = g.Key.Date,
                    SakhanCode = g.Key.Sakhan,
                    Currency = g.Key.Currency,
                    NoOfLicences = g.Select(x => x.LicenceNo == string.Empty ? null : x.LicenceNo).Distinct().Count(),
                    TotalValue = g.Sum(x => x.Amount),
                })
                .ToListAsync(),

            // TotalValue (and any other dimension): group by currency (plus Sakhan) only.
            _ => await source
                .GroupBy(row => new { Sakhan = includeSakhan ? row.SakhanCode : null, row.Currency })
                .Select(g => new AggregateGroupRow
                {
                    SakhanCode = g.Key.Sakhan,
                    Currency = g.Key.Currency,
                    NoOfLicences = g.Select(x => x.LicenceNo == string.Empty ? null : x.LicenceNo).Distinct().Count(),
                    TotalValue = g.Sum(x => x.Amount),
                })
                .ToListAsync(),
        };

        return groups.Select(group => MapGroup(group, dimension, includeSakhan)).ToList();
    }

    private static ReportAggregateResult MapGroup(
        AggregateGroupRow group,
        ReportAggregateDimension dimension,
        bool includeSakhan)
    {
        var isCompanyOrHsCode = dimension is ReportAggregateDimension.Company or ReportAggregateDimension.HSCode;

        return new ReportAggregateResult
        {
            SakhanCode = includeSakhan ? group.SakhanCode : null,
            SectionName = dimension == ReportAggregateDimension.Section ? group.Label : null,
            MethodName = dimension == ReportAggregateDimension.Method ? group.Label : null,
            Country = dimension == ReportAggregateDimension.Country ? group.Label : null,
            CompanyName = isCompanyOrHsCode ? group.CompanyName : null,
            CompanyRegistrationNo = isCompanyOrHsCode ? group.CompanyRegistrationNo : null,
            HSCode = dimension == ReportAggregateDimension.HSCode ? group.Label : null,
            HSDescription = dimension == ReportAggregateDimension.HSCode ? group.HSDescription : null,
            Date = dimension == ReportAggregateDimension.Daily
                ? group.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null,
            Currency = group.Currency,
            NoOfLicences = group.NoOfLicences,
            TotalValue = group.TotalValue,
            TotalUSDValue = null,
        };
    }

    private static IQueryable<ImportLicenceDetailFastRow> Rows(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return RowsUnordered(db, request).OrderBy(row => row.CreatedDate);
    }

    private static IQueryable<ImportLicenceDetailFastRow> RowsUnordered(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return request.Type switch
        {
            "Oversea" => OverseaRows(db, request),
            "Border" => BorderPaThaKaRows(db, request)
                .Concat(BorderIndividualTradingRows(db, request)),
            _ => OverseaRows(db, request)
                .Where(_ => false)
        };
    }

    /// <summary>
    /// Total row count matching the same filters as <see cref="RowsUnordered"/>, but joining only the
    /// tables that affect cardinality (the licence-item fan-out) or back a filter (PaThaKa /
    /// IndividualTrading). The lookup joins (PaThaKaType/Unit/Currency/HSCode/Section/Countries/
    /// Method/Incoterm/Sakhan) are FK=PK on NOT NULL columns, so they are 1:1 and do not change the
    /// count — dropping them avoids materializing the full join just to count rows. PaThaKa type is
    /// filtered directly on PaThaKa.PaThaKaTypeId / IndividualTrading.PaThaKaTypeId.
    /// </summary>
    private static async Task<int> CountRowsAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return request.Type switch
        {
            "Oversea" => await CountOverseaAsync(db, request),
            "Border" => await CountBorderPaThaKaAsync(db, request)
                + await CountBorderIndividualTradingAsync(db, request),
            _ => 0,
        };
    }

    private static Task<int> CountOverseaAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return (
            from licence in db.ImportLicences.AsNoTracking()
            join paThaKa in db.PaThaKas.AsNoTracking() on licence.PaThaKaId equals paThaKa.Id
            join item in db.ImportLicenceItems.AsNoTracking() on licence.Id equals item.ImportLicenceId
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.ImportLicenceNo != string.Empty
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKa.PaThaKaTypeId == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                && (request.SellerCountryId == 0 || licence.SellerCountryId == request.SellerCountryId)
            select 1).CountAsync();
    }

    private static Task<int> CountBorderPaThaKaAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return (
            from licence in db.BorderImportLicences.AsNoTracking()
            join paThaKa in db.PaThaKas.AsNoTracking() on licence.PaThaKaId equals paThaKa.Id
            join item in db.BorderImportLicenceItems.AsNoTracking() on licence.Id equals item.BorderImportLicenceId
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == PaThaKaCardType
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKa.PaThaKaTypeId == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                && (request.SellerCountryId == 0 || licence.SellerCountryId == request.SellerCountryId)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select 1).CountAsync();
    }

    private static Task<int> CountBorderIndividualTradingAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return (
            from licence in db.BorderImportLicences.AsNoTracking()
            join individualTrading in db.IndividualTradings.AsNoTracking() on licence.IndividualTradingId equals individualTrading.Id
            join item in db.BorderImportLicenceItems.AsNoTracking() on licence.Id equals item.BorderImportLicenceId
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == IndividualTradingCardType
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || individualTrading.Tinno == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || individualTrading.PaThaKaTypeId == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                && (request.SellerCountryId == 0 || licence.SellerCountryId == request.SellerCountryId)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select 1).CountAsync();
    }

    private static IQueryable<ImportLicenceDetailFastRow> OverseaRows(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return
            from licence in db.ImportLicences.AsNoTracking()
            join paThaKa in db.PaThaKas.AsNoTracking() on licence.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes.AsNoTracking() on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.ImportLicenceItems.AsNoTracking() on licence.Id equals item.ImportLicenceId
            join unit in db.Units.AsNoTracking() on item.UnitId equals unit.Id
            join currency in db.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes.AsNoTracking() on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections.AsNoTracking() on licence.ExportImportSectionId equals section.Id
            join sellerCountry in db.Countries.AsNoTracking() on licence.SellerCountryId equals sellerCountry.Id
            join method in db.ExportImportMethods.AsNoTracking() on licence.ExportImportMethodId equals method.Id
            join incoterm in db.ExportImportIncoterms.AsNoTracking() on licence.ExportImportIncotermId equals incoterm.Id
            where request.Type == "Oversea"
                && licence.ApplyType == New
                && licence.Status == Approved
                && licence.ImportLicenceNo != string.Empty
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKaType.Id == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                && (request.SellerCountryId == 0 || licence.SellerCountryId == request.SellerCountryId)
            select new ImportLicenceDetailFastRow
            {
                CreatedDate = licence.CreatedDate,
                PaThaKaTypeId = paThaKaType.Id,
                PaThaKaTypeCode = paThaKaType.Code,
                PaThaKaTypeName = paThaKaType.Description,
                ExportImportSectionId = licence.ExportImportSectionId,
                ExportImportMethodId = licence.ExportImportMethodId,
                ExportImportIncotermId = licence.ExportImportIncotermId,
                SellerCountryId = licence.SellerCountryId,
                SectionCode = section.Code,
                SectionName = section.Name,
                LicenceNo = licence.ImportLicenceNo,
                LicenceDate = licence.IssuedDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                SellerName = licence.SellerName,
                SellerAddress = licence.SellerAddress,
                SellerCountry = sellerCountry.Name,
                PortofDischarge = licence.PortofDischarge,
                LastDate = licence.LastDate,
                MethodName = method.Name,
                ConsignedCountryIds = licence.ConsignedCountryId,
                CountryofOriginIds = licence.CountryofOriginId,
                HSCode = hsCode.Code,
                HSDescription = item.Description,
                Unit = unit.Code,
                Price = item.Price,
                Quantity = item.Quantity,
                Amount = item.Amount,
                Currency = currency.Code,
                Conditions = licence.Remark,
                ApplicationNo = licence.ApplicationNo,
                ApplicationDate = licence.ApplicationDate,
                FESCNo = licence.Fescno,
                CommodityType = licence.CommodityType,
                ApproveDate = licence.ApproveDate
            };
    }

    private static IQueryable<ImportLicenceDetailFastRow> BorderPaThaKaRows(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return
            from licence in db.BorderImportLicences.AsNoTracking()
            join paThaKa in db.PaThaKas.AsNoTracking() on licence.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes.AsNoTracking() on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderImportLicenceItems.AsNoTracking() on licence.Id equals item.BorderImportLicenceId
            join unit in db.Units.AsNoTracking() on item.UnitId equals unit.Id
            join currency in db.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes.AsNoTracking() on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections.AsNoTracking() on licence.ExportImportSectionId equals section.Id
            join sellerCountry in db.Countries.AsNoTracking() on licence.SellerCountryId equals sellerCountry.Id
            join method in db.ExportImportMethods.AsNoTracking() on licence.ExportImportMethodId equals method.Id
            join incoterm in db.ExportImportIncoterms.AsNoTracking() on licence.ExportImportIncotermId equals incoterm.Id
            join sakhan in db.Sakhans.AsNoTracking() on licence.SakhanId equals sakhan.Id
            where request.Type == "Border"
                && licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == PaThaKaCardType
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKaType.Id == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                && (request.SellerCountryId == 0 || licence.SellerCountryId == request.SellerCountryId)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select new ImportLicenceDetailFastRow
            {
                CreatedDate = licence.CreatedDate,
                PaThaKaTypeId = paThaKaType.Id,
                PaThaKaTypeCode = paThaKaType.Code,
                PaThaKaTypeName = paThaKaType.Description,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name,
                ExportImportSectionId = licence.ExportImportSectionId,
                ExportImportMethodId = licence.ExportImportMethodId,
                ExportImportIncotermId = licence.ExportImportIncotermId,
                SellerCountryId = licence.SellerCountryId,
                SectionCode = section.Code,
                SectionName = section.Name,
                LicenceNo = licence.ImportLicenceNo,
                LicenceDate = licence.IssuedDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                SellerName = licence.SellerName,
                SellerAddress = licence.SellerAddress,
                SellerCountry = sellerCountry.Name,
                PortofDischarge = licence.PortofDischarge,
                LastDate = licence.LastDate,
                MethodName = method.Name,
                ConsignedCountryIds = licence.ConsignedCountryId,
                CountryofOriginIds = licence.CountryofOriginId,
                HSCode = hsCode.Code,
                HSDescription = item.Description,
                Unit = unit.Code,
                Price = item.Price,
                Quantity = item.Quantity,
                Amount = item.Amount,
                Currency = currency.Code,
                Conditions = licence.Remark,
                ApplicationNo = licence.ApplicationNo,
                ApplicationDate = licence.ApplicationDate,
                FESCNo = licence.Fescno,
                CommodityType = licence.CommodityType,
                ApproveDate = licence.ApproveDate
            };
    }

    private static IQueryable<ImportLicenceDetailFastRow> BorderIndividualTradingRows(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return
            from licence in db.BorderImportLicences.AsNoTracking()
            join individualTrading in db.IndividualTradings.AsNoTracking() on licence.IndividualTradingId equals individualTrading.Id
            join paThaKaType in db.PaThaKaTypes.AsNoTracking() on individualTrading.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderImportLicenceItems.AsNoTracking() on licence.Id equals item.BorderImportLicenceId
            join unit in db.Units.AsNoTracking() on item.UnitId equals unit.Id
            join currency in db.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes.AsNoTracking() on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections.AsNoTracking() on licence.ExportImportSectionId equals section.Id
            join sellerCountry in db.Countries.AsNoTracking() on licence.SellerCountryId equals sellerCountry.Id
            join method in db.ExportImportMethods.AsNoTracking() on licence.ExportImportMethodId equals method.Id
            join incoterm in db.ExportImportIncoterms.AsNoTracking() on licence.ExportImportIncotermId equals incoterm.Id
            join sakhan in db.Sakhans.AsNoTracking() on licence.SakhanId equals sakhan.Id
            where request.Type == "Border"
                && licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == IndividualTradingCardType
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || individualTrading.Tinno == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKaType.Id == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                && (request.SellerCountryId == 0 || licence.SellerCountryId == request.SellerCountryId)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select new ImportLicenceDetailFastRow
            {
                CreatedDate = licence.CreatedDate,
                PaThaKaTypeId = paThaKaType.Id,
                PaThaKaTypeCode = paThaKaType.Code,
                PaThaKaTypeName = paThaKaType.Description,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name,
                ExportImportSectionId = licence.ExportImportSectionId,
                ExportImportMethodId = licence.ExportImportMethodId,
                ExportImportIncotermId = licence.ExportImportIncotermId,
                SellerCountryId = licence.SellerCountryId,
                SectionCode = section.Code,
                SectionName = section.Name,
                LicenceNo = licence.ImportLicenceNo,
                LicenceDate = licence.IssuedDate,
                CompanyRegistrationNo = individualTrading.Tinno,
                CompanyName = individualTrading.Name,
                UnitLevel = individualTrading.UnitLevel,
                StreetNumberStreetName = individualTrading.StreetNumberStreetName,
                QuarterCityTownship = individualTrading.QuarterCityTownship,
                State = individualTrading.State,
                Country = individualTrading.Country,
                PostalCode = individualTrading.PostalCode,
                SellerName = licence.SellerName,
                SellerAddress = licence.SellerAddress,
                SellerCountry = sellerCountry.Name,
                PortofDischarge = licence.PortofDischarge,
                LastDate = licence.LastDate,
                MethodName = method.Name,
                ConsignedCountryIds = licence.ConsignedCountryId,
                CountryofOriginIds = licence.CountryofOriginId,
                HSCode = hsCode.Code,
                HSDescription = item.Description,
                Unit = unit.Code,
                Price = item.Price,
                Quantity = item.Quantity,
                Amount = item.Amount,
                Currency = currency.Code,
                Conditions = licence.Remark,
                ApplicationNo = licence.ApplicationNo,
                ApplicationDate = licence.ApplicationDate,
                FESCNo = licence.Fescno,
                CommodityType = licence.CommodityType,
                ApproveDate = licence.ApproveDate
            };
    }

    /// <summary>Intermediate shape returned by the By Section SQL GROUP BY.</summary>
    private sealed class SectionAggregateRow
    {
        public string? SectionName { get; init; }
        public string? Currency { get; init; }
        public int NoOfLicences { get; init; }
        public decimal TotalValue { get; init; }
    }

    /// <summary>Intermediate shape returned by the SQL GROUP BY; mapped to <see cref="ReportAggregateResult"/>.</summary>
    private sealed class AggregateGroupRow
    {
        public string? Label { get; init; }
        public string? CompanyName { get; init; }
        public string? CompanyRegistrationNo { get; init; }
        public string? HSDescription { get; init; }
        public DateTime? Date { get; init; }
        public string? SakhanCode { get; init; }
        public string? Currency { get; init; }
        public int NoOfLicences { get; init; }
        public decimal TotalValue { get; init; }
    }

    private sealed class ImportLicenceDetailFastRow
    {
        public DateTime? CreatedDate { get; init; }
        public int PaThaKaTypeId { get; init; }
        public string PaThaKaTypeCode { get; init; } = null!;
        public string PaThaKaTypeName { get; init; } = null!;
        public int? SakhanId { get; init; }
        public string? SakhanCode { get; init; }
        public string? SakhanName { get; init; }
        public int ExportImportSectionId { get; init; }
        public int ExportImportMethodId { get; init; }
        public int ExportImportIncotermId { get; init; }
        public int SellerCountryId { get; init; }
        public string SectionCode { get; init; } = null!;
        public string SectionName { get; init; } = null!;
        public string LicenceNo { get; init; } = null!;
        public DateTime? LicenceDate { get; init; }
        public string CompanyRegistrationNo { get; init; } = null!;
        public string CompanyName { get; init; } = null!;
        public string? UnitLevel { get; init; }
        public string StreetNumberStreetName { get; init; } = null!;
        public string QuarterCityTownship { get; init; } = null!;
        public string State { get; init; } = null!;
        public string Country { get; init; } = null!;
        public string? PostalCode { get; init; }
        public string SellerName { get; init; } = null!;
        public string SellerAddress { get; init; } = null!;
        public string? SellerCountry { get; init; }
        public string PortofDischarge { get; init; } = null!;
        public DateTime? LastDate { get; init; }
        public string MethodName { get; init; } = null!;
        public string? ConsignedCountryIds { get; init; }
        public string? CountryofOriginIds { get; init; }
        public string HSCode { get; init; } = null!;
        public string? HSDescription { get; init; }
        public string? Unit { get; init; }
        public decimal Price { get; init; }
        public decimal Quantity { get; init; }
        public decimal Amount { get; init; }
        public string? Currency { get; init; }
        public string? Conditions { get; init; }
        public string ApplicationNo { get; init; } = null!;
        public DateTime ApplicationDate { get; init; }
        public string? FESCNo { get; init; }
        public string? CommodityType { get; init; }
        public DateTime? ApproveDate { get; init; }

        public sp_ImportLicenceDetailReportResult ToResult(IReadOnlyList<ReportLookupEntry> countries)
        {
            return new sp_ImportLicenceDetailReportResult
            {
                PaThaKaTypeId = PaThaKaTypeId,
                PaThaKaTypeCode = PaThaKaTypeCode,
                PaThaKaTypeName = PaThaKaTypeName,
                SakhanId = SakhanId,
                SakhanCode = SakhanCode,
                SakhanName = SakhanName,
                ExportImportSectionId = ExportImportSectionId,
                ExportImportMethodId = ExportImportMethodId,
                ExportImportIncotermId = ExportImportIncotermId,
                SellerCountryId = SellerCountryId,
                SectionCode = SectionCode,
                SectionName = SectionName,
                LicenceNo = LicenceNo,
                LicenceDate = LicenceDate,
                CompanyRegistrationNo = CompanyRegistrationNo,
                CompanyName = CompanyName,
                UnitLevel = UnitLevel,
                StreetNumberStreetName = StreetNumberStreetName,
                QuarterCityTownship = QuarterCityTownship,
                State = State,
                Country = Country,
                PostalCode = PostalCode,
                SellerName = SellerName,
                SellerAddress = SellerAddress,
                SellerCountry = SellerCountry,
                PortofDischarge = PortofDischarge,
                LastDate = LastDate,
                MethodName = MethodName,
                ConsignedCountry = ReportLookupCache.ResolveCsv(ConsignedCountryIds, countries),
                CountryofOrigin = ReportLookupCache.ResolveCsv(CountryofOriginIds, countries),
                HSCode = HSCode,
                HSDescription = HSDescription,
                Unit = Unit,
                Price = Price,
                Quantity = Quantity,
                Amount = Amount,
                Currency = Currency,
                Conditions = Conditions,
                ApplicationNo = ApplicationNo,
                ApplicationDate = ApplicationDate,
                FESCNo = FESCNo,
                CommodityType = CommodityType,
                ApproveDate = ApproveDate
            };
        }
    }
}

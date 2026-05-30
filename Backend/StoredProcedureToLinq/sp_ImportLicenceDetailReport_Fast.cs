using API.DBContext;
using API.Model;
using API.Service.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
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
        IMemoryCache cache,
        sp_ImportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var countryNames = await ReportLookupCache.GetCountryNamesAsync(db, cache);
        var pageIndex = Math.Max(0, pagingRequest.PageIndex);
        var pageSize = pagingRequest.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(pagingRequest.PageSize, MaxPageSize);

        var rows = Rows(db, request);
        var totalCount = pagingRequest.IncludeTotalCount
            ? await rows.CountAsync()
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
        IMemoryCache cache,
        sp_ImportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var countryNames = await ReportLookupCache.GetCountryNamesAsync(db, cache);
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

        var source = await AggregateSourceRowsAsync(db, request);
        return ReportAggregationService.CreatePagedResult(source, dimension, includeSakhan, pagingRequest);
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

        var source = await AggregateSourceRowsAsync(db, request);
        return await ReportAggregationService.CreateExcelWorkbookAsync(
            source, dimension, includeSakhan, pagingRequest, worksheetName);
    }

    private static async Task<List<AggregateSourceRow>> AggregateSourceRowsAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        var rows = await sp_ImportLicenceDetailReport.ExecuteAsync(
            db, request, sortColumn: null, sortOrder: null, pageIndex: null, pageSize: null);

        return rows
            .Select(row => new AggregateSourceRow
            {
                SakhanCode = null,
                SakhanName = null,
                SectionName = row.SectionName,
                MethodName = row.MethodName,
                Country = row.SellerCountry,
                CompanyName = row.CompanyName,
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                HSCode = row.HSCode,
                HSDescription = row.HSDescription,
                LicenceNo = row.LicenceNo ?? string.Empty,
                LicenceDate = row.LicenceDate,
                Amount = row.Amount ?? 0m,
                Currency = row.Currency,
            })
            .ToList();
    }

    private static IQueryable<ImportLicenceDetailFastRow> Rows(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return request.Type switch
        {
            "Oversea" => OverseaRows(db, request)
                .OrderBy(row => row.CreatedDate),
            "Border" => BorderPaThaKaRows(db, request)
                .Concat(BorderIndividualTradingRows(db, request))
                .OrderBy(row => row.CreatedDate),
            _ => OverseaRows(db, request)
                .Where(_ => false)
                .OrderBy(row => row.CreatedDate)
        };
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

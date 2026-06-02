using API.DBContext;
using API.Model;
using API.Service.ExcelExport;
using API.Service.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public static class sp_ExportLicenceDetailReport_Fast
{
    private const string New = "New";
    private const string Approved = "Approved";
    private const string PaThaKaCardType = "Pa Tha Ka";
    private const string IndividualTradingCardType = "Individual Trading";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    public static async Task<ApiResult<sp_ExportLicenceDetailReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_ExportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var ports = await ReportLookupCache.GetPortNamesAsync(db, cache);
        var countries = await ReportLookupCache.GetCountryNamesAsync(db, cache);

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
            .Select(row => row.ToResult(ports, countries))
            .ToList();

        if (totalCount.HasValue)
        {
            return ApiResult<sp_ExportLicenceDetailReportResult>.CreatePageFromRows(
                results,
                totalCount.Value,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<sp_ExportLicenceDetailReportResult>.CreateFastPageFromRows(
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
        sp_ExportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var ports = await ReportLookupCache.GetPortNamesAsync(db, cache);
        var countries = await ReportLookupCache.GetCountryNamesAsync(db, cache);

        var rows = await Rows(db, request).ToListAsync();

        var resolved = rows
            .Select(row => row.ToResult(ports, countries))
            .ToList();

        return await ExcelGenerator.CreateWorkbookAsync(resolved.AsQueryable(), pagingRequest, worksheetName);
    }

    public static async IAsyncEnumerable<List<sp_ExportLicenceDetailReportResult>> StreamResolvedChunksAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_ExportLicenceDetailReportRequest request,
        int chunkSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var ports = await ReportLookupCache.GetPortNamesAsync(db, cache);
        var countries = await ReportLookupCache.GetCountryNamesAsync(db, cache);

        await foreach (var rawChunk in Rows(db, request).AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
        {
            yield return rawChunk.Select(row => row.ToResult(ports, countries)).ToList();
        }
    }

    public static async Task<ApiResult<ReportAggregateResult>> CreateAggregateResultAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
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
        sp_ExportLicenceDetailReportRequest request,
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

    public static async Task<List<ReportAggregateResult>> GetAggregateRowsAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
        ReportAggregateDimension dimension,
        bool includeSakhan)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var source = await AggregateSourceRowsAsync(db, request);
        return ReportAggregationService.Aggregate(source, dimension, includeSakhan);
    }

    private static async Task<List<AggregateSourceRow>> AggregateSourceRowsAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request)
    {
        var rows = await Rows(db, request).ToListAsync();

        return rows
            .Select(row => new AggregateSourceRow
            {
                SakhanCode = row.SakhanCode,
                SakhanName = row.SakhanName,
                SectionName = row.SectionName,
                MethodName = row.MethodName,
                Country = row.BuyerCountry,
                CompanyName = row.CompanyName,
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                HSCode = row.HSCode,
                HSDescription = row.HSDescription,
                LicenceNo = row.LicenceNo,
                LicenceDate = row.LicenceDate,
                Amount = row.Amount,
                Currency = row.Currency,
            })
            .ToList();
    }

    private static IQueryable<ExportLicenceDetailFastRow> Rows(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request)
    {
        return request.Type switch
        {
            "Oversea" => OverseaRows(db, request)
                .OrderBy(row => row.LicenceDate),
            "Border" => BorderPaThaKaRows(db, request)
                .Concat(BorderIndividualTradingRows(db, request))
                .OrderBy(row => row.LicenceDate),
            _ => OverseaRows(db, request)
                .Where(_ => false)
                .OrderBy(row => row.LicenceDate)
        };
    }

    private static IQueryable<ExportLicenceDetailFastRow> OverseaRows(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request)
    {
        return
            from licence in db.ExportLicences.AsNoTracking()
            join paThaKa in db.PaThaKas.AsNoTracking() on licence.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes.AsNoTracking() on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.ExportLicenceItems.AsNoTracking() on licence.Id equals item.ExportLicenceId
            join unit in db.Units.AsNoTracking() on item.UnitId equals unit.Id
            join currency in db.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes.AsNoTracking() on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections.AsNoTracking() on licence.ExportImportSectionId equals section.Id
            join buyerCountry in db.Countries.AsNoTracking() on licence.BuyerCountryId equals buyerCountry.Id
            join method in db.ExportImportMethods.AsNoTracking() on licence.ExportImportMethodId equals method.Id
            join consignedCountry in db.Countries.AsNoTracking() on licence.ConsignedCountryId equals consignedCountry.Id
            join countryofOrigin in db.Countries.AsNoTracking() on licence.CountryofOriginId equals countryofOrigin.Id
            join incoterm in db.ExportImportIncoterms.AsNoTracking() on licence.ExportImportIncotermId equals incoterm.Id
            where request.Type == "Oversea"
                && licence.ApplyType == New
                && licence.Status == Approved
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKaType.Id == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                && (request.BuyerCountryId == 0 || licence.BuyerCountryId == request.BuyerCountryId)
            select new ExportLicenceDetailFastRow
            {
                PaThaKaTypeId = paThaKaType.Id,
                PaThaKaTypeCode = paThaKaType.Code,
                PaThaKaTypeName = paThaKaType.Description,
                ExportImportSectionId = licence.ExportImportSectionId,
                ExportImportMethodId = licence.ExportImportMethodId,
                ExportImportIncotermId = licence.ExportImportIncotermId,
                BuyerCountryId = licence.BuyerCountryId,
                SectionCode = section.Code,
                SectionName = section.Name,
                LicenceNo = licence.ExportLicenceNo,
                LicenceDate = licence.IssuedDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                BuyerName = licence.BuyerName,
                BuyerAddress = licence.BuyerAddress,
                BuyerCountry = buyerCountry.Name,
                PortofExportIds = licence.PortofExportId,
                PortofDischarge = licence.PortofDischarge,
                LastDate = licence.LastDate,
                MethodName = method.Name,
                DestinationCountryIds = licence.DestinationCountryId,
                ConsignedCountry = consignedCountry.Name,
                CountryofOrigin = countryofOrigin.Name,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description + " " + item.Description,
                Unit = unit.Code,
                Price = item.Price,
                Quantity = item.Quantity,
                Amount = item.Amount,
                Currency = currency.Code,
                Conditions = licence.Remark,
                ApplicationNo = licence.ApplicationNo,
                ApplicationDate = licence.ApplicationDate,
                CommodityType = licence.CommodityType,
                ApproveDate = licence.ApproveDate
            };
    }

    private static IQueryable<ExportLicenceDetailFastRow> BorderPaThaKaRows(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request)
    {
        return
            from licence in db.BorderExportLicences.AsNoTracking()
            join paThaKa in db.PaThaKas.AsNoTracking() on licence.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes.AsNoTracking() on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderExportLicenceItems.AsNoTracking() on licence.Id equals item.BorderExportLicenceId
            join unit in db.Units.AsNoTracking() on item.UnitId equals unit.Id
            join currency in db.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes.AsNoTracking() on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections.AsNoTracking() on licence.ExportImportSectionId equals section.Id
            join buyerCountry in db.Countries.AsNoTracking() on licence.BuyerCountryId equals buyerCountry.Id
            join method in db.ExportImportMethods.AsNoTracking() on licence.ExportImportMethodId equals method.Id
            join consignedCountry in db.Countries.AsNoTracking() on licence.ConsignedCountryId equals consignedCountry.Id
            join countryofOrigin in db.Countries.AsNoTracking() on licence.CountryofOriginId equals countryofOrigin.Id
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
                && (request.BuyerCountryId == 0 || licence.BuyerCountryId == request.BuyerCountryId)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select new ExportLicenceDetailFastRow
            {
                PaThaKaTypeId = paThaKaType.Id,
                PaThaKaTypeCode = paThaKaType.Code,
                PaThaKaTypeName = paThaKaType.Description,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name,
                ExportImportSectionId = licence.ExportImportSectionId,
                ExportImportMethodId = licence.ExportImportMethodId,
                ExportImportIncotermId = licence.ExportImportIncotermId,
                BuyerCountryId = licence.BuyerCountryId,
                SectionCode = section.Code,
                SectionName = section.Name,
                LicenceNo = licence.ExportLicenceNo,
                LicenceDate = licence.IssuedDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                BuyerName = licence.BuyerName,
                BuyerAddress = licence.BuyerAddress,
                BuyerCountry = buyerCountry.Name,
                PortofExportIds = licence.PortofExportId,
                PortofDischarge = licence.PortofDischarge,
                LastDate = licence.LastDate,
                MethodName = method.Name,
                DestinationCountryIds = licence.DestinationCountryId,
                ConsignedCountry = consignedCountry.Name,
                CountryofOrigin = countryofOrigin.Name,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description + " " + (item.Description ?? string.Empty),
                Unit = unit.Code,
                Price = item.Price,
                Quantity = item.Quantity,
                Amount = item.Amount,
                Currency = currency.Code,
                Conditions = licence.Remark,
                ApplicationNo = licence.ApplicationNo,
                ApplicationDate = licence.ApplicationDate,
                CommodityType = licence.CommodityType,
                ApproveDate = licence.ApproveDate
            };
    }

    private static IQueryable<ExportLicenceDetailFastRow> BorderIndividualTradingRows(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request)
    {
        return
            from licence in db.BorderExportLicences.AsNoTracking()
            join individualTrading in db.IndividualTradings.AsNoTracking() on licence.IndividualTradingId equals individualTrading.Id
            join paThaKaType in db.PaThaKaTypes.AsNoTracking() on individualTrading.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderExportLicenceItems.AsNoTracking() on licence.Id equals item.BorderExportLicenceId
            join unit in db.Units.AsNoTracking() on item.UnitId equals unit.Id
            join currency in db.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes.AsNoTracking() on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections.AsNoTracking() on licence.ExportImportSectionId equals section.Id
            join buyerCountry in db.Countries.AsNoTracking() on licence.BuyerCountryId equals buyerCountry.Id
            join method in db.ExportImportMethods.AsNoTracking() on licence.ExportImportMethodId equals method.Id
            join consignedCountry in db.Countries.AsNoTracking() on licence.ConsignedCountryId equals consignedCountry.Id
            join countryofOrigin in db.Countries.AsNoTracking() on licence.CountryofOriginId equals countryofOrigin.Id
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
                && (request.BuyerCountryId == 0 || licence.BuyerCountryId == request.BuyerCountryId)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select new ExportLicenceDetailFastRow
            {
                PaThaKaTypeId = paThaKaType.Id,
                PaThaKaTypeCode = paThaKaType.Code,
                PaThaKaTypeName = paThaKaType.Description,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name,
                ExportImportSectionId = licence.ExportImportSectionId,
                ExportImportMethodId = licence.ExportImportMethodId,
                ExportImportIncotermId = licence.ExportImportIncotermId,
                BuyerCountryId = licence.BuyerCountryId,
                SectionCode = section.Code,
                SectionName = section.Name,
                LicenceNo = licence.ExportLicenceNo,
                LicenceDate = licence.IssuedDate,
                CompanyRegistrationNo = individualTrading.Tinno,
                CompanyName = individualTrading.Name,
                UnitLevel = individualTrading.UnitLevel,
                StreetNumberStreetName = individualTrading.StreetNumberStreetName,
                QuarterCityTownship = individualTrading.QuarterCityTownship,
                State = individualTrading.State,
                Country = individualTrading.Country,
                PostalCode = individualTrading.PostalCode,
                BuyerName = licence.BuyerName,
                BuyerAddress = licence.BuyerAddress,
                BuyerCountry = buyerCountry.Name,
                PortofExportIds = licence.PortofExportId,
                PortofDischarge = licence.PortofDischarge,
                LastDate = licence.LastDate,
                MethodName = method.Name,
                DestinationCountryIds = licence.DestinationCountryId,
                ConsignedCountry = consignedCountry.Name,
                CountryofOrigin = countryofOrigin.Name,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description + " " + (item.Description ?? string.Empty),
                Unit = unit.Code,
                Price = item.Price,
                Quantity = item.Quantity,
                Amount = item.Amount,
                Currency = currency.Code,
                Conditions = licence.Remark,
                ApplicationNo = licence.ApplicationNo,
                ApplicationDate = licence.ApplicationDate,
                CommodityType = licence.CommodityType,
                ApproveDate = licence.ApproveDate
            };
    }

    private sealed class ExportLicenceDetailFastRow
    {
        public int PaThaKaTypeId { get; init; }
        public string PaThaKaTypeCode { get; init; } = null!;
        public string PaThaKaTypeName { get; init; } = null!;
        public int? SakhanId { get; init; }
        public string? SakhanCode { get; init; }
        public string? SakhanName { get; init; }
        public int ExportImportSectionId { get; init; }
        public int ExportImportMethodId { get; init; }
        public int ExportImportIncotermId { get; init; }
        public int BuyerCountryId { get; init; }
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
        public string BuyerName { get; init; } = null!;
        public string BuyerAddress { get; init; } = null!;
        public string? BuyerCountry { get; init; }
        public string? PortofExportIds { get; init; }
        public string PortofDischarge { get; init; } = null!;
        public DateTime? LastDate { get; init; }
        public string MethodName { get; init; } = null!;
        public string? DestinationCountryIds { get; init; }
        public string? ConsignedCountry { get; init; }
        public string? CountryofOrigin { get; init; }
        public string HSCode { get; init; } = null!;
        public string HSDescription { get; init; } = null!;
        public string? Unit { get; init; }
        public decimal Price { get; init; }
        public decimal Quantity { get; init; }
        public decimal Amount { get; init; }
        public string? Currency { get; init; }
        public string? Conditions { get; init; }
        public string? ApplicationNo { get; init; }
        public DateTime? ApplicationDate { get; init; }
        public string? CommodityType { get; init; }
        public DateTime? ApproveDate { get; init; }

        public sp_ExportLicenceDetailReportResult ToResult(
            IReadOnlyList<ReportLookupEntry> ports,
            IReadOnlyList<ReportLookupEntry> countries)
        {
            return new sp_ExportLicenceDetailReportResult
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
                BuyerCountryId = BuyerCountryId,
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
                BuyerName = BuyerName,
                BuyerAddress = BuyerAddress,
                BuyerCountry = BuyerCountry,
                PortofExport = ReportLookupCache.ResolveCsv(PortofExportIds, ports),
                PortofDischarge = PortofDischarge,
                LastDate = LastDate,
                MethodName = MethodName,
                DestinationCountry = ReportLookupCache.ResolveCsv(DestinationCountryIds, countries),
                ConsignedCountry = ConsignedCountry,
                CountryofOrigin = CountryofOrigin,
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
                CommodityType = CommodityType,
                ApproveDate = ApproveDate
            };
        }
    }
}

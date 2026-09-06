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

public static class sp_ImportPermitDetailReport_Fast
{
    private const string New = "New";
    private const string Approved = "Approved";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    public static async Task<ApiResult<sp_ImportPermitDetailReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_ImportPermitDetailReportRequest request,
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

        var rows = OrderedRows(db, request);
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
            return ApiResult<sp_ImportPermitDetailReportResult>.CreatePageFromRows(
                results,
                totalCount.Value,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<sp_ImportPermitDetailReportResult>.CreateFastPageFromRows(
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
        sp_ImportPermitDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var ports = await ReportLookupCache.GetPortNamesAsync(db, cache);
        var countries = await ReportLookupCache.GetCountryNamesAsync(db, cache);

        var rows = await OrderedRows(db, request).ToListAsync();

        var resolved = rows
            .Select(row => row.ToResult(ports, countries))
            .ToList();

        return await ExcelGenerator.CreateWorkbookAsync(resolved.AsQueryable(), pagingRequest, worksheetName);
    }

    public static async IAsyncEnumerable<List<sp_ImportPermitDetailReportResult>> StreamResolvedChunksAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_ImportPermitDetailReportRequest request,
        int chunkSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var ports = await ReportLookupCache.GetPortNamesAsync(db, cache);
        var countries = await ReportLookupCache.GetCountryNamesAsync(db, cache);

        await foreach (var rawChunk in OrderedRows(db, request).AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
        {
            yield return rawChunk.Select(row => row.ToResult(ports, countries)).ToList();
        }
    }

    public static async Task<ApiResult<ReportAggregateResult>> CreateAggregateResultAsync(
        TradeNetDbContext db,
        sp_ImportPermitDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        ReportAggregateDimension dimension,
        bool includeSakhan,
        bool includeColumnTotals = false,
        ReportColumnTotalsMode columnTotalsMode = ReportColumnTotalsMode.CountAndValue,
        ReportAggregateOrdering ordering = ReportAggregateOrdering.Canonical)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var source = await AggregateSourceRowsAsync(db, request, ordering);

        ApiResult<ReportAggregateResult> result;

        if (dimension == ReportAggregateDimension.Daily)
        {
            // Daily reports carry a "Total USD Value" column. Group, fill the FX conversion
            // (which needs DB access, unlike the in-memory CreatePagedResult), then page.
            var groups = ReportAggregationService.Aggregate(source, dimension, includeSakhan);
            await ReportUsdConversionService.FillDailyUsdValuesAsync(db, groups);
            result = ReportAggregationService.CreatePagedResultFromGroups(
                groups, dimension, includeSakhan, pagingRequest, includeColumnTotals, columnTotalsMode);
        }
        else
        {
            result = ReportAggregationService.CreatePagedResult(
                source, dimension, includeSakhan, pagingRequest, includeColumnTotals, columnTotalsMode, ordering);
        }

        if (includeColumnTotals && result.ColumnTotals is not null)
        {
            // The legacy footer is =CountDistinct(Fields!LicenceNo.Value) over the WHOLE
            // dataset (e.g. ImportPermitBySectionReport.rdlc:895), which is NOT the sum of
            // the per-row counts: each grid row is one (group, currency) pair, so a permit
            // with items in two currencies would otherwise be counted twice. `source` is
            // already materialized, so this costs no extra query.
            result.ColumnTotals = new Dictionary<string, decimal>(result.ColumnTotals)
            {
                ["noOfLicences"] = source
                    .Select(row => row.LicenceNo)
                    .Where(licenceNo => !string.IsNullOrEmpty(licenceNo))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
            };
        }

        return result;
    }

    public static async Task<byte[]> CreateAggregateExcelWorkbookAsync(
        TradeNetDbContext db,
        sp_ImportPermitDetailReportRequest request,
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
        sp_ImportPermitDetailReportRequest request,
        ReportAggregateDimension dimension,
        bool includeSakhan,
        ReportAggregateOrdering ordering = ReportAggregateOrdering.Canonical)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var source = await AggregateSourceRowsAsync(db, request, ordering);
        var groups = ReportAggregationService.Aggregate(source, dimension, includeSakhan, ordering);

        if (dimension == ReportAggregateDimension.Daily)
        {
            await ReportUsdConversionService.FillDailyUsdValuesAsync(db, groups);
        }

        return groups;
    }

    private static async Task<List<AggregateSourceRow>> AggregateSourceRowsAsync(
        TradeNetDbContext db,
        sp_ImportPermitDetailReportRequest request,
        ReportAggregateOrdering ordering = ReportAggregateOrdering.Canonical)
    {
        // SourceOrder reports print their groups in first-appearance order, so the source has
        // to arrive in the legacy row order (permits as created, items in line order) -- the
        // same order the Detail grid pages in. Canonical reports re-sort, so they skip the ORDER BY.
        var rows = ordering == ReportAggregateOrdering.SourceOrder
            ? await OrderedRows(db, request).ToListAsync()
            : await Rows(db, request).ToListAsync();

        return rows
            .Select(row => new AggregateSourceRow
            {
                SakhanCode = row.SakhanCode,
                SakhanName = row.SakhanName,
                SectionName = row.SectionName,
                SectionId = row.ExportImportSectionId,
                MethodName = null,
                Country = row.SellerCountry,
                CountryId = row.SellerCountryId,
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

    /// <summary>
    /// <see cref="Rows"/> in the order the grid, the Excel stream and
    /// sp_BorderImportPermitDetailReport_pagination all page in: permits by CreatedDate then Id,
    /// items by ItemNo then UniqueId. Without it OFFSET/FETCH ran over an unordered join and a
    /// row could appear on two pages or on none. The aggregate paths keep the unordered query.
    /// </summary>
    private static IOrderedQueryable<ImportPermitDetailFastRow> OrderedRows(
        TradeNetDbContext db,
        sp_ImportPermitDetailReportRequest request)
    {
        return Rows(db, request)
            .OrderBy(row => row.PermitCreatedDate)
            .ThenBy(row => row.PermitId)
            .ThenBy(row => row.ItemNo)
            .ThenBy(row => row.ItemUniqueId);
    }

    private static IQueryable<ImportPermitDetailFastRow> Rows(
        TradeNetDbContext db,
        sp_ImportPermitDetailReportRequest request)
    {
        return request.Type switch
        {
            "Oversea" => OverseaRows(db, request),
            "Border" => BorderRows(db, request),
            _ => OverseaRows(db, request).Where(_ => false)
        };
    }

    private static IQueryable<ImportPermitDetailFastRow> OverseaRows(
        TradeNetDbContext db,
        sp_ImportPermitDetailReportRequest request)
    {
        return
            from permit in db.ImportPermits.AsNoTracking()
            join paThaKa in db.PaThaKas.AsNoTracking() on permit.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes.AsNoTracking() on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.ImportPermitItems.AsNoTracking() on permit.Id equals item.ImportPermitId
            join unit in db.Units.AsNoTracking() on item.UnitId equals unit.Id
            join currency in db.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes.AsNoTracking() on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections.AsNoTracking() on permit.ExportImportSectionId equals section.Id
            join sellerCountry in db.Countries.AsNoTracking() on permit.SellerCountryId equals sellerCountry.Id
            from nrcPrefix in db.Nrcprefixes.AsNoTracking()
                .Where(prefix => permit.NrcprefixId == prefix.Id)
                .DefaultIfEmpty()
            from nrcPrefixCode in db.NrcprefixCodes.AsNoTracking()
                .Where(prefixCode => permit.NrcprefixCodeId == prefixCode.Id)
                .DefaultIfEmpty()
            where request.Type == "Oversea"
                && permit.ApplyType == New
                && permit.Status == Approved
                && permit.CreatedDate >= request.FromDate
                && permit.CreatedDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKaType.Id == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || permit.ExportImportSectionId == request.ExportImportSectionId)
                && (request.SellerCountryId == 0 || permit.SellerCountryId == request.SellerCountryId)
            select new ImportPermitDetailFastRow
            {
                PermitCreatedDate = permit.CreatedDate,
                PermitId = permit.Id,
                ItemNo = item.ItemNo,
                ItemUniqueId = item.UniqueId,
                PaThaKaTypeId = paThaKaType.Id,
                PaThaKaTypeCode = paThaKaType.Code,
                PaThaKaTypeName = paThaKaType.Description,
                ExportImportSectionId = permit.ExportImportSectionId,
                SellerCountryId = permit.SellerCountryId,
                SectionCode = section.Code,
                SectionName = section.Name,
                LicenceNo = permit.ImportPermitNo,
                LicenceDate = permit.IssuedDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                AuthorisedAgentName = permit.AuthorisedAgentName,
                AuthorisedAgentAddress = permit.AuthorisedAgentAddress,
                SellerCountry = sellerCountry.Name,
                PortofShipmentIds = permit.PortofShipmentId,
                PortofDischarge = permit.PortofDischarge,
                CountryofOriginIds = permit.CountryofOriginId,
                LastDate = permit.LastDate,
                HSCode = hsCode.Code,
                HSDescription = item.Description,
                Unit = unit.Code,
                Price = item.Price,
                Quantity = item.Quantity,
                Amount = item.Amount,
                Currency = currency.Code,
                NRCNo = permit.Nrctype == CurrentNrcType && permit.Nrcno != string.Empty
                    ? nrcPrefix!.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode!.Code + permit.Nrcno
                    : permit.Nrctype == OldNrcType && permit.Nrcno != string.Empty
                        ? permit.Nrcno!
                        : string.Empty,
                PermitType = permit.PermitType,
                Conditions = permit.Remark,
                ApproveDate = permit.ApproveDate
            };
    }

    private static IQueryable<ImportPermitDetailFastRow> BorderRows(
        TradeNetDbContext db,
        sp_ImportPermitDetailReportRequest request)
    {
        return
            from permit in db.BorderImportPermits.AsNoTracking()
            join paThaKa in db.PaThaKas.AsNoTracking() on permit.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes.AsNoTracking() on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderImportPermitItems.AsNoTracking() on permit.Id equals item.BorderImportPermitId
            join unit in db.Units.AsNoTracking() on item.UnitId equals unit.Id
            join currency in db.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes.AsNoTracking() on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections.AsNoTracking() on permit.ExportImportSectionId equals section.Id
            join sellerCountry in db.Countries.AsNoTracking() on permit.SellerCountryId equals sellerCountry.Id
            join sakhan in db.Sakhans.AsNoTracking() on permit.SakhanId equals sakhan.Id
            from nrcPrefix in db.Nrcprefixes.AsNoTracking()
                .Where(prefix => permit.NrcprefixId == prefix.Id)
                .DefaultIfEmpty()
            from nrcPrefixCode in db.NrcprefixCodes.AsNoTracking()
                .Where(prefixCode => permit.NrcprefixCodeId == prefixCode.Id)
                .DefaultIfEmpty()
            where request.Type == "Border"
                && permit.ApplyType == New
                && permit.Status == Approved
                && permit.CreatedDate >= request.FromDate
                && permit.CreatedDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKaType.Id == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || permit.ExportImportSectionId == request.ExportImportSectionId)
                && (request.SellerCountryId == 0 || permit.SellerCountryId == request.SellerCountryId)
                && (request.SakhanId == 0 || permit.SakhanId == request.SakhanId)
            select new ImportPermitDetailFastRow
            {
                PermitCreatedDate = permit.CreatedDate,
                PermitId = permit.Id,
                ItemNo = item.ItemNo,
                ItemUniqueId = item.UniqueId,
                PaThaKaTypeId = paThaKaType.Id,
                PaThaKaTypeCode = paThaKaType.Code,
                PaThaKaTypeName = paThaKaType.Description,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name,
                ExportImportSectionId = permit.ExportImportSectionId,
                SellerCountryId = permit.SellerCountryId,
                SectionCode = section.Code,
                SectionName = section.Name,
                LicenceNo = permit.ImportPermitNo,
                LicenceDate = permit.IssuedDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                AuthorisedAgentName = permit.AuthorisedAgentName,
                AuthorisedAgentAddress = permit.AuthorisedAgentAddress,
                SellerCountry = sellerCountry.Name,
                PortofShipmentIds = permit.PortofShipmentId,
                PortofDischarge = permit.PortofDischarge,
                CountryofOriginIds = permit.CountryofOriginId,
                LastDate = permit.LastDate,
                HSCode = hsCode.Code,
                HSDescription = item.Description,
                Unit = unit.Code,
                Price = item.Price,
                Quantity = item.Quantity,
                Amount = item.Amount,
                Currency = currency.Code,
                NRCNo = permit.Nrctype == CurrentNrcType && permit.Nrcno != string.Empty
                    ? nrcPrefix!.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode!.Code + permit.Nrcno
                    : permit.Nrctype == OldNrcType && permit.Nrcno != string.Empty
                        ? permit.Nrcno!
                        : string.Empty,
                PermitType = permit.PermitType,
                Conditions = permit.Remark,
                ApproveDate = permit.ApproveDate
            };
    }

    private sealed class ImportPermitDetailFastRow
    {
        // Paging / streaming order only (never exposed): the permits in creation order, their
        // items in line order -- the deterministic reading of the legacy procedure's output,
        // which has no ORDER BY. Also what sp_BorderImportPermitDetailReport_pagination uses,
        // so the LINQ fallback and the procedure page identically.
        public DateTime? PermitCreatedDate { get; init; }
        public string PermitId { get; init; } = null!;
        public int ItemNo { get; init; }
        public int ItemUniqueId { get; init; }

        public int PaThaKaTypeId { get; init; }
        public string PaThaKaTypeCode { get; init; } = null!;
        public string PaThaKaTypeName { get; init; } = null!;
        public int? SakhanId { get; init; }
        public string? SakhanCode { get; init; }
        public string? SakhanName { get; init; }
        public int ExportImportSectionId { get; init; }
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
        public string AuthorisedAgentName { get; init; } = null!;
        public string AuthorisedAgentAddress { get; init; } = null!;
        public string? SellerCountry { get; init; }
        public string PortofShipmentIds { get; init; } = null!;
        public string PortofDischarge { get; init; } = null!;
        public string CountryofOriginIds { get; init; } = null!;
        public DateTime? LastDate { get; init; }
        public string HSCode { get; init; } = null!;
        public string? HSDescription { get; init; }
        public string? Unit { get; init; }
        public decimal Price { get; init; }
        public decimal Quantity { get; init; }
        public decimal Amount { get; init; }
        public string? Currency { get; init; }
        public string NRCNo { get; init; } = null!;
        public string PermitType { get; init; } = null!;
        public string? Conditions { get; init; }
        public DateTime? ApproveDate { get; init; }

        public sp_ImportPermitDetailReportResult ToResult(
            IReadOnlyList<ReportLookupEntry> ports,
            IReadOnlyList<ReportLookupEntry> countries)
        {
            return new sp_ImportPermitDetailReportResult
            {
                PaThaKaTypeId = PaThaKaTypeId,
                PaThaKaTypeCode = PaThaKaTypeCode,
                PaThaKaTypeName = PaThaKaTypeName,
                SakhanId = SakhanId,
                SakhanCode = SakhanCode,
                SakhanName = SakhanName,
                ExportImportSectionId = ExportImportSectionId,
                SellerCountryId = SellerCountryId,
                SectionCode = SectionCode,
                SectionName = SectionName,
                LicenceNo = LicenceNo,
                LicenceDate = LicenceDate,
                CompanyRegistrationNo = CompanyRegistrationNo,
                CompanyName = CompanyName,
                CompanyAddress = LegacyCompanyAddress.Compose(
                    UnitLevel, StreetNumberStreetName, QuarterCityTownship, State, Country, PostalCode),
                UnitLevel = UnitLevel,
                StreetNumberStreetName = StreetNumberStreetName,
                QuarterCityTownship = QuarterCityTownship,
                State = State,
                Country = Country,
                PostalCode = PostalCode,
                AuthorisedAgentName = AuthorisedAgentName,
                AuthorisedAgentAddress = AuthorisedAgentAddress,
                SellerCountry = SellerCountry,
                PortofShipment = ReportLookupCache.ResolveCsv(PortofShipmentIds, ports),
                PortofDischarge = PortofDischarge,
                CountryofOrigin = ReportLookupCache.ResolveCsv(CountryofOriginIds, countries),
                LastDate = LastDate,
                HSCode = HSCode,
                HSDescription = HSDescription,
                Unit = Unit,
                Price = Price,
                Quantity = Quantity,
                Amount = Amount,
                Currency = Currency,
                NRCNo = NRCNo,
                PermitType = PermitType,
                Conditions = Conditions,
                ApproveDate = ApproveDate
            };
        }
    }
}

using API.DBContext;
using API.Model;
using API.Service.ExcelExport;
using API.Service.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public static class sp_ExportPermitDetailReport_Fast
{
    private const string New = "New";
    private const string Approved = "Approved";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    public static async Task<ApiResult<sp_ExportPermitDetailReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_ExportPermitDetailReportRequest request,
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
            return ApiResult<sp_ExportPermitDetailReportResult>.CreatePageFromRows(
                results,
                totalCount.Value,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<sp_ExportPermitDetailReportResult>.CreateFastPageFromRows(
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
        sp_ExportPermitDetailReportRequest request,
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

    public static async IAsyncEnumerable<List<sp_ExportPermitDetailReportResult>> StreamResolvedChunksAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_ExportPermitDetailReportRequest request,
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
        sp_ExportPermitDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        ReportAggregateDimension dimension,
        bool includeSakhan,
        bool includeColumnTotals = false)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        // Group in SQL (sp_ExportPermitDetailReport_Aggregate) instead of pulling the whole detail
        // join into memory — the old in-memory path transferred ~12k wide rows over the DB link and
        // hung (~50s). The grouped rows are equivalent to ReportAggregationService.Aggregate.
        var groups = await AggregateGroupedRowsAsync(db, request, dimension, includeSakhan);

        if (dimension == ReportAggregateDimension.Daily)
        {
            // Daily reports carry a "Total USD Value" column; fill the FX conversion (needs DB access).
            await ReportUsdConversionService.FillDailyUsdValuesAsync(db, groups);
        }

        return ReportAggregationService.CreatePagedResultFromGroups(
            groups, dimension, includeSakhan, pagingRequest, includeColumnTotals);
    }

    public static async Task<byte[]> CreateAggregateExcelWorkbookAsync(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        ReportAggregateDimension dimension,
        bool includeSakhan,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var groups = await AggregateGroupedRowsAsync(db, request, dimension, includeSakhan);
        if (dimension == ReportAggregateDimension.Daily)
        {
            await ReportUsdConversionService.FillDailyUsdValuesAsync(db, groups);
        }

        return await ReportAggregationService.CreateExcelWorkbookFromGroupsAsync(
            groups, dimension, includeSakhan, pagingRequest, worksheetName);
    }

    public static async Task<List<ReportAggregateResult>> GetAggregateRowsAsync(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request,
        ReportAggregateDimension dimension,
        bool includeSakhan)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var groups = await AggregateGroupedRowsAsync(db, request, dimension, includeSakhan);

        if (dimension == ReportAggregateDimension.Daily)
        {
            await ReportUsdConversionService.FillDailyUsdValuesAsync(db, groups);
        }

        // SQL returns the groups unordered; apply the report's canonical ordering so the
        // streamed Excel rows match the on-screen order.
        return ReportAggregationService.OrderGroups(groups, dimension, includeSakhan);
    }

    // Dimensions handled by sp_ExportPermitDetailReport_Aggregate (the SQL-side GROUP BY).
    private static readonly Dictionary<ReportAggregateDimension, string> AggregateProcDimensions = new()
    {
        [ReportAggregateDimension.Section] = "Section",
        [ReportAggregateDimension.Country] = "Country",
        [ReportAggregateDimension.Company] = "Company",
        [ReportAggregateDimension.Daily] = "Daily",
    };

    /// <summary>
    /// Returns the aggregate groups for the report's dimension, grouped in SQL via
    /// <c>dbo.sp_ExportPermitDetailReport_Aggregate</c> (equivalent to the in-memory
    /// <see cref="ReportAggregationService.Aggregate"/> but without materializing the detail join).
    /// Falls back to the in-memory path for any dimension the proc does not handle.
    /// </summary>
    private static async Task<List<ReportAggregateResult>> AggregateGroupedRowsAsync(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request,
        ReportAggregateDimension dimension,
        bool includeSakhan)
    {
        if (!AggregateProcDimensions.TryGetValue(dimension, out var dimensionArg))
        {
            var source = await AggregateSourceRowsAsync(db, request);
            return ReportAggregationService.Aggregate(source, dimension, includeSakhan);
        }

        var parameters = new[]
        {
            new SqlParameter("@Type", (object?)request.Type ?? "Oversea"),
            new SqlParameter("@Dimension", dimensionArg),
            new SqlParameter("@IncludeSakhan", includeSakhan),
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@PaThaKaTypeId", request.PaThaKaTypeId),
            new SqlParameter("@ExportImportSectionId", request.ExportImportSectionId),
            new SqlParameter("@BuyerCountryId", request.BuyerCountryId),
            new SqlParameter("@CompanyRegistrationNo", (object?)request.CompanyRegistrationNo ?? string.Empty),
            new SqlParameter("@SakhanId", request.SakhanId),
        };

        const string sql =
            "EXEC dbo.sp_ExportPermitDetailReport_Aggregate @Type, @Dimension, @IncludeSakhan, @FromDate, " +
            "@ToDate, @PaThaKaTypeId, @ExportImportSectionId, @BuyerCountryId, @CompanyRegistrationNo, @SakhanId";

        return await db.Database
            .SqlQueryRaw<ReportAggregateResult>(sql, parameters)
            .ToListAsync();
    }

    private static async Task<List<AggregateSourceRow>> AggregateSourceRowsAsync(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request)
    {
        var rows = await Rows(db, request).ToListAsync();

        return rows
            .Select(row => new AggregateSourceRow
            {
                SakhanCode = row.SakhanCode,
                SakhanName = row.SakhanName,
                SectionName = row.SectionName,
                SectionId = row.ExportImportSectionId,
                MethodName = null,
                Country = row.BuyerCountry,
                CountryId = row.BuyerCountryId,
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

    private static IQueryable<ExportPermitDetailFastRow> Rows(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request)
    {
        return request.Type switch
        {
            "Oversea" => OverseaRows(db, request),
            "Border" => BorderRows(db, request),
            _ => OverseaRows(db, request).Where(_ => false)
        };
    }

    private static IQueryable<ExportPermitDetailFastRow> OverseaRows(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request)
    {
        return
            from permit in db.ExportPermits.AsNoTracking()
            join paThaKa in db.PaThaKas.AsNoTracking() on permit.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes.AsNoTracking() on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.ExportPermitItems.AsNoTracking() on permit.Id equals item.ExportPermitId
            join unit in db.Units.AsNoTracking() on item.UnitId equals unit.Id
            join currency in db.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes.AsNoTracking() on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections.AsNoTracking() on permit.ExportImportSectionId equals section.Id
            join buyerCountry in db.Countries.AsNoTracking() on permit.BuyerCountryId equals buyerCountry.Id
            join consignedCountry in db.Countries.AsNoTracking() on permit.ConsignedCountryId equals consignedCountry.Id
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
                && (request.BuyerCountryId == 0 || permit.BuyerCountryId == request.BuyerCountryId)
                && (request.HSCode == string.Empty || hsCode.Code == request.HSCode)
            select new ExportPermitDetailFastRow
            {
                PaThaKaTypeId = paThaKaType.Id,
                PaThaKaTypeCode = paThaKaType.Code,
                PaThaKaTypeName = paThaKaType.Description,
                ExportImportSectionId = permit.ExportImportSectionId,
                BuyerCountryId = permit.BuyerCountryId,
                SectionCode = section.Code,
                SectionName = section.Name,
                LicenceNo = permit.ExportPermitNo,
                LicenceDate = permit.IssuedDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                ConsigneeName = permit.ConsigneeName,
                ConsigneeAddress = permit.ConsigneeAddress,
                BuyerCountry = buyerCountry.Name,
                PortofExportIds = permit.PortofExportId,
                PortofDischarge = permit.PortofDischarge,
                DestinationCountryIds = permit.DestinationCountryId,
                LastDate = permit.LastDate,
                ConsignedCountry = consignedCountry.Name,
                CountryofOriginIds = permit.CountryofOriginId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description + " " + item.Description,
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

    private static IQueryable<ExportPermitDetailFastRow> BorderRows(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request)
    {
        return
            from permit in db.BorderExportPermits.AsNoTracking()
            join paThaKa in db.PaThaKas.AsNoTracking() on permit.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes.AsNoTracking() on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderExportPermitItems.AsNoTracking() on permit.Id equals item.BorderExportPermitId
            join unit in db.Units.AsNoTracking() on item.UnitId equals unit.Id
            join currency in db.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes.AsNoTracking() on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections.AsNoTracking() on permit.ExportImportSectionId equals section.Id
            join buyerCountry in db.Countries.AsNoTracking() on permit.BuyerCountryId equals buyerCountry.Id
            join consignedCountry in db.Countries.AsNoTracking() on permit.ConsignedCountryId equals consignedCountry.Id
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
                && (request.BuyerCountryId == 0 || permit.BuyerCountryId == request.BuyerCountryId)
                && (request.SakhanId == 0 || permit.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty || hsCode.Code == request.HSCode)
            select new ExportPermitDetailFastRow
            {
                PaThaKaTypeId = paThaKaType.Id,
                PaThaKaTypeCode = paThaKaType.Code,
                PaThaKaTypeName = paThaKaType.Description,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name,
                ExportImportSectionId = permit.ExportImportSectionId,
                BuyerCountryId = permit.BuyerCountryId,
                SectionCode = section.Code,
                SectionName = section.Name,
                LicenceNo = permit.ExportPermitNo,
                LicenceDate = permit.IssuedDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                ConsigneeName = permit.ConsigneeName,
                ConsigneeAddress = permit.ConsigneeAddress,
                BuyerCountry = buyerCountry.Name,
                PortofExportIds = permit.PortofExportId,
                PortofDischarge = permit.PortofDischarge,
                DestinationCountryIds = permit.DestinationCountryId,
                LastDate = permit.LastDate,
                ConsignedCountry = consignedCountry.Name,
                CountryofOriginIds = permit.CountryofOriginId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description + " " + item.Description,
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

    private sealed class ExportPermitDetailFastRow
    {
        public int PaThaKaTypeId { get; init; }
        public string PaThaKaTypeCode { get; init; } = null!;
        public string PaThaKaTypeName { get; init; } = null!;
        public int? SakhanId { get; init; }
        public string? SakhanCode { get; init; }
        public string? SakhanName { get; init; }
        public int ExportImportSectionId { get; init; }
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
        public string ConsigneeName { get; init; } = null!;
        public string ConsigneeAddress { get; init; } = null!;
        public string? BuyerCountry { get; init; }
        public string? PortofExportIds { get; init; }
        public string PortofDischarge { get; init; } = null!;
        public string? DestinationCountryIds { get; init; }
        public DateTime? LastDate { get; init; }
        public string? ConsignedCountry { get; init; }
        public string? CountryofOriginIds { get; init; }
        public string HSCode { get; init; } = null!;
        public string HSDescription { get; init; } = null!;
        public string? Unit { get; init; }
        public decimal Price { get; init; }
        public decimal Quantity { get; init; }
        public decimal Amount { get; init; }
        public string? Currency { get; init; }
        public string NRCNo { get; init; } = null!;
        public string PermitType { get; init; } = null!;
        public string? Conditions { get; init; }
        public DateTime? ApproveDate { get; init; }

        public sp_ExportPermitDetailReportResult ToResult(
            IReadOnlyList<ReportLookupEntry> ports,
            IReadOnlyList<ReportLookupEntry> countries)
        {
            return new sp_ExportPermitDetailReportResult
            {
                PaThaKaTypeId = PaThaKaTypeId,
                PaThaKaTypeCode = PaThaKaTypeCode,
                PaThaKaTypeName = PaThaKaTypeName,
                SakhanId = SakhanId,
                SakhanCode = SakhanCode,
                SakhanName = SakhanName,
                ExportImportSectionId = ExportImportSectionId,
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
                ConsigneeName = ConsigneeName,
                ConsigneeAddress = ConsigneeAddress,
                BuyerCountry = BuyerCountry,
                PortofExport = ReportLookupCache.ResolveCsv(PortofExportIds, ports),
                PortofDischarge = PortofDischarge,
                DestinationCountry = ReportLookupCache.ResolveCsv(DestinationCountryIds, countries),
                LastDate = LastDate,
                ConsignedCountry = ConsignedCountry,
                CountryofOrigin = ReportLookupCache.ResolveCsv(CountryofOriginIds, countries),
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

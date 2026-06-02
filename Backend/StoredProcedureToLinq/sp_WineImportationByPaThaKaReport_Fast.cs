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

public static class sp_WineImportationByPaThaKaReport_Fast
{
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    public static async Task<ApiResult<sp_WineImportationByPaThaKaReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_WineImportationByPaThaKaReportRequest request,
        ReportQueryRequest pagingRequest)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var wineTypes = await ReportLookupCache.GetWineTypeNamesAsync(db, cache);

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
            .Select(row => row.ToResult(wineTypes))
            .ToList();

        if (totalCount.HasValue)
        {
            return ApiResult<sp_WineImportationByPaThaKaReportResult>.CreatePageFromRows(
                results,
                totalCount.Value,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<sp_WineImportationByPaThaKaReportResult>.CreateFastPageFromRows(
            results,
            pageIndex,
            pageSize,
            null,
            null,
            pagingRequest.FilterColumn,
            pagingRequest.FilterQuery);
    }

    /// <summary>
    /// Returns every Wine Importation (Alcoholic Beverages) card for the company,
    /// with NRC and wine-type values resolved. No paging — intended for the
    /// composite CardListsByPaThaKa detail report.
    /// </summary>
    public static async Task<List<sp_WineImportationByPaThaKaReportResult>> GetAllResolvedAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_WineImportationByPaThaKaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);

        var wineTypes = await ReportLookupCache.GetWineTypeNamesAsync(db, cache);

        var rows = await Rows(db, request).ToListAsync();

        return rows
            .Select(row => row.ToResult(wineTypes))
            .ToList();
    }

    public static async Task<byte[]> CreateExcelWorkbookAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_WineImportationByPaThaKaReportRequest request,
        ReportQueryRequest pagingRequest,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var wineTypes = await ReportLookupCache.GetWineTypeNamesAsync(db, cache);

        var rows = await Rows(db, request).ToListAsync();

        var resolved = rows
            .Select(row => row.ToResult(wineTypes))
            .ToList();

        return await ExcelGenerator.CreateWorkbookAsync(resolved.AsQueryable(), pagingRequest, worksheetName);
    }

    private static IQueryable<WineImportationByPaThaKaFastRow> Rows(
        TradeNetDbContext db,
        sp_WineImportationByPaThaKaReportRequest request)
    {
        return from wineImportation in db.WineImportations.AsNoTracking()
               join paThaKa in db.PaThaKas.AsNoTracking() on wineImportation.PaThaKaId equals paThaKa.Id
               from nrcPrefix in db.Nrcprefixes.AsNoTracking()
                   .Where(prefix => wineImportation.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes.AsNoTracking()
                   .Where(prefixCode => wineImportation.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl11NrcPrefix in db.Nrcprefixes.AsNoTracking()
                   .Where(prefix => wineImportation.Fl11nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl11NrcPrefixCode in db.NrcprefixCodes.AsNoTracking()
                   .Where(prefixCode => wineImportation.Fl11nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl4NrcPrefix in db.Nrcprefixes.AsNoTracking()
                   .Where(prefix => wineImportation.Fl4nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl4NrcPrefixCode in db.NrcprefixCodes.AsNoTracking()
                   .Where(prefixCode => wineImportation.Fl4nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl5NrcPrefix in db.Nrcprefixes.AsNoTracking()
                   .Where(prefix => wineImportation.Fl5nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl5NrcPrefixCode in db.NrcprefixCodes.AsNoTracking()
                   .Where(prefixCode => wineImportation.Fl5nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
               select new WineImportationByPaThaKaFastRow
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   WineImportationNo = wineImportation.WineImportationNo,
                   CompanyName = paThaKa.CompanyName,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   Name = wineImportation.Name,
                   NRCNo = wineImportation.Nrctype == CurrentNrcType && wineImportation.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + wineImportation.Nrcno
                       : wineImportation.Nrctype == OldNrcType && wineImportation.Nrcno != string.Empty
                           ? wineImportation.Nrcno
                           : string.Empty,
                   FL11Name = wineImportation.Fl11name,
                   FL11NRCNo = wineImportation.Fl11nrctype == CurrentNrcType && wineImportation.Fl11nrcno != string.Empty
                       ? fl11NrcPrefix.StatePrefix.ToString() + "/" + fl11NrcPrefix.TownshipPrefix + fl11NrcPrefixCode.Code + wineImportation.Fl11nrcno
                       : wineImportation.Fl11nrctype == OldNrcType && wineImportation.Fl11nrcno != string.Empty
                           ? wineImportation.Fl11nrcno
                           : string.Empty,
                   FL4Name = wineImportation.Fl4name,
                   FL4NRCNo = wineImportation.Fl4nrctype == CurrentNrcType && wineImportation.Fl4nrcno != string.Empty
                       ? fl4NrcPrefix.StatePrefix.ToString() + "/" + fl4NrcPrefix.TownshipPrefix + fl4NrcPrefixCode.Code + wineImportation.Fl4nrcno
                       : wineImportation.Fl4nrctype == OldNrcType && wineImportation.Fl4nrcno != string.Empty
                           ? wineImportation.Fl4nrcno
                           : string.Empty,
                   FL5Name = wineImportation.Fl5name,
                   FL5NRCNo = wineImportation.Fl5nrctype == CurrentNrcType && wineImportation.Fl5nrcno != string.Empty
                       ? fl5NrcPrefix.StatePrefix.ToString() + "/" + fl5NrcPrefix.TownshipPrefix + fl5NrcPrefixCode.Code + wineImportation.Fl5nrcno
                       : wineImportation.Fl5nrctype == OldNrcType && wineImportation.Fl5nrcno != string.Empty
                           ? wineImportation.Fl5nrcno
                           : string.Empty,
                   WineTypeIds = wineImportation.WineTypeId,
                   IssuedDate = wineImportation.IssuedDate,
                   EndDate = wineImportation.EndDate
               };
    }

    private sealed class WineImportationByPaThaKaFastRow
    {
        public string CompanyRegistrationNo { get; init; } = null!;
        public string WineImportationNo { get; init; } = null!;
        public string CompanyName { get; init; } = null!;
        public string? UnitLevel { get; init; }
        public string StreetNumberStreetName { get; init; } = null!;
        public string QuarterCityTownship { get; init; } = null!;
        public string State { get; init; } = null!;
        public string Country { get; init; } = null!;
        public string? PostalCode { get; init; }
        public string Name { get; init; } = null!;
        public string? NRCNo { get; init; }
        public string FL11Name { get; init; } = null!;
        public string? FL11NRCNo { get; init; }
        public string FL4Name { get; init; } = null!;
        public string? FL4NRCNo { get; init; }
        public string FL5Name { get; init; } = null!;
        public string? FL5NRCNo { get; init; }
        public string WineTypeIds { get; init; } = null!;
        public DateTime IssuedDate { get; init; }
        public DateTime EndDate { get; init; }

        public sp_WineImportationByPaThaKaReportResult ToResult(
            IReadOnlyList<ReportLookupEntry> wineTypes)
        {
            return new sp_WineImportationByPaThaKaReportResult
            {
                CompanyRegistrationNo = CompanyRegistrationNo,
                WineImportationNo = WineImportationNo,
                CompanyName = CompanyName,
                UnitLevel = UnitLevel,
                StreetNumberStreetName = StreetNumberStreetName,
                QuarterCityTownship = QuarterCityTownship,
                State = State,
                Country = Country,
                PostalCode = PostalCode,
                Name = Name,
                NRCNo = NRCNo,
                FL11Name = FL11Name,
                FL11NRCNo = FL11NRCNo,
                FL4Name = FL4Name,
                FL4NRCNo = FL4NRCNo,
                FL5Name = FL5Name,
                FL5NRCNo = FL5NRCNo,
                WineType = ReportLookupCache.ResolveCsv(WineTypeIds, wineTypes),
                IssuedDate = IssuedDate,
                EndDate = EndDate
            };
        }
    }
}

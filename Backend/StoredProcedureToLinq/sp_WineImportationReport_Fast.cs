using API.DBContext;
using API.Model;
using API.Model.TradeNet;
using API.Service.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public static class sp_WineImportationReport_Fast
{
    private const string Approved = "Approved";
    private const string Summary = "Summary";
    private const string Valid = "Valid";
    private const string Invalid = "Invalid";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    public static async Task<ApiResult<sp_WineImportationReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_WineImportationReportRequest request,
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
            ? rows.Count()
            : (int?)null;

        var pageRows = rows
            .Skip(pageIndex * pageSize)
            .Take(pageSize + (totalCount.HasValue ? 0 : 1))
            .ToList();

        var results = pageRows
            .Select(row => row.ToResult(wineTypes))
            .ToList();

        if (totalCount.HasValue)
        {
            return ApiResult<sp_WineImportationReportResult>.CreatePageFromRows(
                results,
                totalCount.Value,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<sp_WineImportationReportResult>.CreateFastPageFromRows(
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
        sp_WineImportationReportRequest request,
        ReportQueryRequest pagingRequest,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var wineTypes = await ReportLookupCache.GetWineTypeNamesAsync(db, cache);

        var resolved = Rows(db, request)
            .Select(row => row.ToResult(wineTypes))
            .ToList();

        return await ExcelGenerator.CreateWorkbookAsync(resolved.AsQueryable(), pagingRequest, worksheetName);
    }

    private static IEnumerable<WineImportationFastRow> Rows(
        TradeNetDbContext db,
        sp_WineImportationReportRequest request)
    {
        if (request.Type == Summary)
        {
            return SummaryRows(db, request);
        }

        if (request.ApplyType == Valid)
        {
            return WineImportationDetailRows(db, wineImportation => wineImportation.EndDate > request.Date);
        }

        if (request.ApplyType == Invalid)
        {
            return WineImportationDetailRows(db, wineImportation => wineImportation.EndDate < request.Date);
        }

        return DefaultRows(db, request);
    }

    private static IQueryable<WineImportationFastRow> DefaultRows(
        TradeNetDbContext db,
        sp_WineImportationReportRequest request)
    {
        return from wineImportation in db.WineImportations.AsNoTracking()
               join paThaKa in db.PaThaKas.AsNoTracking() on wineImportation.PaThaKaId equals paThaKa.Id
               join registration in db.WineImportationRegistrations.AsNoTracking()
                   on wineImportation.WineImportationNo equals registration.WineImportationNo
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
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new WineImportationFastRow
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

    private static IEnumerable<WineImportationFastRow> SummaryRows(
        TradeNetDbContext db,
        sp_WineImportationReportRequest request)
    {
        return GroupedSummaryRow(db.WineImportationRegistrations.AsNoTracking()
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "New"
                    && registration.Status == Approved)
                .Select(_ => 1), "New")
            .AsEnumerable()
            .Concat(GroupedSummaryRow(db.WineImportationRegistrations.AsNoTracking()
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Cancel"
                    && registration.Status == Approved)
                .Select(_ => 1), "Cancel")
                .AsEnumerable())
            .Concat(CountSummaryRow(db.WineImportationRegistrations.AsNoTracking()
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Extension"
                    && registration.Status == Approved)
                .Select(_ => 1), "Extension")
                .AsEnumerable())
            .Concat(CountSummaryRow(db.WineImportations.AsNoTracking()
                .Where(wineImportation => wineImportation.EndDate > request.Date)
                .Select(_ => 1), Valid)
                .AsEnumerable())
            .Concat(CountSummaryRow(db.WineImportations.AsNoTracking()
                .Where(wineImportation => wineImportation.EndDate < request.Date)
                .Select(_ => 1), Invalid)
                .AsEnumerable());
    }

    private static IQueryable<WineImportationFastRow> GroupedSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .GroupBy(_ => 1)
            .Select(group => new WineImportationFastRow
            {
                ApplicationCount = group.Count(),
                ApplyType = applyType
            });
    }

    private static IQueryable<WineImportationFastRow> CountSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .DefaultIfEmpty()
            .GroupBy(_ => 1)
            .Select(group => new WineImportationFastRow
            {
                ApplicationCount = group.Sum(),
                ApplyType = applyType
            });
    }

    private static IQueryable<WineImportationFastRow> WineImportationDetailRows(
        TradeNetDbContext db,
        Expression<Func<WineImportation, bool>> predicate)
    {
        return from wineImportation in db.WineImportations.AsNoTracking().Where(predicate)
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
               select new WineImportationFastRow
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

    private sealed class WineImportationFastRow
    {
        public int? ApplicationCount { get; init; }
        public string? ApplyType { get; init; }
        public string? CompanyRegistrationNo { get; init; }
        public string? WineImportationNo { get; init; }
        public string? CompanyName { get; init; }
        public string? UnitLevel { get; init; }
        public string? StreetNumberStreetName { get; init; }
        public string? QuarterCityTownship { get; init; }
        public string? State { get; init; }
        public string? Country { get; init; }
        public string? PostalCode { get; init; }
        public string? Name { get; init; }
        public string? NRCNo { get; init; }
        public string? FL11Name { get; init; }
        public string? FL11NRCNo { get; init; }
        public string? FL4Name { get; init; }
        public string? FL4NRCNo { get; init; }
        public string? FL5Name { get; init; }
        public string? FL5NRCNo { get; init; }

        // Raw comma-separated WineType ids; the only string.Join correlated subquery
        // moved out of SQL and resolved from the cached lookup in ToResult.
        public string? WineTypeIds { get; init; }

        public DateTime? IssuedDate { get; init; }
        public DateTime? EndDate { get; init; }

        public sp_WineImportationReportResult ToResult(IReadOnlyList<ReportLookupEntry> wineTypes)
        {
            return new sp_WineImportationReportResult
            {
                ApplicationCount = ApplicationCount,
                ApplyType = ApplyType,
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

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

public static class sp_WineImportationRegistrationReport_Fast
{
    private const string Approved = "Approved";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    public static async Task<ApiResult<sp_WineImportationRegistrationReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_WineImportationRegistrationReportRequest request,
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
            return ApiResult<sp_WineImportationRegistrationReportResult>.CreatePageFromRows(
                results,
                totalCount.Value,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<sp_WineImportationRegistrationReportResult>.CreateFastPageFromRows(
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
        sp_WineImportationRegistrationReportRequest request,
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

    private static IQueryable<WineImportationRegistrationFastRow> Rows(
        TradeNetDbContext db,
        sp_WineImportationRegistrationReportRequest request)
    {
        return from registration in db.WineImportationRegistrations.AsNoTracking()
               join paThaKa in db.PaThaKas.AsNoTracking() on registration.PaThaKaId equals paThaKa.Id
               join accountTransaction in db.AccountTransactions.AsNoTracking() on registration.Id equals accountTransaction.TransactionId
               from nrcPrefix in db.Nrcprefixes.AsNoTracking()
                   .Where(prefix => registration.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes.AsNoTracking()
                   .Where(prefixCode => registration.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl11NrcPrefix in db.Nrcprefixes.AsNoTracking()
                   .Where(prefix => registration.Fl11nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl11NrcPrefixCode in db.NrcprefixCodes.AsNoTracking()
                   .Where(prefixCode => registration.Fl11nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl4NrcPrefix in db.Nrcprefixes.AsNoTracking()
                   .Where(prefix => registration.Fl4nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl4NrcPrefixCode in db.NrcprefixCodes.AsNoTracking()
                   .Where(prefixCode => registration.Fl4nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl5NrcPrefix in db.Nrcprefixes.AsNoTracking()
                   .Where(prefix => registration.Fl5nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl5NrcPrefixCode in db.NrcprefixCodes.AsNoTracking()
                   .Where(prefixCode => registration.Fl5nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && accountTransaction.IsPayment
                   && (request.PaymentType == string.Empty || accountTransaction.PaymentType == request.PaymentType)
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new WineImportationRegistrationFastRow
               {
                   Date = registration.CreatedDate,
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   CompanyName = paThaKa.CompanyName,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   WineImportationNo = registration.WineImportationNo,
                   Name = registration.Name,
                   NRCNo = registration.Nrctype == CurrentNrcType && registration.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + registration.Nrcno
                       : registration.Nrctype == OldNrcType && registration.Nrcno != string.Empty
                           ? registration.Nrcno
                           : string.Empty,
                   FL11Name = registration.Fl11name,
                   FL11NRCNo = registration.Fl11nrctype == CurrentNrcType && registration.Fl11nrcno != string.Empty
                       ? fl11NrcPrefix.StatePrefix.ToString() + "/" + fl11NrcPrefix.TownshipPrefix + fl11NrcPrefixCode.Code + registration.Fl11nrcno
                       : registration.Fl11nrctype == OldNrcType && registration.Fl11nrcno != string.Empty
                           ? registration.Fl11nrcno
                           : string.Empty,
                   FL4Name = registration.Fl4name,
                   FL4NRCNo = registration.Fl4nrctype == CurrentNrcType && registration.Fl4nrcno != string.Empty
                       ? fl4NrcPrefix.StatePrefix.ToString() + "/" + fl4NrcPrefix.TownshipPrefix + fl4NrcPrefixCode.Code + registration.Fl4nrcno
                       : registration.Fl4nrctype == OldNrcType && registration.Fl4nrcno != string.Empty
                           ? registration.Fl4nrcno
                           : string.Empty,
                   FL5Name = registration.Fl5name,
                   FL5NRCNo = registration.Fl5nrctype == CurrentNrcType && registration.Fl5nrcno != string.Empty
                       ? fl5NrcPrefix.StatePrefix.ToString() + "/" + fl5NrcPrefix.TownshipPrefix + fl5NrcPrefixCode.Code + registration.Fl5nrcno
                       : registration.Fl5nrctype == OldNrcType && registration.Fl5nrcno != string.Empty
                           ? registration.Fl5nrcno
                           : string.Empty,
                   WineTypeIds = registration.WineTypeId,
                   PaymentType = accountTransaction.PaymentType,
                   VoucherNo = accountTransaction.VoucherNo,
                   VoucherDate = accountTransaction.VoucherDate,
                   TotalAmount = accountTransaction.TotalAmount
               };
    }

    private sealed class WineImportationRegistrationFastRow
    {
        public DateTime? Date { get; init; }
        public string CompanyRegistrationNo { get; init; } = null!;
        public string CompanyName { get; init; } = null!;
        public string? UnitLevel { get; init; }
        public string StreetNumberStreetName { get; init; } = null!;
        public string QuarterCityTownship { get; init; } = null!;
        public string State { get; init; } = null!;
        public string Country { get; init; } = null!;
        public string? PostalCode { get; init; }
        public string WineImportationNo { get; init; } = null!;
        public string Name { get; init; } = null!;
        public string? NRCNo { get; init; }
        public string FL11Name { get; init; } = null!;
        public string? FL11NRCNo { get; init; }
        public string FL4Name { get; init; } = null!;
        public string? FL4NRCNo { get; init; }
        public string FL5Name { get; init; } = null!;
        public string? FL5NRCNo { get; init; }
        public string? WineTypeIds { get; init; }
        public string PaymentType { get; init; } = null!;
        public string? VoucherNo { get; init; }
        public DateTime? VoucherDate { get; init; }
        public double TotalAmount { get; init; }

        public sp_WineImportationRegistrationReportResult ToResult(
            IReadOnlyList<ReportLookupEntry> wineTypes)
        {
            return new sp_WineImportationRegistrationReportResult
            {
                Date = Date,
                CompanyRegistrationNo = CompanyRegistrationNo,
                CompanyName = CompanyName,
                UnitLevel = UnitLevel,
                StreetNumberStreetName = StreetNumberStreetName,
                QuarterCityTownship = QuarterCityTownship,
                State = State,
                Country = Country,
                PostalCode = PostalCode,
                WineImportationNo = WineImportationNo,
                Name = Name,
                NRCNo = NRCNo,
                FL11Name = FL11Name,
                FL11NRCNo = FL11NRCNo,
                FL4Name = FL4Name,
                FL4NRCNo = FL4NRCNo,
                FL5Name = FL5Name,
                FL5NRCNo = FL5NRCNo,
                WineType = ReportLookupCache.ResolveCsv(WineTypeIds, wineTypes),
                PaymentType = PaymentType,
                VoucherNo = VoucherNo,
                VoucherDate = VoucherDate,
                TotalAmount = TotalAmount
            };
        }
    }
}

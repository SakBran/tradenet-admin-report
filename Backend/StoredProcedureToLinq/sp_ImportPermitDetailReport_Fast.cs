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

        var rows = await Rows(db, request).ToListAsync();

        var resolved = rows
            .Select(row => row.ToResult(ports, countries))
            .ToList();

        return await ExcelGenerator.CreateWorkbookAsync(resolved.AsQueryable(), pagingRequest, worksheetName);
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

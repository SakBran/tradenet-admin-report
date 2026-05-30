using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_ImportLicenceDetailReportRequest
{
    public string Type { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PaThaKaTypeId { get; set; }
    public int ExportImportSectionId { get; set; }
    public int ExportImportMethodId { get; set; }
    public int ExportImportIncotermId { get; set; }
    public int SellerCountryId { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public int SakhanId { get; set; }
}

public sealed class sp_ImportLicenceDetailReportResult
{
    public int PaThaKaTypeId { get; set; }
    public string PaThaKaTypeCode { get; set; } = null!;
    public string PaThaKaTypeName { get; set; } = null!;
    public int? SakhanId { get; set; }
    public string? SakhanCode { get; set; }
    public string? SakhanName { get; set; }
    public int ExportImportSectionId { get; set; }
    public int ExportImportMethodId { get; set; }
    public int ExportImportIncotermId { get; set; }
    public int SellerCountryId { get; set; }
    public string SectionCode { get; set; } = null!;
    public string SectionName { get; set; } = null!;
    public string LicenceNo { get; set; } = null!;
    public DateTime? LicenceDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string SellerName { get; set; } = null!;
    public string SellerAddress { get; set; } = null!;
    public string? SellerCountry { get; set; }
    public string PortofDischarge { get; set; } = null!;
    public DateTime? LastDate { get; set; }
    public string MethodName { get; set; } = null!;
    public string ConsignedCountry { get; set; } = null!;
    public string CountryofOrigin { get; set; } = null!;
    public string HSCode { get; set; } = null!;
    public string? HSDescription { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Conditions { get; set; }
    public string ApplicationNo { get; set; } = null!;
    public DateTime ApplicationDate { get; set; }
    public string? FESCNo { get; set; }
    public string? CommodityType { get; set; }
    public DateTime? ApproveDate { get; set; }
}

public sealed class sp_ImportLicenceDetailReportRow
{
    public int? PaThaKaTypeId { get; set; }
    public string? PaThaKaTypeCode { get; set; }
    public string? PaThaKaTypeName { get; set; }
    public int? ExportImportSectionId { get; set; }
    public int? ExportImportMethodId { get; set; }
    public int? ExportImportIncotermId { get; set; }
    public int? SellerCountryId { get; set; }
    public string? SectionCode { get; set; }
    public string? SectionName { get; set; }
    public string? LicenceNo { get; set; }
    public DateTime? LicenceDate { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? CompanyName { get; set; }
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? SellerName { get; set; }
    public string? SellerAddress { get; set; }
    public string? SellerCountry { get; set; }
    public string? PortofDischarge { get; set; }
    public DateTime? LastDate { get; set; }
    public string? MethodName { get; set; }
    public string? ConsignedCountry { get; set; }
    public string? CountryofOrigin { get; set; }
    public string? HSCode { get; set; }
    public string? HSDescription { get; set; }
    public string? Unit { get; set; }
    public decimal? Price { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Conditions { get; set; }
    public string? ApplicationNo { get; set; }
    public DateTime? ApplicationDate { get; set; }
    public string? FESCNo { get; set; }
    public string? CommodityType { get; set; }
    public DateTime? ApproveDate { get; set; }
    public int TotalCount { get; set; }

    public sp_ImportLicenceDetailReportResult ToResult() => new()
    {
        PaThaKaTypeId = PaThaKaTypeId ?? 0,
        PaThaKaTypeCode = PaThaKaTypeCode ?? string.Empty,
        PaThaKaTypeName = PaThaKaTypeName ?? string.Empty,
        ExportImportSectionId = ExportImportSectionId ?? 0,
        ExportImportMethodId = ExportImportMethodId ?? 0,
        ExportImportIncotermId = ExportImportIncotermId ?? 0,
        SellerCountryId = SellerCountryId ?? 0,
        SectionCode = SectionCode ?? string.Empty,
        SectionName = SectionName ?? string.Empty,
        LicenceNo = LicenceNo ?? string.Empty,
        LicenceDate = LicenceDate,
        CompanyRegistrationNo = CompanyRegistrationNo ?? string.Empty,
        CompanyName = CompanyName ?? string.Empty,
        UnitLevel = UnitLevel,
        StreetNumberStreetName = StreetNumberStreetName ?? string.Empty,
        QuarterCityTownship = QuarterCityTownship ?? string.Empty,
        State = State ?? string.Empty,
        Country = Country ?? string.Empty,
        PostalCode = PostalCode,
        SellerName = SellerName ?? string.Empty,
        SellerAddress = SellerAddress ?? string.Empty,
        SellerCountry = SellerCountry,
        PortofDischarge = PortofDischarge ?? string.Empty,
        LastDate = LastDate,
        MethodName = MethodName ?? string.Empty,
        ConsignedCountry = ConsignedCountry ?? string.Empty,
        CountryofOrigin = CountryofOrigin ?? string.Empty,
        HSCode = HSCode ?? string.Empty,
        HSDescription = HSDescription,
        Unit = Unit,
        Price = Price ?? 0m,
        Quantity = Quantity ?? 0m,
        Amount = Amount ?? 0m,
        Currency = Currency,
        Conditions = Conditions,
        ApplicationNo = ApplicationNo ?? string.Empty,
        ApplicationDate = ApplicationDate ?? default,
        FESCNo = FESCNo,
        CommodityType = CommodityType,
        ApproveDate = ApproveDate,
    };
}

public static class sp_ImportLicenceDetailReport
{
    private const string New = "New";
    private const string Approved = "Approved";
    private const string PaThaKaCardType = "Pa Tha Ka";
    private const string IndividualTradingCardType = "Individual Trading";

    /// <summary>
    /// Executes <c>dbo.sp_ImportLicenceDetailReport_pagination</c> (DB-side paging via INSERT-EXEC
    /// wrapper over the untouched original).
    /// </summary>
    public static async Task<List<sp_ImportLicenceDetailReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new[]
        {
            new SqlParameter("@Type", request.Type ?? string.Empty),
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@PaThaKaTypeId", request.PaThaKaTypeId),
            new SqlParameter("@ExportImportSectionId", request.ExportImportSectionId),
            new SqlParameter("@ExportImportMethodId", request.ExportImportMethodId),
            new SqlParameter("@ExportImportIncotermId", request.ExportImportIncotermId),
            new SqlParameter("@SellerCountryId", request.SellerCountryId),
            new SqlParameter("@CompanyRegistrationNo", request.CompanyRegistrationNo ?? string.Empty),
            new SqlParameter("@SakhanId", request.SakhanId),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        const string sql =
            "EXEC dbo.sp_ImportLicenceDetailReport_pagination @Type, @FromDate, @ToDate, @PaThaKaTypeId, " +
            "@ExportImportSectionId, @ExportImportMethodId, @ExportImportIncotermId, @SellerCountryId, " +
            "@CompanyRegistrationNo, @SakhanId, @SortColumn, @SortOrder, @PageIndex, @PageSize";

        return await db.Database
            .SqlQueryRaw<sp_ImportLicenceDetailReportRow>(sql, parameters)
            .ToListAsync();
    }

    public static IQueryable<sp_ImportLicenceDetailReportResult> Query(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return request.Type switch
        {
            "Oversea" => OverseaRows(db, request).AsSplitQuery(),
            "Border" => BorderPaThaKaRows(db, request)
                .AsSplitQuery()
                .AsEnumerable()
                .Concat(BorderIndividualTradingRows(db, request).AsSplitQuery().AsEnumerable())
                .OrderBy(row => row.LicenceDate)
                .AsQueryable(),
            _ => OverseaRows(db, request)
                .Where(_ => false)
                .AsSplitQuery()
        };
    }

    private static IQueryable<sp_ImportLicenceDetailReportResult> OverseaRows(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return
            from licence in db.ImportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.ImportLicenceItems on licence.Id equals item.ImportLicenceId
            join unit in db.Units on item.UnitId equals unit.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join sellerCountry in db.Countries on licence.SellerCountryId equals sellerCountry.Id
            join method in db.ExportImportMethods on licence.ExportImportMethodId equals method.Id
            join incoterm in db.ExportImportIncoterms on licence.ExportImportIncotermId equals incoterm.Id
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
            orderby licence.CreatedDate
            select new sp_ImportLicenceDetailReportResult
            {
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
                ConsignedCountry = string.Join(",",
                    from country in db.Countries
                    where ("," + licence.ConsignedCountryId + ",").Contains("," + country.Id.ToString() + ",")
                    select country.Name ?? string.Empty),
                CountryofOrigin = string.Join(",",
                    from country in db.Countries
                    where ("," + licence.CountryofOriginId + ",").Contains("," + country.Id.ToString() + ",")
                    select country.Name ?? string.Empty),
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

    private static IQueryable<sp_ImportLicenceDetailReportResult> BorderPaThaKaRows(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return
            from licence in db.BorderImportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderImportLicenceItems on licence.Id equals item.BorderImportLicenceId
            join unit in db.Units on item.UnitId equals unit.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join sellerCountry in db.Countries on licence.SellerCountryId equals sellerCountry.Id
            join method in db.ExportImportMethods on licence.ExportImportMethodId equals method.Id
            join incoterm in db.ExportImportIncoterms on licence.ExportImportIncotermId equals incoterm.Id
            join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
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
            orderby licence.CreatedDate
            select new sp_ImportLicenceDetailReportResult
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
                ConsignedCountry = string.Join(",",
                    from consignedCountry in db.Countries
                    where ("," + licence.ConsignedCountryId + ",").Contains("," + consignedCountry.Id.ToString() + ",")
                    select consignedCountry.Name ?? string.Empty),
                CountryofOrigin = string.Join(",",
                    from countryOfOrigin in db.Countries
                    where ("," + licence.CountryofOriginId + ",").Contains("," + countryOfOrigin.Id.ToString() + ",")
                    select countryOfOrigin.Name ?? string.Empty),
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

    private static IQueryable<sp_ImportLicenceDetailReportResult> BorderIndividualTradingRows(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request)
    {
        return
            from licence in db.BorderImportLicences
            join individualTrading in db.IndividualTradings on licence.IndividualTradingId equals individualTrading.Id
            join paThaKaType in db.PaThaKaTypes on individualTrading.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderImportLicenceItems on licence.Id equals item.BorderImportLicenceId
            join unit in db.Units on item.UnitId equals unit.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join sellerCountry in db.Countries on licence.SellerCountryId equals sellerCountry.Id
            join method in db.ExportImportMethods on licence.ExportImportMethodId equals method.Id
            join incoterm in db.ExportImportIncoterms on licence.ExportImportIncotermId equals incoterm.Id
            join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
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
            orderby licence.CreatedDate
            select new sp_ImportLicenceDetailReportResult
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
                ConsignedCountry = string.Join(",",
                    from consignedCountry in db.Countries
                    where ("," + licence.ConsignedCountryId + ",").Contains("," + consignedCountry.Id.ToString() + ",")
                    select consignedCountry.Name ?? string.Empty),
                CountryofOrigin = string.Join(",",
                    from countryOfOrigin in db.Countries
                    where ("," + licence.CountryofOriginId + ",").Contains("," + countryOfOrigin.Id.ToString() + ",")
                    select countryOfOrigin.Name ?? string.Empty),
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
}

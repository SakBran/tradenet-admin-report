using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_ImportLicencePendingDetailReportRequest
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

public sealed class sp_ImportLicencePendingDetailReportResult
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
    public string? ConsignedCountry { get; set; }
    public string? CountryofOrigin { get; set; }
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
}

public static class sp_ImportLicencePendingDetailReport
{
    private const string New = "New";
    private const string Pending = "Pending";
    private const string PaThaKaCardType = "Pa Tha Ka";
    private const string IndividualTradingCardType = "Individual Trading";

    public static IQueryable<sp_ImportLicencePendingDetailReportResult> Query(
        TradeNetDbContext db,
        sp_ImportLicencePendingDetailReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return request.Type switch
        {
            "Oversea" => OverseaRows(db, request).OrderBy(row => row.LicenceDate),
            "Border" => BorderPaThaKaRows(db, request)
                .AsEnumerable()
                .Concat(BorderIndividualTradingRows(db, request).AsEnumerable())
                .OrderBy(row => row.LicenceDate)
                .AsQueryable(),
            _ => OverseaRows(db, request)
                .Where(_ => false)
                .OrderBy(row => row.LicenceDate)
        };
    }

    private static IQueryable<sp_ImportLicencePendingDetailReportResult> OverseaRows(
        TradeNetDbContext db,
        sp_ImportLicencePendingDetailReportRequest request)
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
                && licence.Status == Pending
                && licence.ApplicationDate >= request.FromDate
                && licence.ApplicationDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKaType.Id == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                && (request.SellerCountryId == 0 || licence.SellerCountryId == request.SellerCountryId)
            select new sp_ImportLicencePendingDetailReportResult
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
                CommodityType = licence.CommodityType
            };
    }

    private static IQueryable<sp_ImportLicencePendingDetailReportResult> BorderPaThaKaRows(
        TradeNetDbContext db,
        sp_ImportLicencePendingDetailReportRequest request)
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
                && licence.Status == Pending
                && licence.CardType == PaThaKaCardType
                && licence.ApplicationDate >= request.FromDate
                && licence.ApplicationDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKaType.Id == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                && (request.SellerCountryId == 0 || licence.SellerCountryId == request.SellerCountryId)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select new sp_ImportLicencePendingDetailReportResult
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
                CommodityType = licence.CommodityType
            };
    }

    private static IQueryable<sp_ImportLicencePendingDetailReportResult> BorderIndividualTradingRows(
        TradeNetDbContext db,
        sp_ImportLicencePendingDetailReportRequest request)
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
                && licence.Status == Pending
                && licence.CardType == IndividualTradingCardType
                && licence.ApplicationDate >= request.FromDate
                && licence.ApplicationDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty || individualTrading.Tinno == request.CompanyRegistrationNo)
                && (request.PaThaKaTypeId == 0 || paThaKaType.Id == request.PaThaKaTypeId)
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                && (request.SellerCountryId == 0 || licence.SellerCountryId == request.SellerCountryId)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select new sp_ImportLicencePendingDetailReportResult
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
                CommodityType = licence.CommodityType
            };
    }
}

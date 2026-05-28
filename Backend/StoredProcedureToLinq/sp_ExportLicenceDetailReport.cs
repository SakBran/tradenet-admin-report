using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_ExportLicenceDetailReportRequest
{
    public string Type { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PaThaKaTypeId { get; set; }
    public int ExportImportSectionId { get; set; }
    public int ExportImportMethodId { get; set; }
    public int ExportImportIncotermId { get; set; }
    public int BuyerCountryId { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public int SakhanId { get; set; }
}

public sealed class sp_ExportLicenceDetailReportResult
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
    public int BuyerCountryId { get; set; }
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
    public string BuyerName { get; set; } = null!;
    public string BuyerAddress { get; set; } = null!;
    public string? BuyerCountry { get; set; }
    public string PortofExport { get; set; } = null!;
    public string PortofDischarge { get; set; } = null!;
    public DateTime? LastDate { get; set; }
    public string MethodName { get; set; } = null!;
    public string DestinationCountry { get; set; } = null!;
    public string? ConsignedCountry { get; set; }
    public string? CountryofOrigin { get; set; }
    public string HSCode { get; set; } = null!;
    public string HSDescription { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Conditions { get; set; }
    public DateTime? ApproveDate { get; set; }
}

public static class sp_ExportLicenceDetailReport
{
    private const string New = "New";
    private const string Approved = "Approved";
    private const string PaThaKaCardType = "Pa Tha Ka";
    private const string IndividualTradingCardType = "Individual Trading";

    public static IQueryable<sp_ExportLicenceDetailReportResult> Query(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request)
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

    private static IQueryable<sp_ExportLicenceDetailReportResult> OverseaRows(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request)
    {
        return
            from licence in db.ExportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.ExportLicenceItems on licence.Id equals item.ExportLicenceId
            join unit in db.Units on item.UnitId equals unit.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join buyerCountry in db.Countries on licence.BuyerCountryId equals buyerCountry.Id
            join method in db.ExportImportMethods on licence.ExportImportMethodId equals method.Id
            join consignedCountry in db.Countries on licence.ConsignedCountryId equals consignedCountry.Id
            join countryofOrigin in db.Countries on licence.CountryofOriginId equals countryofOrigin.Id
            join incoterm in db.ExportImportIncoterms on licence.ExportImportIncotermId equals incoterm.Id
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
            select new sp_ExportLicenceDetailReportResult
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
                PortofExport = string.Join(",",
                    from port in db.PortOfDischarges
                    where ("," + licence.PortofExportId + ",").Contains("," + port.Id.ToString() + ",")
                    select port.Name ?? string.Empty),
                PortofDischarge = licence.PortofDischarge,
                LastDate = licence.LastDate,
                MethodName = method.Name,
                DestinationCountry = string.Join(",",
                    from country in db.Countries
                    where ("," + licence.DestinationCountryId + ",").Contains("," + country.Id.ToString() + ",")
                    select country.Name ?? string.Empty),
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
                ApproveDate = licence.ApproveDate
            };
    }

    private static IQueryable<sp_ExportLicenceDetailReportResult> BorderPaThaKaRows(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request)
    {
        return
            from licence in db.BorderExportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderExportLicenceItems on licence.Id equals item.BorderExportLicenceId
            join unit in db.Units on item.UnitId equals unit.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join buyerCountry in db.Countries on licence.BuyerCountryId equals buyerCountry.Id
            join method in db.ExportImportMethods on licence.ExportImportMethodId equals method.Id
            join consignedCountry in db.Countries on licence.ConsignedCountryId equals consignedCountry.Id
            join countryofOrigin in db.Countries on licence.CountryofOriginId equals countryofOrigin.Id
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
                && (request.BuyerCountryId == 0 || licence.BuyerCountryId == request.BuyerCountryId)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select new sp_ExportLicenceDetailReportResult
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
                PortofExport = string.Join(",",
                    from port in db.PortOfDischarges
                    where ("," + licence.PortofExportId + ",").Contains("," + port.Id.ToString() + ",")
                    select port.Name ?? string.Empty),
                PortofDischarge = licence.PortofDischarge,
                LastDate = licence.LastDate,
                MethodName = method.Name,
                DestinationCountry = string.Join(",",
                    from country in db.Countries
                    where ("," + licence.DestinationCountryId + ",").Contains("," + country.Id.ToString() + ",")
                    select country.Name ?? string.Empty),
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
                ApproveDate = licence.ApproveDate
            };
    }

    private static IQueryable<sp_ExportLicenceDetailReportResult> BorderIndividualTradingRows(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request)
    {
        return
            from licence in db.BorderExportLicences
            join individualTrading in db.IndividualTradings on licence.IndividualTradingId equals individualTrading.Id
            join paThaKaType in db.PaThaKaTypes on individualTrading.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderExportLicenceItems on licence.Id equals item.BorderExportLicenceId
            join unit in db.Units on item.UnitId equals unit.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join buyerCountry in db.Countries on licence.BuyerCountryId equals buyerCountry.Id
            join method in db.ExportImportMethods on licence.ExportImportMethodId equals method.Id
            join consignedCountry in db.Countries on licence.ConsignedCountryId equals consignedCountry.Id
            join countryofOrigin in db.Countries on licence.CountryofOriginId equals countryofOrigin.Id
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
                && (request.BuyerCountryId == 0 || licence.BuyerCountryId == request.BuyerCountryId)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select new sp_ExportLicenceDetailReportResult
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
                PortofExport = string.Join(",",
                    from port in db.PortOfDischarges
                    where ("," + licence.PortofExportId + ",").Contains("," + port.Id.ToString() + ",")
                    select port.Name ?? string.Empty),
                PortofDischarge = licence.PortofDischarge,
                LastDate = licence.LastDate,
                MethodName = method.Name,
                DestinationCountry = string.Join(",",
                    from country in db.Countries
                    where ("," + licence.DestinationCountryId + ",").Contains("," + country.Id.ToString() + ",")
                    select country.Name ?? string.Empty),
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
                ApproveDate = licence.ApproveDate
            };
    }
}

using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_ExportPermitDetailReportRequest
{
    public string Type { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PaThaKaTypeId { get; set; }
    public int ExportImportSectionId { get; set; }
    public int BuyerCountryId { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public int SakhanId { get; set; }
}

public sealed class sp_ExportPermitDetailReportResult
{
    public int PaThaKaTypeId { get; set; }
    public string PaThaKaTypeCode { get; set; } = null!;
    public string PaThaKaTypeName { get; set; } = null!;
    public int? SakhanId { get; set; }
    public string? SakhanCode { get; set; }
    public string? SakhanName { get; set; }
    public int ExportImportSectionId { get; set; }
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
    public string ConsigneeName { get; set; } = null!;
    public string ConsigneeAddress { get; set; } = null!;
    public string? BuyerCountry { get; set; }
    public string PortofExport { get; set; } = null!;
    public string PortofDischarge { get; set; } = null!;
    public string DestinationCountry { get; set; } = null!;
    public DateTime? LastDate { get; set; }
    public string? ConsignedCountry { get; set; }
    public string CountryofOrigin { get; set; } = null!;
    public string HSCode { get; set; } = null!;
    public string HSDescription { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string NRCNo { get; set; } = null!;
    public string PermitType { get; set; } = null!;
    public string? Conditions { get; set; }
    public DateTime? ApproveDate { get; set; }
}

public static class sp_ExportPermitDetailReport
{
    private const string New = "New";
    private const string Approved = "Approved";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_ExportPermitDetailReportResult> Query(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return OverseaRows(db, request).Concat(BorderRows(db, request));
    }

    private static IQueryable<sp_ExportPermitDetailReportResult> OverseaRows(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request)
    {
        return
            from permit in db.ExportPermits
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.ExportPermitItems on permit.Id equals item.ExportPermitId
            join unit in db.Units on item.UnitId equals unit.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            join buyerCountry in db.Countries on permit.BuyerCountryId equals buyerCountry.Id
            join consignedCountry in db.Countries on permit.ConsignedCountryId equals consignedCountry.Id
            from nrcPrefix in db.Nrcprefixes
                .Where(prefix => permit.NrcprefixId == prefix.Id)
                .DefaultIfEmpty()
            from nrcPrefixCode in db.NrcprefixCodes
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
            select new sp_ExportPermitDetailReportResult
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
                PortofExport = string.Join(",",
                    from port in db.PortOfDischarges
                    where ("," + permit.PortofExportId + ",").Contains("," + port.Id.ToString() + ",")
                    select port.Name ?? string.Empty),
                PortofDischarge = permit.PortofDischarge,
                DestinationCountry = string.Join(",",
                    from country in db.Countries
                    where ("," + permit.DestinationCountryId + ",").Contains("," + country.Id.ToString() + ",")
                    select country.Name ?? string.Empty),
                LastDate = permit.LastDate,
                ConsignedCountry = consignedCountry.Name,
                CountryofOrigin = string.Join(",",
                    from country in db.Countries
                    where ("," + permit.CountryofOriginId + ",").Contains("," + country.Id.ToString() + ",")
                    select country.Name ?? string.Empty),
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

    private static IQueryable<sp_ExportPermitDetailReportResult> BorderRows(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request)
    {
        return
            from permit in db.BorderExportPermits
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join paThaKaType in db.PaThaKaTypes on paThaKa.PaThaKaTypeId equals paThaKaType.Id
            join item in db.BorderExportPermitItems on permit.Id equals item.BorderExportPermitId
            join unit in db.Units on item.UnitId equals unit.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            join buyerCountry in db.Countries on permit.BuyerCountryId equals buyerCountry.Id
            join consignedCountry in db.Countries on permit.ConsignedCountryId equals consignedCountry.Id
            join sakhan in db.Sakhans on permit.SakhanId equals sakhan.Id
            from nrcPrefix in db.Nrcprefixes
                .Where(prefix => permit.NrcprefixId == prefix.Id)
                .DefaultIfEmpty()
            from nrcPrefixCode in db.NrcprefixCodes
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
            select new sp_ExportPermitDetailReportResult
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
                PortofExport = string.Join(",",
                    from port in db.PortOfDischarges
                    where ("," + permit.PortofExportId + ",").Contains("," + port.Id.ToString() + ",")
                    select port.Name ?? string.Empty),
                PortofDischarge = permit.PortofDischarge,
                DestinationCountry = string.Join(",",
                    from country in db.Countries
                    where ("," + permit.DestinationCountryId + ",").Contains("," + country.Id.ToString() + ",")
                    select country.Name ?? string.Empty),
                LastDate = permit.LastDate,
                ConsignedCountry = consignedCountry.Name,
                CountryofOrigin = string.Join(",",
                    from country in db.Countries
                    where ("," + permit.CountryofOriginId + ",").Contains("," + country.Id.ToString() + ",")
                    select country.Name ?? string.Empty),
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
}

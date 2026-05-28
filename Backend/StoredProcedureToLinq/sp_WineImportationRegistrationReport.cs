using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_WineImportationRegistrationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string ApplyType { get; set; } = string.Empty;
}

public sealed class sp_WineImportationRegistrationReportResult
{
    public DateTime? Date { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string WineImportationNo { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? NRCNo { get; set; }
    public string FL11Name { get; set; } = null!;
    public string? FL11NRCNo { get; set; }
    public string FL4Name { get; set; } = null!;
    public string? FL4NRCNo { get; set; }
    public string FL5Name { get; set; } = null!;
    public string? FL5NRCNo { get; set; }
    public string WineType { get; set; } = string.Empty;
    public string PaymentType { get; set; } = null!;
    public string? VoucherNo { get; set; }
    public DateTime? VoucherDate { get; set; }
    public double TotalAmount { get; set; }
}

public static class sp_WineImportationRegistrationReport
{
    private const string Approved = "Approved";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_WineImportationRegistrationReportResult> Query(
        TradeNetDbContext db,
        sp_WineImportationRegistrationReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from registration in db.WineImportationRegistrations
               join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
               join accountTransaction in db.AccountTransactions on registration.Id equals accountTransaction.TransactionId
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => registration.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => registration.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl11NrcPrefix in db.Nrcprefixes
                   .Where(prefix => registration.Fl11nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl11NrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => registration.Fl11nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl4NrcPrefix in db.Nrcprefixes
                   .Where(prefix => registration.Fl4nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl4NrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => registration.Fl4nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl5NrcPrefix in db.Nrcprefixes
                   .Where(prefix => registration.Fl5nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl5NrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => registration.Fl5nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && accountTransaction.IsPayment
                   && (request.PaymentType == string.Empty || accountTransaction.PaymentType == request.PaymentType)
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new sp_WineImportationRegistrationReportResult
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
                   WineType = string.Join(",",
                       from wineType in db.WineTypes
                       where ("," + registration.WineTypeId + ",").Contains("," + wineType.Id.ToString() + ",")
                       select wineType.Name),
                   PaymentType = accountTransaction.PaymentType,
                   VoucherNo = accountTransaction.VoucherNo,
                   VoucherDate = accountTransaction.VoucherDate,
                   TotalAmount = accountTransaction.TotalAmount
               };
    }
}

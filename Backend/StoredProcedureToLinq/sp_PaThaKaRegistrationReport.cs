using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_PaThaKaRegistrationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string ApplyType { get; set; } = string.Empty;
}

public sealed class sp_PaThaKaRegistrationReportResult
{
    public DateTime? Date { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string PaymentType { get; set; } = null!;
    public string? VoucherNo { get; set; }
    public DateTime? VoucherDate { get; set; }
    public double TotalAmount { get; set; }
}

public static class sp_PaThaKaRegistrationReport
{
    private const string Approved = "Approved";

    public static IQueryable<sp_PaThaKaRegistrationReportResult> Query(
        TradeNetDbContext db,
        sp_PaThaKaRegistrationReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from registration in db.PaThaKaRegistrations
               join accountTransaction in db.AccountTransactions on registration.Id equals accountTransaction.TransactionId
               join businessType in db.BusinessTypes on registration.BusinessTypeId equals businessType.Id
               join lineofBusiness in db.LineofBusinesses on registration.LineofBusinessId equals lineofBusiness.Id
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && accountTransaction.IsPayment
                   && (request.PaymentType == string.Empty || accountTransaction.PaymentType == request.PaymentType)
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new sp_PaThaKaRegistrationReportResult
               {
                   Date = registration.CreatedDate,
                   CompanyRegistrationNo = registration.CompanyRegistrationNo,
                   CompanyName = registration.CompanyName,
                   BusinessType = businessType.Name,
                   LineofBusiness = lineofBusiness.Name,
                   UnitLevel = registration.UnitLevel,
                   StreetNumberStreetName = registration.StreetNumberStreetName,
                   QuarterCityTownship = registration.QuarterCityTownship,
                   State = registration.State,
                   Country = registration.Country,
                   PostalCode = registration.PostalCode,
                   PaymentType = accountTransaction.PaymentType,
                   VoucherNo = accountTransaction.VoucherNo,
                   VoucherDate = accountTransaction.VoucherDate,
                   TotalAmount = accountTransaction.TotalAmount
               };
    }
}

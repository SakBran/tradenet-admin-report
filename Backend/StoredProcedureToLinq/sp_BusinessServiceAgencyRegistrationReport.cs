using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_BusinessServiceAgencyRegistrationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string ApplyType { get; set; } = string.Empty;
}

public sealed class sp_BusinessServiceAgencyRegistrationReportResult
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
    public string BusinessServiceAgencyNo { get; set; } = null!;
    public string AuthorizeCompany { get; set; } = null!;
    public string PaymentType { get; set; } = null!;
    public string? VoucherNo { get; set; }
    public DateTime? VoucherDate { get; set; }
    public double TotalAmount { get; set; }
}

public static class sp_BusinessServiceAgencyRegistrationReport
{
    private const string Approved = "Approved";

    public static IQueryable<sp_BusinessServiceAgencyRegistrationReportResult> Query(
        TradeNetDbContext db,
        sp_BusinessServiceAgencyRegistrationReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return
            from registration in db.BusinessServiceAgencyRegistrations
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            join accountTransaction in db.AccountTransactions on registration.Id equals accountTransaction.TransactionId
            where registration.ApplyType == request.ApplyType
                && registration.Status == Approved
                && accountTransaction.IsPayment
                && (request.PaymentType == string.Empty || accountTransaction.PaymentType == request.PaymentType)
                && registration.CreatedDate >= request.FromDate
                && registration.CreatedDate <= request.ToDate
            select new sp_BusinessServiceAgencyRegistrationReportResult
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
                BusinessServiceAgencyNo = registration.BusinessServiceAgencyNo,
                AuthorizeCompany = registration.AuthorizeCompany,
                PaymentType = accountTransaction.PaymentType,
                VoucherNo = accountTransaction.VoucherNo,
                VoucherDate = accountTransaction.VoucherDate,
                TotalAmount = accountTransaction.TotalAmount
            };
    }
}

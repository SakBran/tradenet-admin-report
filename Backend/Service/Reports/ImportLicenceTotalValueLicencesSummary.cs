using System;
using System.Collections.Generic;

namespace API.Service.Reports
{
    /// <summary>
    /// One licence-level row (one per licence + currency) for the drill-down list
    /// reached from the By Section / Method / Seller Country / Company summaries. Its
    /// row count reconciles with those summaries' per-currency "No of Licences".
    /// </summary>
    public sealed class ReportLicenceListResult
    {
        public string? SectionName { get; set; }
        public string? LicenceNo { get; set; }
        public DateTime? LicenceDate { get; set; }
        public string? CompanyRegistrationNo { get; set; }
        public string? CompanyName { get; set; }
        public string? MethodName { get; set; }
        public string? SellerCountry { get; set; }
        public string? Currency { get; set; }
        public decimal? TotalValue { get; set; }
    }

    /// <summary>Section 1 row: total value summed for one currency.</summary>
    public sealed class TotalValueByCurrencyRow
    {
        public string Currency { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
    }

    /// <summary>Section 2 row: distinct licence count for one Pa Tha Ka type.</summary>
    public sealed class TotalLicencesByPaThaKaTypeRow
    {
        public string PaThaKaType { get; set; } = string.Empty;
        public int NoOfLicences { get; set; }
    }

    /// <summary>
    /// The Total Value &amp; Licences report payload, mirroring the legacy
    /// ImportLicenceByTotalValueLicenceReport.rdlc: per-currency value totals,
    /// per-Pa-Tha-Ka-Type distinct licence counts and the USD-normalised grand total.
    /// </summary>
    public sealed class ImportLicenceTotalValueLicencesSummary
    {
        public List<TotalValueByCurrencyRow> TotalValueByCurrency { get; set; } = new();
        public List<TotalLicencesByPaThaKaTypeRow> TotalLicencesByPaThaKaType { get; set; } = new();
        public decimal TotalUsdValue { get; set; }
    }
}

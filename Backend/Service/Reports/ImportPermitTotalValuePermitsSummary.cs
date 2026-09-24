using System.Collections.Generic;

namespace API.Service.Reports
{
    /// <summary>Distinct permit count for one Pa Tha Ka type.</summary>
    public sealed class TotalPermitsByPaThaKaTypeRow
    {
        public string PaThaKaType { get; set; } = string.Empty;
        public int NoOfPermits { get; set; }
    }

    /// <summary>
    /// Composite Import Permit summary: value by currency, distinct permit count by
    /// Pa Tha Ka type, and the exchange-rate-normalised USD grand total.
    /// </summary>
    public sealed class ImportPermitTotalValuePermitsSummary
    {
        public List<TotalValueByCurrencyRow> TotalValueByCurrency { get; set; } = new();
        public List<TotalPermitsByPaThaKaTypeRow> TotalPermitsByPaThaKaType { get; set; } = new();
        public decimal TotalUsdValue { get; set; }
    }
}

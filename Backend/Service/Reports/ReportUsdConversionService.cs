using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.EntityFrameworkCore;

namespace API.Service.Reports
{
    /// <summary>
    /// Fills the "Total USD Value" column on the Daily licence/permit summary reports.
    ///
    /// Reconstructs the FX conversion from the legacy Tradenet 2.0 report
    /// (TradenetAdmin/Business/Reports.cs): each detail amount was converted to USD via
    /// MMK using the Central Bank of Myanmar daily rate for the licence's date, then the
    /// converted amounts were summed per report group.
    ///
    ///   * USD rows pass through unchanged (factor 1).
    ///   * KRW / JPY are quoted per 100 units in the ExchangeRate table, so their factor
    ///     is divided by 100 (mirrors the old "/ 100" branch).
    ///   * everything else: amount * (currencyRate / usdRate)  — rate = MMK per unit.
    ///
    /// The Daily reports group by (Date, Currency[, Sakhan]); the FX factor depends only on
    /// (Date, Currency), so it is constant within a group and
    ///   Sum(amount * factor) == factor * Sum(amount) == factor * TotalValue.
    /// We therefore convert each group's already-summed <see cref="ReportAggregateResult.TotalValue"/>
    /// instead of re-materializing the detail rows. Result matches the legacy per-row sum exactly.
    /// </summary>
    public static class ReportUsdConversionService
    {
        private const string Usd = "USD";

        // Currencies the ExchangeRate table quotes per 100 units (CBM convention).
        private static readonly HashSet<string> PerHundredCurrencies =
            new(StringComparer.OrdinalIgnoreCase) { "KRW", "JPY" };

        /// <summary>
        /// Populates <see cref="ReportAggregateResult.TotalUSDValue"/> on each Daily group.
        /// Loads every relevant FX rate in a single query, then converts in memory.
        /// No-op when the list is empty or no group carries a parseable date.
        /// </summary>
        public static async Task FillDailyUsdValuesAsync(
            TradeNetDbContext db,
            IReadOnlyList<ReportAggregateResult> groups)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(groups);

            if (groups.Count == 0)
            {
                return;
            }

            var dates = groups
                .Select(group => ParseGroupDate(group.Date))
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .Distinct()
                .ToList();

            if (dates.Count == 0)
            {
                return;
            }

            var minDate = dates.Min();
            var maxDateExclusive = dates.Max().AddDays(1);

            // The item currencies present in the report, plus USD (the conversion target).
            var currencyCodes = groups
                .Select(group => group.Currency)
                .Where(code => !string.IsNullOrEmpty(code))
                .Select(code => code!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!currencyCodes.Contains(Usd, StringComparer.OrdinalIgnoreCase))
            {
                currencyCodes.Add(Usd);
            }

            var rateRows = await (
                from rate in db.ExchangeRates.AsNoTracking()
                join currency in db.Currencies.AsNoTracking() on rate.CurrencyId equals currency.Id
                where rate.Date >= minDate
                    && rate.Date < maxDateExclusive
                    && currencyCodes.Contains(currency.Code)
                orderby rate.Id
                select new { rate.Date, currency.Code, rate.Rate })
                .ToListAsync();

            // (day, currencyCode) -> rate. When a day has duplicate rows for a currency
            // (common in the data), the lowest Id wins — matching the legacy FirstOrDefault.
            var rateByDateCurrency = new Dictionary<(DateTime Date, string Code), decimal>();
            foreach (var rateRow in rateRows)
            {
                var key = (rateRow.Date.Date, rateRow.Code);
                if (!rateByDateCurrency.ContainsKey(key))
                {
                    rateByDateCurrency[key] = rateRow.Rate;
                }
            }

            foreach (var group in groups)
            {
                group.TotalUSDValue = ConvertToUsd(
                    group.TotalValue,
                    group.Currency,
                    ParseGroupDate(group.Date),
                    rateByDateCurrency);
            }
        }

        private static decimal? ConvertToUsd(
            decimal? amount,
            string? currency,
            DateTime? date,
            IReadOnlyDictionary<(DateTime Date, string Code), decimal> rates)
        {
            if (amount is null || string.IsNullOrEmpty(currency) || date is null)
            {
                return null;
            }

            if (string.Equals(currency, Usd, StringComparison.OrdinalIgnoreCase))
            {
                return decimal.Round(amount.Value, 4);
            }

            // Missing rate falls back to 1, mirroring the legacy "== null ? 1" default.
            var currencyRate = rates.TryGetValue((date.Value.Date, currency), out var ori) ? ori : 1m;
            var usdRate = rates.TryGetValue((date.Value.Date, Usd), out var usd) ? usd : 1m;

            if (usdRate == 0m)
            {
                return null;
            }

            var factor = currencyRate / usdRate;
            if (PerHundredCurrencies.Contains(currency))
            {
                factor /= 100m;
            }

            return decimal.Round(amount.Value * factor, 4);
        }

        private static DateTime? ParseGroupDate(string? date)
        {
            return DateTime.TryParseExact(
                date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? parsed
                : null;
        }
    }
}

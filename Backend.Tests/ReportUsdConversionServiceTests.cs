using API.Service.Reports;

namespace Backend.Tests;

public sealed class ReportUsdConversionServiceTests
{
    private static readonly DateTime RateDate = new(2026, 4, 1);

    private static readonly IReadOnlyDictionary<(DateTime Date, string Code), decimal> Rates =
        new Dictionary<(DateTime Date, string Code), decimal>
        {
            [(RateDate, "USD")] = 2_000m,
            [(RateDate, "EUR")] = 2_200m,
            [(RateDate, "JPY")] = 1_400m,
            [(RateDate, "KRW")] = 1_600m,
        };

    [Theory]
    [InlineData("USD", 10, 10)]
    [InlineData("EUR", 10, 11)]
    [InlineData("JPY", 100, 0.7)]
    [InlineData("KRW", 100, 0.8)]
    public void Conversion_matches_the_legacy_daily_rate_rules(
        string currency,
        double amount,
        double expected)
    {
        var converted = ReportUsdConversionService.ConvertToUsd(
            (decimal)amount,
            currency,
            RateDate,
            Rates);

        Assert.Equal((decimal)expected, converted);
    }
}

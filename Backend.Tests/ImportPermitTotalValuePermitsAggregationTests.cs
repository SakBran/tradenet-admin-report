using API.StoredProcedureToLinq;

namespace Backend.Tests;

public sealed class ImportPermitTotalValuePermitsAggregationTests
{
    [Fact]
    public void Aggregation_sums_each_currency_and_counts_each_permit_once_per_card_type()
    {
        var rows = new[]
        {
            new sp_ImportPermitDetailReport_Fast.TotalValuePermitSourceRow
            {
                PermitNo = "IP-001",
                PaThaKaType = "Company",
                Currency = "USD",
                Amount = 100m,
            },
            new sp_ImportPermitDetailReport_Fast.TotalValuePermitSourceRow
            {
                PermitNo = "IP-001",
                PaThaKaType = "Company",
                Currency = "USD",
                Amount = 50m,
            },
            new sp_ImportPermitDetailReport_Fast.TotalValuePermitSourceRow
            {
                PermitNo = "IP-002",
                PaThaKaType = "Government",
                Currency = "JPY",
                Amount = 20_000m,
            },
        };

        var result = sp_ImportPermitDetailReport_Fast.AggregateTotalValuePermits(
            rows,
            totalUsdValue: 290.5m);

        Assert.Collection(
            result.TotalValueByCurrency,
            row =>
            {
                Assert.Equal("JPY", row.Currency);
                Assert.Equal(20_000m, row.TotalValue);
            },
            row =>
            {
                Assert.Equal("USD", row.Currency);
                Assert.Equal(150m, row.TotalValue);
            });

        Assert.Collection(
            result.TotalPermitsByPaThaKaType,
            row =>
            {
                Assert.Equal("Company", row.PaThaKaType);
                Assert.Equal(1, row.NoOfPermits);
            },
            row =>
            {
                Assert.Equal("Government", row.PaThaKaType);
                Assert.Equal(1, row.NoOfPermits);
            });

        Assert.Equal(290.5m, result.TotalUsdValue);
    }
}

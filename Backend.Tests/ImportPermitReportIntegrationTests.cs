using API.Model;
using API.Service.Reports;

namespace Backend.Tests;

/// <summary>
/// Level 2 (integration): exercises the aggregation -> paging -> column-totals -> ApiResult
/// envelope pipeline the Import Permit "By X" controllers run after fetching detail rows
/// (ReportAggregationService.CreatePagedResult). This is the controller's post-fetch logic
/// assembled together; it is driven with hand-built source rows so it runs deterministically
/// without a database. (The DB-query half — sp_*_Fast.Rows — cannot run on the EF InMemory
/// provider because the NRC projection calls int.ToString() on a left-joined int?, which only
/// SQL Server can translate; that half is covered by ImportPermitLiveDbIntegrationTests and
/// the live-DB validation, plus the pure logic in ReportAggregationServiceTests.)
/// </summary>
public sealed class ImportPermitReportIntegrationTests
{
    private static AggregateSourceRow Row(
        string licenceNo, decimal amount, string currency,
        string? sectionName = null, int? sectionId = null,
        string? country = null, int? countryId = null,
        string? companyName = null, string? companyRegistrationNo = null)
        => new()
        {
            LicenceNo = licenceNo,
            Amount = amount,
            Currency = currency,
            SectionName = sectionName,
            SectionId = sectionId,
            Country = country,
            CountryId = countryId,
            CompanyName = companyName,
            CompanyRegistrationNo = companyRegistrationNo,
        };

    [Fact]
    public void BySection_pipeline_pages_carries_SectionId_and_emits_column_totals()
    {
        var rows = new[]
        {
            Row("L1", 100m, "USD", sectionName: "4", sectionId: 10),
            Row("L2", 50m, "USD", sectionName: "4", sectionId: 10),
            Row("L3", 70m, "USD", sectionName: "5", sectionId: 11),
        };

        var result = ReportAggregationService.CreatePagedResult(
            rows, ReportAggregateDimension.Section, includeSakhan: false,
            new ReportQueryRequest { PageIndex = 0, PageSize = 100, IncludeTotalCount = true },
            includeColumnTotals: true);

        Assert.Equal(2, result.TotalCount);                 // two section groups
        var s4 = Assert.Single(result.Data, r => r.SectionName == "4");
        Assert.Equal(10, s4.SectionId);                     // drill-down id threaded into the envelope
        Assert.Equal(2, s4.NoOfLicences);
        Assert.Equal(150m, s4.TotalValue);

        Assert.NotNull(result.ColumnTotals);
        Assert.Equal(3m, result.ColumnTotals!["noOfLicences"]);   // grand total licences
        Assert.Equal(220m, result.ColumnTotals!["totalValue"]);   // grand total value
    }

    [Fact]
    public void ByCountry_pipeline_carries_CountryId()
    {
        var rows = new[]
        {
            Row("L1", 100m, "USD", country: "JAPAN", countryId: 112),
            Row("L2", 200m, "USD", country: "CHINA", countryId: 45),
        };

        var result = ReportAggregationService.CreatePagedResult(
            rows, ReportAggregateDimension.Country, includeSakhan: false,
            new ReportQueryRequest { PageSize = 100, IncludeTotalCount = true },
            includeColumnTotals: true);

        var japan = Assert.Single(result.Data, r => r.Country == "JAPAN");
        Assert.Equal(112, japan.CountryId);
        Assert.Null(japan.SectionId);
    }

    [Fact]
    public void Pipeline_respects_paging()
    {
        var rows = Enumerable.Range(1, 5)
            .Select(i => Row($"L{i}", 10m, "USD", sectionName: $"S{i}", sectionId: i))
            .ToArray();

        var page = ReportAggregationService.CreatePagedResult(
            rows, ReportAggregateDimension.Section, includeSakhan: false,
            new ReportQueryRequest { PageIndex = 0, PageSize = 2, IncludeTotalCount = true });

        Assert.Equal(5, page.TotalCount);   // total groups
        Assert.Equal(2, page.Data.Count);   // page size
    }

    [Fact]
    public void Pipeline_omits_column_totals_when_not_requested()
    {
        var rows = new[] { Row("L1", 100m, "USD", sectionName: "4", sectionId: 10) };

        var result = ReportAggregationService.CreatePagedResult(
            rows, ReportAggregateDimension.Section, includeSakhan: false,
            new ReportQueryRequest { PageSize = 100 },
            includeColumnTotals: false);

        Assert.Null(result.ColumnTotals);
    }
}

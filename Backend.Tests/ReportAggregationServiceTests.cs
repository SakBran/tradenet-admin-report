using API.Service.Reports;

namespace Backend.Tests;

/// <summary>
/// Unit tests for <see cref="ReportAggregationService.Aggregate"/> — the in-memory grouping
/// behind the Import/Export Permit "By X" summary reports. Guards (a) the drill-down id
/// threading (SectionId/CountryId/CompanyRegistrationNo carried alongside the display name so
/// the frontend can drill into an id-filtered Detail report) and (b) the distinct-licence
/// count + per-currency grouping the "Total" footers depend on.
/// </summary>
public sealed class ReportAggregationServiceTests
{
    private static AggregateSourceRow Row(
        string licenceNo,
        decimal amount,
        string currency,
        string? sectionName = null,
        int? sectionId = null,
        string? country = null,
        int? countryId = null,
        string? companyName = null,
        string? companyRegistrationNo = null,
        string? hsCode = null,
        string? hsDescription = null)
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
            HSCode = hsCode,
            HSDescription = hsDescription,
        };

    [Fact]
    public void Section_dimension_carries_SectionId_counts_distinct_and_sums_value()
    {
        var rows = new[]
        {
            Row("L1", 100m, "USD", sectionName: "A", sectionId: 10),
            Row("L1", 50m, "USD", sectionName: "A", sectionId: 10),  // same licence -> counted once, amount still sums
            Row("L2", 30m, "USD", sectionName: "A", sectionId: 10),
            Row("L3", 70m, "USD", sectionName: "B", sectionId: 20),
        };

        var result = ReportAggregationService.Aggregate(rows, ReportAggregateDimension.Section, includeSakhan: false);

        var a = Assert.Single(result, r => r.SectionName == "A");
        Assert.Equal(10, a.SectionId);     // drill-down fix: id is populated, not null
        Assert.Equal(2, a.NoOfLicences);   // L1, L2 distinct
        Assert.Equal(180m, a.TotalValue);  // 100 + 50 + 30
        Assert.Null(a.CountryId);          // non-matching dimension ids stay null

        var b = Assert.Single(result, r => r.SectionName == "B");
        Assert.Equal(20, b.SectionId);
        Assert.Equal(1, b.NoOfLicences);
        Assert.Equal(70m, b.TotalValue);
    }

    [Fact]
    public void Country_dimension_carries_CountryId()
    {
        var rows = new[]
        {
            Row("L1", 100m, "USD", country: "JAPAN", countryId: 112),
            Row("L2", 200m, "USD", country: "JAPAN", countryId: 112),
            Row("L3", 300m, "USD", country: "CHINA", countryId: 45),
        };

        var result = ReportAggregationService.Aggregate(rows, ReportAggregateDimension.Country, includeSakhan: false);

        var japan = Assert.Single(result, r => r.Country == "JAPAN");
        Assert.Equal(112, japan.CountryId);  // drill-down fix
        Assert.Equal(2, japan.NoOfLicences);
        Assert.Null(japan.SectionId);

        var china = Assert.Single(result, r => r.Country == "CHINA");
        Assert.Equal(45, china.CountryId);
    }

    [Fact]
    public void Company_dimension_carries_CompanyRegistrationNo()
    {
        var rows = new[]
        {
            Row("L1", 10m, "USD", companyName: "ACME", companyRegistrationNo: "REG-1"),
            Row("L2", 20m, "USD", companyName: "ACME", companyRegistrationNo: "REG-1"),
        };

        var result = ReportAggregationService.Aggregate(rows, ReportAggregateDimension.Company, includeSakhan: false);

        var acme = Assert.Single(result);
        Assert.Equal("ACME", acme.CompanyName);
        Assert.Equal("REG-1", acme.CompanyRegistrationNo);  // Company drill-down source key
        Assert.Equal(2, acme.NoOfLicences);
    }

    [Fact]
    public void Different_currencies_split_into_separate_rows_keeping_the_dimension_id()
    {
        var rows = new[]
        {
            Row("L1", 100m, "USD", sectionName: "A", sectionId: 10),
            Row("L2", 200m, "CNY", sectionName: "A", sectionId: 10),
        };

        var result = ReportAggregationService.Aggregate(rows, ReportAggregateDimension.Section, includeSakhan: false);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(10, r.SectionId));
        Assert.Contains(result, r => r.Currency == "USD" && r.TotalValue == 100m);
        Assert.Contains(result, r => r.Currency == "CNY" && r.TotalValue == 200m);
    }

    [Fact]
    public void HSCode_dimension_counts_a_licence_once_per_group_not_globally()
    {
        // A licence can appear under several HS codes. Each HS-code group counts it once,
        // so summing the per-row NoOfLicences would double-count it -- which is exactly why
        // the HSCode "Total No of License" footer uses a DISTINCT count, not a sum.
        var rows = new[]
        {
            Row("L1", 10m, "USD", hsCode: "1001", hsDescription: "d1", companyName: "C", companyRegistrationNo: "R"),
            Row("L1", 20m, "USD", hsCode: "2002", hsDescription: "d2", companyName: "C", companyRegistrationNo: "R"),
        };

        var result = ReportAggregationService.Aggregate(rows, ReportAggregateDimension.HSCode, includeSakhan: false);

        Assert.Equal(2, result.Count);                       // two HS-code groups
        Assert.All(result, r => Assert.Equal(1, r.NoOfLicences));
        Assert.Equal(2, result.Sum(r => r.NoOfLicences));    // naive sum = 2, but only 1 distinct licence
    }

    [Fact]
    public void Distinct_licence_count_is_case_insensitive_and_ignores_blanks()
    {
        var rows = new[]
        {
            Row("ovsip-1", 10m, "USD", sectionName: "A", sectionId: 10),
            Row("OVSIP-1", 10m, "USD", sectionName: "A", sectionId: 10),  // same licence, different case
            Row("", 10m, "USD", sectionName: "A", sectionId: 10),          // blank -> ignored
        };

        var result = ReportAggregationService.Aggregate(rows, ReportAggregateDimension.Section, includeSakhan: false);

        var a = Assert.Single(result);
        Assert.Equal(1, a.NoOfLicences);
    }

    [Fact]
    public void Empty_input_yields_no_rows()
    {
        var result = ReportAggregationService.Aggregate(
            Array.Empty<AggregateSourceRow>(), ReportAggregateDimension.Section, includeSakhan: false);

        Assert.Empty(result);
    }
}

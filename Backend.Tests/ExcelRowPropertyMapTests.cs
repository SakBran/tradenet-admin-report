using API.Service.ExcelExport;

namespace Backend.Tests;

/// <summary>
/// The grid reads cells as <c>row[dataIndex]</c> where dataIndex is the camelCase JSON
/// key. If this mapping is off by one capital letter the column exports blank, which is
/// exactly how the NRC columns were silently empty before.
/// </summary>
public sealed class ExcelRowPropertyMapTests
{
    private sealed class Row
    {
        public string Id { get; init; } = "1";
        public string? NRCNo { get; init; }
        public decimal? TotalUSDValue { get; init; }
        public string? HSCode { get; init; }
        public string? CompanyName { get; init; }
    }

    [Theory]
    [InlineData("id")]
    [InlineData("nrcNo")]
    [InlineData("totalUSDValue")]
    [InlineData("hsCode")]
    [InlineData("companyName")]
    public void Every_camelCase_json_key_resolves(string dataIndex)
        => Assert.NotNull(ExcelRowPropertyMap.For(typeof(Row)).Find(dataIndex));

    [Fact]
    public void The_acronym_properties_map_the_way_System_Text_Json_names_them()
    {
        var map = ExcelRowPropertyMap.For(typeof(Row));
        var row = new Row { NRCNo = "12/AAA(N)123456", TotalUSDValue = 5m, HSCode = "0101" };

        Assert.Equal("12/AAA(N)123456", map.GetValue(row, "nrcNo"));
        Assert.Equal(5m, map.GetValue(row, "totalUSDValue"));
        Assert.Equal("0101", map.GetValue(row, "hsCode"));

        // "nRCNo" was the camelCase spelling the first attempt guessed — it must not be
        // the only spelling that works, but it must not resolve to nothing either.
        Assert.NotNull(map.Find("NRCNo"));
    }

    [Fact]
    public void An_unknown_dataIndex_resolves_to_nothing_rather_than_throwing()
    {
        var map = ExcelRowPropertyMap.For(typeof(Row));

        Assert.Null(map.Find("remark"));
        Assert.Null(map.GetValue(new Row(), "remark"));
        Assert.False(map.TryGet("remark", out var accessor));
        Assert.Null(accessor(new Row()));
    }

    [Fact]
    public void The_map_is_cached_per_row_type()
        => Assert.Same(ExcelRowPropertyMap.For(typeof(Row)), ExcelRowPropertyMap.For(typeof(Row)));

    [Fact]
    public void Property_types_are_exposed_for_section_row_resolution()
        => Assert.Equal(typeof(decimal?), ExcelRowPropertyMap.For(typeof(Row)).PropertyType("totalUSDValue"));
}

using System.Diagnostics;
using API.DBContext;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Backend.Tests;

public sealed class TempAggregateSqlValidation
{
    private const string Conn =
        "Server=203.81.66.111,14330;Initial Catalog=TradeNetDB;User ID=sa;Password=Pr0fessi0nal@IM2022;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;";

    private readonly ITestOutputHelper _output;

    public TempAggregateSqlValidation(ITestOutputHelper output) => _output = output;

    private static TradeNetDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer(Conn, o => o.CommandTimeout(60))
            .Options;
        return new TradeNetDbContext(options);
    }

    [Theory]
    [InlineData("Oversea", ReportAggregateDimension.Section)]
    [InlineData("Oversea", ReportAggregateDimension.Method)]
    [InlineData("Oversea", ReportAggregateDimension.Country)]
    [InlineData("Oversea", ReportAggregateDimension.Company)]
    [InlineData("Oversea", ReportAggregateDimension.HSCode)]
    [InlineData("Oversea", ReportAggregateDimension.Daily)]
    [InlineData("Oversea", ReportAggregateDimension.TotalValue)]
    [InlineData("Border", ReportAggregateDimension.Section)]
    public async Task Runs_and_is_fast(string type, ReportAggregateDimension dimension)
    {
        await using var db = NewDb();

        var request = new sp_ImportLicenceDetailReportRequest
        {
            Type = type,
            FromDate = new DateTime(2018, 1, 1),
            ToDate = new DateTime(2024, 12, 31, 23, 59, 59),
            CompanyRegistrationNo = string.Empty,
        };
        var paging = new ReportQueryRequest { PageIndex = 0, PageSize = 10, IncludeTotalCount = true };

        var sw = Stopwatch.StartNew();
        var result = await sp_ImportLicenceDetailReport_Fast.CreateAggregateResultAsync(
            db, request, paging, dimension, includeSakhan: false);
        sw.Stop();

        _output.WriteLine($"{type}/{dimension}: {sw.ElapsedMilliseconds} ms, totalGroups={result.TotalCount}, pageRows={result.Data.Count}");
        Assert.NotNull(result.Data);
    }
}

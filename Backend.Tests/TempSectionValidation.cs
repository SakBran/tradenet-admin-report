using System.Diagnostics;
using API.DBContext;
using API.Model;
using API.StoredProcedureToLinq;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Backend.Tests;

public sealed class TempSectionValidation
{
    private const string Conn =
        "Server=203.81.66.111,14330;Initial Catalog=TradeNetDB;User ID=sa;Password=Pr0fessi0nal@IM2022;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;";

    private readonly ITestOutputHelper _output;
    public TempSectionValidation(ITestOutputHelper output) => _output = output;

    private static TradeNetDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer(Conn, o => o.CommandTimeout(120))
            .Options;
        return new TradeNetDbContext(options);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Section_runs_and_is_fast(bool includeTotalCount)
    {
        await using var db = NewDb();

        var request = new sp_ImportLicenceDetailReportRequest
        {
            Type = "Oversea",
            FromDate = new DateTime(2015, 1, 1),
            ToDate = new DateTime(2024, 12, 31, 23, 59, 59),
            CompanyRegistrationNo = string.Empty,
        };
        var paging = new ReportQueryRequest { PageIndex = 0, PageSize = 10, IncludeTotalCount = includeTotalCount };

        var sw = Stopwatch.StartNew();
        var result = await sp_ImportLicenceDetailReport_Fast.CreateSectionPagedResultAsync(db, request, paging);
        sw.Stop();

        _output.WriteLine($"includeTotalCount={includeTotalCount}: {sw.ElapsedMilliseconds} ms, totalCount={result.TotalCount}, pageRows={result.Data.Count}");
        foreach (var r in result.Data)
        {
            _output.WriteLine($"  {r.SectionName} | {r.NoOfLicences} | {r.TotalValue} | {r.Currency}");
        }
        Assert.NotNull(result.Data);
    }
}

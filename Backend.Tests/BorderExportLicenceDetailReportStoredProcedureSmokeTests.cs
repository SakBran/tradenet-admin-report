using System.Text.Json;
using API.DBContext;
using API.Model;
using API.StoredProcedureToLinq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Tests;

public sealed class BorderExportLicenceDetailReportStoredProcedureSmokeTests
{
    [Fact]
    public async Task Border_export_licence_detail_code_path_returns_rows_from_dedicated_stored_procedure()
    {
        var connectionString = LoadTradeNetDbConnectionString();

        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new TradeNetDbContext(options);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var request = new sp_ExportLicenceDetailReportRequest
        {
            Type = "Border",
            FromDate = new DateTime(2026, 1, 2),
            ToDate = new DateTime(2026, 1, 2),
            PaThaKaTypeId = 0,
            ExportImportSectionId = 0,
            ExportImportMethodId = 0,
            ExportImportIncotermId = 0,
            BuyerCountryId = 0,
            CompanyRegistrationNo = string.Empty,
            SakhanId = 0,
        };

        var pagingRequest = new ReportQueryRequest
        {
            PageIndex = 0,
            PageSize = 20,
            IncludeTotalCount = false,
        };

        var result = await sp_ExportLicenceDetailReport_Fast.CreatePagedResultAsync(
            db,
            cache,
            request,
            pagingRequest);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Data);
        Assert.Equal(0, result.PageIndex);
        Assert.Equal(20, result.PageSize);
    }

    private static string LoadTradeNetDbConnectionString()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var appsettingsPath = Path.Combine(repoRoot, "Backend", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appsettingsPath));

        return document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("TradeNetDBTest")
            .GetString()
            ?? throw new InvalidOperationException("TradeNetDBTest connection string is missing.");
    }
}

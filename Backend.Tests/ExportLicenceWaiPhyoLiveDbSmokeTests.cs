using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using Backend.Controllers.Report;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Backend.Tests;

public sealed class ExportLicenceWaiPhyoLiveDbSmokeTests(ITestOutputHelper output)
{
    private const string SkipReason =
        "Set TRADENET_REPORT_TEST_CONNECTION_STRING to a reachable TradeNetDB to run this live integration test.";

    private TradeNetDbContext? TryConnect()
    {
        var cs = Environment.GetEnvironmentVariable("TRADENET_REPORT_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(cs))
        {
            output.WriteLine("SKIPPED: " + SkipReason);
            return null;
        }

        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer(cs, sql => sql.CommandTimeout(180))
            .Options;
        var db = new TradeNetDbContext(options);

        try
        {
            if (!db.Database.CanConnect())
            {
                output.WriteLine("SKIPPED: connection string set but database unreachable.");
                db.Dispose();
                return null;
            }
        }
        catch (Exception ex)
        {
            output.WriteLine("SKIPPED: " + ex.Message);
            db.Dispose();
            return null;
        }

        return db;
    }

    [Fact]
    public async Task Voucher_report_blank_filters_returns_rows_through_real_controller()
    {
        var db = TryConnect();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var controller = (ExportLicenceVoucherReportController)ReportTestHelper.CreateController(
                typeof(ExportLicenceVoucherReportController), db);

            var sw = Stopwatch.StartNew();
            var result = await controller.Post(new ExportLicenceVoucherReportRequest
            {
                FromDate = new DateTime(2023, 4, 3),
                ToDate = new DateTime(2023, 4, 3, 23, 59, 59),
                ExportImportSectionId = 0,
                ApplyType = string.Empty,
                PaymentType = string.Empty,
                CompanyRegistrationNo = string.Empty,
                PageIndex = 0,
                PageSize = 20,
                IncludeTotalCount = true,
            });
            sw.Stop();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var api = Assert.IsType<ApiResult<sp_VoucherReportResult>>(ok.Value);

            output.WriteLine($"ExportLicenceVoucherReportController: rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}, elapsedMs={sw.ElapsedMilliseconds}");
            foreach (var row in api.Data.Take(3))
            {
                output.WriteLine(
                    $"sample licence={row.LicenceNo}, company={row.CompanyName}, commodity={row.CommodityType}, currency={row.Currency}, amount={row.Amount}");
            }

            Assert.NotEmpty(api.Data);
            Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.LicenceNo)));
            Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.Currency)));
            Assert.All(api.Data, row => Assert.True(row.Amount >= 0));
        }
    }

    [Fact]
    public async Task New_report_returns_rows_through_real_controller()
    {
        var db = TryConnect();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var controller = (ExportLicenceNewReportNewReportController)ReportTestHelper.CreateController(
                typeof(ExportLicenceNewReportNewReportController), db);

            var sw = Stopwatch.StartNew();
            var result = await controller.Post(new ExportLicenceNewReportNewReportRequest
            {
                FromDate = new DateTime(2023, 4, 3),
                ToDate = new DateTime(2023, 4, 3, 23, 59, 59),
                ExportImportSectionId = 0,
                CompanyRegistrationNo = string.Empty,
                Auto = string.Empty,
                PageIndex = 0,
                PageSize = 20,
                IncludeTotalCount = true,
            });
            sw.Stop();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var api = Assert.IsType<ApiResult<sp_NewReportResult>>(ok.Value);

            output.WriteLine($"ExportLicenceNewReportNewReportController: rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}, elapsedMs={sw.ElapsedMilliseconds}");
            foreach (var row in api.Data.Take(3))
            {
                output.WriteLine(
                    $"sample licence={row.LicenceNo}, company={row.CompanyName}, commodity={row.CommodityType}, hs={row.HSCode}, quota={row.Quota ?? "<null>"}");
            }

            Assert.NotEmpty(api.Data);
            Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.LicenceNo)));
            Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.CompanyName)));
        }
    }

    [Fact]
    public async Task Actual_amendment_report_returns_rows_through_real_controller()
    {
        var db = TryConnect();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var controller = (ExportLicenceActualAmendmentReportController)ReportTestHelper.CreateController(
                typeof(ExportLicenceActualAmendmentReportController), db);

            var sw = Stopwatch.StartNew();
            var result = await controller.Post(new ExportLicenceActualAmendmentReportRequest
            {
                FromDate = new DateTime(2026, 4, 1),
                ToDate = new DateTime(2026, 4, 1, 23, 59, 59),
                ExportImportSectionId = 0,
                AmendRemarkId = 0,
                CompanyRegistrationNo = string.Empty,
                PageIndex = 0,
                PageSize = 20,
                IncludeTotalCount = true,
            });
            sw.Stop();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var api = Assert.IsType<ApiResult<sp_ActualAmendReportResult>>(ok.Value);

            output.WriteLine($"ExportLicenceActualAmendmentReportController: rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}, elapsedMs={sw.ElapsedMilliseconds}");
            foreach (var row in api.Data.Take(3))
            {
                output.WriteLine(
                    $"sample licence={row.LicenceNo}, old={row.OldLicenceNo}, company={row.CompanyName}, hs={row.HSCode}, amount={row.Amount}, currency={row.Currency}");
            }

            Assert.NotEmpty(api.Data);
            Assert.True(api.IsTotalCountExact);
            Assert.True(api.TotalCount >= api.Data.Count);
            Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.LicenceNo)));
        }
    }
}

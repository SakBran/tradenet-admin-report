using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Backend.Tests;

public sealed class BorderImportLicenceWaiPhyoLiveDbSmokeTests(ITestOutputHelper output)
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
    public async Task Amendment_report_returns_rows_through_real_controller()
    {
        var db = TryConnect();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var controller = (BorderImportLicenceAmendmentReportController)ReportTestHelper.CreateController(
                typeof(BorderImportLicenceAmendmentReportController), db);

            var sw = Stopwatch.StartNew();
            var result = await controller.Post(new BorderImportLicenceAmendmentReportRequest
            {
                FromDate = new DateTime(2026, 5, 22),
                ToDate = new DateTime(2026, 5, 22, 23, 59, 59),
                ExportImportSectionId = 0,
                AmendRemarkId = 0,
                CompanyRegistrationNo = string.Empty,
                SakhanId = 0,
                PageIndex = 0,
                PageSize = 20,
                IncludeTotalCount = true,
            });
            sw.Stop();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var api = Assert.IsType<ApiResult<sp_AmendReportResult>>(ok.Value);

            output.WriteLine($"BorderImportLicenceAmendmentReportController: rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}, elapsedMs={sw.ElapsedMilliseconds}");
            Assert.NotEmpty(api.Data);
            Assert.True(api.IsTotalCountExact);
            Assert.True(api.TotalCount >= api.Data.Count);
        }
    }

    [Fact]
    public async Task Voucher_report_returns_rows_through_real_controller()
    {
        var db = TryConnect();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var controller = (BorderImportLicenceVoucherReportController)ReportTestHelper.CreateController(
                typeof(BorderImportLicenceVoucherReportController), db);

            var sw = Stopwatch.StartNew();
            var result = await controller.Post(new BorderImportLicenceVoucherReportRequest
            {
                FromDate = new DateTime(2026, 5, 1),
                ToDate = new DateTime(2026, 6, 30, 23, 59, 59),
                ExportImportSectionId = 0,
                PaymentType = string.Empty,
                ApplyType = string.Empty,
                CompanyRegistrationNo = string.Empty,
                SakhanId = 0,
                PageIndex = 0,
                PageSize = 20,
                IncludeTotalCount = true,
            });
            sw.Stop();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var api = Assert.IsType<ApiResult<sp_VoucherReportResult>>(ok.Value);

            output.WriteLine($"BorderImportLicenceVoucherReportController: rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}, elapsedMs={sw.ElapsedMilliseconds}");
            Assert.NotEmpty(api.Data);
            Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.LicenceNo)));
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
            var controller = (BorderImportLicenceNewReportNewReportController)ReportTestHelper.CreateController(
                typeof(BorderImportLicenceNewReportNewReportController), db);

            var sw = Stopwatch.StartNew();
            var result = await controller.Post(new BorderImportLicenceNewReportNewReportRequest
            {
                FromDate = new DateTime(2026, 5, 1),
                ToDate = new DateTime(2026, 6, 30, 23, 59, 59),
                ExportImportSectionId = 0,
                CompanyRegistrationNo = string.Empty,
                SakhanId = 0,
                Auto = string.Empty,
                PageIndex = 0,
                PageSize = 20,
                IncludeTotalCount = true,
            });
            sw.Stop();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var api = Assert.IsType<ApiResult<sp_NewReportResult>>(ok.Value);

            output.WriteLine($"BorderImportLicenceNewReportNewReportController: rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}, elapsedMs={sw.ElapsedMilliseconds}");
            Assert.NotEmpty(api.Data);
            Assert.True(api.IsTotalCountExact);
            Assert.True(api.TotalCount >= api.Data.Count);
        }
    }
}

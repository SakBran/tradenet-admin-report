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

public sealed class BorderExportPermitWaiPhyoLiveDbSmokeTests(ITestOutputHelper output)
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
            var controller = (BorderExportPermitAmendmentReportController)ReportTestHelper.CreateController(
                typeof(BorderExportPermitAmendmentReportController), db);

            var sw = Stopwatch.StartNew();
            var result = await controller.Post(new BorderExportPermitAmendmentReportRequest
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

            output.WriteLine($"BorderExportPermitAmendmentReportController: rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}, elapsedMs={sw.ElapsedMilliseconds}");
            Assert.NotEmpty(api.Data);
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
            var controller = (BorderExportPermitVoucherReportController)ReportTestHelper.CreateController(
                typeof(BorderExportPermitVoucherReportController), db);

            var sw = Stopwatch.StartNew();
            var result = await controller.Post(new BorderExportPermitVoucherReportRequest
            {
                FromDate = new DateTime(2023, 1, 1),
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

            output.WriteLine($"BorderExportPermitVoucherReportController: rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}, elapsedMs={sw.ElapsedMilliseconds}");
            Assert.NotEmpty(api.Data);
            Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.LicenceNo)));
        }
    }

    [Fact]
    public async Task Actual_amendment_report_returns_empty_result_without_error_when_db_has_no_rows()
    {
        var db = TryConnect();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var controller = (BorderExportPermitActualAmendmentReportController)ReportTestHelper.CreateController(
                typeof(BorderExportPermitActualAmendmentReportController), db);

            var sw = Stopwatch.StartNew();
            var result = await controller.Post(new BorderExportPermitActualAmendmentReportRequest
            {
                FromDate = new DateTime(2023, 1, 1),
                ToDate = new DateTime(2026, 6, 30, 23, 59, 59),
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
            var api = Assert.IsType<ApiResult<sp_ActualAmendReportResult>>(ok.Value);

            output.WriteLine($"BorderExportPermitActualAmendmentReportController: rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}, elapsedMs={sw.ElapsedMilliseconds}");
            Assert.Empty(api.Data);
            Assert.True(api.IsTotalCountExact);
            Assert.Equal(0, api.TotalCount);
        }
    }
}

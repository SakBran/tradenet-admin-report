using API.Model.ExcelExport;
using API.Model.TradeNet;
using API.Service.Reports;
using Backend.Controllers;

namespace Backend.Tests;

public class ExcelExportAccessTests
{
    [Fact]
    public void Non_admin_can_access_only_owned_jobs_for_assigned_reports()
    {
        var access = ReportAccessPolicy.Create("Report", new[]
        {
            new UserDetail { Type = "Import Licence", SubType = "Border", Section = "1" },
        });
        var job = new ExcelExportJob
        {
            RequestedByUserName = "42", ReportKey = "BorderImportLicencePendingReport",
        };

        Assert.True(ExcelExportController.CanAccessJob(access, "42", job));
        Assert.False(ExcelExportController.CanAccessJob(access, "43", job));
        job.ReportKey = "ImportLicencePendingReport";
        Assert.False(ExcelExportController.CanAccessJob(access, "42", job));
    }

    [Fact]
    public void Admin_can_access_every_job()
    {
        var access = ReportAccessPolicy.Create("Super Administrator", Array.Empty<UserDetail>());
        var job = new ExcelExportJob { RequestedByUserName = "42", ReportKey = "ImportLicencePendingReport" };
        Assert.True(ExcelExportController.CanAccessJob(access, "43", job));
    }

    [Fact]
    public void View_all_reports_user_can_access_any_report_but_only_own_exports()
    {
        var access = ReportAccessPolicy.Create("Report", Array.Empty<UserDetail>(), viewAllReports: true);
        var job = new ExcelExportJob { RequestedByUserName = "1503", ReportKey = "ExportPermitPendingReport" };

        Assert.True(ExcelExportController.CanAccessJob(access, "1503", job));
        Assert.False(ExcelExportController.CanAccessJob(access, "another-user", job));
        job.ReportKey = "ImportLicenceDataImport";
        Assert.False(ExcelExportController.CanAccessJob(access, "1503", job));
    }
}

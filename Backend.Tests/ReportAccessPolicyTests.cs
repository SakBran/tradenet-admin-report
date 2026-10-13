using API.Model.TradeNet;
using API.Service.Reports;

namespace Backend.Tests;

public class ReportAccessPolicyTests
{
    [Fact]
    public void Report_user_sees_only_assigned_type_and_subtype()
    {
        var access = ReportAccessPolicy.Create("Report", new[]
        {
            new UserDetail { Type = "Import Licence", SubType = "Border", Section = "1,2" },
            new UserDetail { Type = "Registration", SubType = "Pa Tha Ka", Section = "" },
        });

        Assert.True(access.CanAccess("BorderImportLicencePendingReport"));
        Assert.True(access.CanAccess("AdvanceSearchBorderImportLicence"));
        Assert.True(access.CanAccess("CompanyProfile"));
        Assert.False(access.CanAccess("ImportLicencePendingReport"));
        Assert.False(access.CanAccess("AdvanceSearchImportLicence"));
        Assert.False(access.CanAccess("ImportPermitDetailReport"));
        Assert.False(access.CanAccess("MPUReport"));
    }

    [Fact]
    public void Check_and_approve_users_follow_the_same_assignments()
    {
        var assignment = new[] { new UserDetail { Type = "Export Permit", SubType = "Oversea", Section = "1" } };

        foreach (var userType in new[] { "Check User", "Approve User" })
        {
            var access = ReportAccessPolicy.Create(userType, assignment);
            Assert.True(access.CanAccess("ExportPermitDetailReport"));
            Assert.False(access.CanAccess("BorderExportPermitDetailReport"));
        }
    }

    [Fact]
    public void Admin_sees_all_and_account_user_sees_payment_only()
    {
        var admin = ReportAccessPolicy.Create("Super Administrator", Array.Empty<UserDetail>());
        var account = ReportAccessPolicy.Create("Account User", Array.Empty<UserDetail>());

        Assert.True(admin.IsAdmin);
        Assert.True(admin.CanAccess("MemberRegistrationReport"));
        Assert.True(admin.CanAccess("DataImport"));
        Assert.True(account.CanAccess("MPUReport"));
        Assert.False(account.CanAccess("ImportLicenceDetailReport"));
    }

    [Fact]
    public void Unassigned_and_unknown_roles_fail_closed()
    {
        Assert.Empty(ReportAccessPolicy.Create("Report", Array.Empty<UserDetail>()).Categories);
        Assert.False(ReportAccessPolicy.Create("Report", Array.Empty<UserDetail>()).CanAccess("ImportLicenceDetailReport"));
        Assert.False(ReportAccessPolicy.Create("Sakhan User", new[]
        {
            new UserDetail { Type = "Import Licence", SubType = "Border", Section = "1" },
        }).CanAccess("BorderImportLicenceDetailReport"));
        Assert.False(ReportAccessPolicy.Create("Report", Array.Empty<UserDetail>()).CanAccess("UnknownNewReport"));
    }

    [Fact]
    public void Every_report_controller_has_an_explicit_category_or_is_data_import()
    {
        var unmapped = ReportTestHelper.ControllerTypes
            .Select(type => type.Name.Replace("Controller", "", StringComparison.Ordinal))
            .Where(name => name != "DataImport" && ReportAccessPolicy.CategoryForController(name) == null)
            .ToArray();

        Assert.Empty(unmapped);
    }
}

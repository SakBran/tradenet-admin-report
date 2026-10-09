using System.Security.Claims;
using API.Model.TradeNet;
using API.Service.Reports;
using Microsoft.Extensions.Configuration;

namespace Backend.Tests;

public class ReportAccessServiceTests
{
    [Theory]
    [InlineData("Report", true, false, "1503", true)]
    [InlineData("Report", true, false, "1504", false)]
    [InlineData("Check User", true, false, "1503", false)]
    [InlineData("Report", false, false, "1503", false)]
    [InlineData("Report", true, true, "1503", false)]
    [InlineData("Report", true, false, "not-an-id", false)]
    public async Task Grant_requires_matching_active_report_user(
        string userType, bool isActive, bool isDeleted, string configuredId, bool expected)
    {
        using var db = ReportTestHelper.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        db.Users.Add(new User { Id = 1503, UserType = userType, IsActive = isActive, IsDeleted = isDeleted,
            FullName = "Test User", UserName = "test-user", Password = "test-only", Position = "Test" });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReportAccess:ViewAllReportUserIds:0"] = configuredId,
        }).Build();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "1503") }, "test"));
        var access = await new ReportAccessService(db, config).GetAsync(principal);

        Assert.Equal(expected, access?.CanViewAllReports ?? false);
        Assert.False(access?.IsAdmin ?? false);
    }

    [Fact]
    public async Task Missing_configuration_keeps_report_user_on_assigned_categories()
    {
        using var db = ReportTestHelper.CreateInMemoryDbContext(Guid.NewGuid().ToString());
        db.Users.Add(new User { Id = 1503, UserType = "Report", IsActive = true,
            FullName = "Test User", UserName = "test-user", Password = "test-only", Position = "Test" });
        db.UserDetails.Add(new UserDetail { Id = 1, UserId = 1503, Type = "Import Licence", SubType = "Border", Section = "1" });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "1503") }, "test"));
        var access = await new ReportAccessService(db, new ConfigurationBuilder().Build()).GetAsync(principal);

        Assert.NotNull(access);
        Assert.False(access.CanViewAllReports);
        Assert.True(access.CanAccess("BorderImportLicencePendingReport"));
        Assert.False(access.CanAccess("ImportLicencePendingReport"));
    }
}

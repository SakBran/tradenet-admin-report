using API.DBContext;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

public sealed class ReportSqlServerFixture : IAsyncLifetime
{
    private readonly string _databaseName = "TradeNet_Report_Endpoint_Tests_" + Guid.NewGuid().ToString("N");

    internal TradeNetDbContext CreateDbContext() =>
        ReportTestHelper.CreateSqlServerDbContext(_databaseName);

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }
}

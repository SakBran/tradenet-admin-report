namespace Backend.Tests;

public sealed class BorderExportPermitDailyDetailContractTests
{
    [Fact]
    public void Detail_post_uses_stored_procedure_pagination_path()
    {
        var controller = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "Controllers",
            "Report",
            "BorderExportPermitDetailReportController.cs"));

        var postMethod = controller[
            controller.IndexOf("public async Task<ActionResult<ApiResult<sp_ExportPermitDetailReportResult>>> Post", StringComparison.Ordinal)..];
        postMethod = postMethod[..postMethod.IndexOf("[HttpPost(\"Excel\")]", StringComparison.Ordinal)];

        Assert.Contains("sp_ExportPermitDetailReport.CreatePagedResultAsync", postMethod);
        Assert.DoesNotContain("sp_ExportPermitDetailReport_Fast.CreatePagedResultAsync", postMethod);
    }

    [Fact]
    public void Detail_stored_procedure_wrapper_forwards_paging_sort_and_lazy_total_count()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "StoredProcedureToLinq",
            "sp_ExportPermitDetailReport.cs"));

        Assert.Contains("EXEC dbo.sp_ExportPermitDetailReport_Fast_pagination", source);
        Assert.Contains("pagingRequest.IncludeTotalCount", source);
        Assert.Contains("pagingRequest.SortColumn", source);
        Assert.Contains("pagingRequest.SortOrder", source);
        Assert.Contains("CreateFastPageFromRows", source);
    }

    [Fact]
    public void Detail_stored_procedure_script_keeps_dynamic_sql_as_nvarchar_max()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StoredProcedureMigrations",
            "sp_ExportPermitDetailReport_Fast_pagination.sql"));

        Assert.Contains("CAST(N'DECLARE @__total int = 0; ' AS nvarchar(max))", source);
    }

    [Fact]
    public void Daily_report_uses_sql_aggregate_path_with_column_totals()
    {
        var controller = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "Controllers",
            "Report",
            "BorderExportPermitDailyReportNewPermitReportController.cs"));

        Assert.Contains("sp_ExportPermitDetailReport_Fast.CreateAggregateResultAsync", controller);
        Assert.Contains("ReportAggregateDimension.Daily", controller);
        Assert.Contains("includeColumnTotals: true", controller);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null
                && !Directory.Exists(Path.Combine(directory.FullName, "Frontend")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}

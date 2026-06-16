using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;

namespace Backend.Tests;

public sealed class ExportLicenceDetailReportContractTests
{
    [Fact]
    public void Ui_columns_are_backed_by_export_licence_detail_api_result_fields()
    {
        var config = ExtractReportConfig("ExportLicenceDetailReport");
        var columns = ExtractColumns(config);
        var apiFields = typeof(sp_ExportLicenceDetailReportResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => ToCamelCase(property.Name))
            .ToHashSet(StringComparer.Ordinal);

        var unsupported = columns
            .Where(column => !apiFields.Contains(column.DataIndex)
                && !column.FallbackDataIndexes.Any(apiFields.Contains))
            .Select(column => column.DataIndex)
            .ToArray();

        Assert.Empty(unsupported);

        Assert.Equal(
            [
                "Section",
                "Application Date",
                "Application No",
                "Licence No",
                "Licence Date",
                "Company Registration No",
                "Company Name",
                "Company Address",
                "Buyer Name",
                "Buyer Address",
                "Buyer Country",
                "Place/Port of Export",
                "Place/Port of Discharge",
                "Last Date",
                "Method",
                "Consigned Country",
                "Country of Orign",
                "Country of Destination",
                "hsCode",
                "Decription",
                "Unit",
                "Price",
                "Qty",
                "Value",
                "Currency",
                "Commodity Type",
                "Conditions",
            ],
            columns.Select(column => column.Title).ToArray());
    }

    [Fact]
    public void Ui_filters_are_accepted_by_export_licence_detail_request()
    {
        var config = ExtractReportConfig("ExportLicenceDetailReport");
        var filterNames = Regex.Matches(config, @"name:\s*'(?<name>[^']+)'")
            .Select(match => match.Groups["name"].Value)
            .Where(name => name != "dateRange")
            .ToArray();

        var requestFields = typeof(Backend.Controllers.Report.ExportLicenceDetailReportRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("FromDate", requestFields);
        Assert.Contains("ToDate", requestFields);
        Assert.DoesNotContain(filterNames, name => !requestFields.Contains(name));
        Assert.Equal(
            [
                "PaThaKaTypeId",
                "ExportImportSectionId",
                "ExportImportMethodId",
                "ExportImportIncotermId",
                "BuyerCountryId",
                "CompanyRegistrationNo",
                "Auto",
            ],
            filterNames);
        Assert.Contains("lookupName: 'exportLicenceSections'", config);
        Assert.Contains("lookupName: 'exportLicenceMethods'", config);
        Assert.Contains("lookupName: 'exportLicenceIncoterms'", config);
        Assert.Contains("defaultDateRangeMonths: 3", config);
    }

    [Fact]
    public void Export_licence_detail_scoped_lookup_routes_are_registered()
    {
        var controllerSource = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "Controllers",
            "ReportLookupsController.cs"));

        Assert.Contains("\"exportlicencesections\" => GetExportLicenceSections", controllerSource);
        Assert.Contains("\"exportlicencemethods\" => GetExportLicenceMethods", controllerSource);
        Assert.Contains("\"exportlicenceincoterms\" => GetExportLicenceIncoterms", controllerSource);
        Assert.Contains("item.Type == ExportLicenceFormType", controllerSource);
        Assert.Contains("item.Type == ExportTradeType", controllerSource);
        Assert.Contains("item.IsOversea", controllerSource);
    }

    [Fact]
    public void Pagination_stored_procedure_returns_columns_required_by_api_row()
    {
        var sql = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StoredProcedureMigrations",
            "sp_ExportLicenceDetailReportV2_pagination.sql"));

        var requiredColumns = typeof(sp_ExportLicenceDetailReportRow)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        foreach (var column in requiredColumns)
        {
            Assert.Matches($@"\b{Regex.Escape(column)}\b", sql);
        }

        Assert.Contains("sp_ExportLicenceDetailReportV2_Pagination", sql);
        Assert.DoesNotContain("@Type", sql);
        Assert.DoesNotContain("BorderExportLicence", sql);
        Assert.Contains("licence.ApplicationNo", sql);
        Assert.Contains("licence.ApplicationDate", sql);
        Assert.Contains("licence.CommodityType", sql);
        Assert.Contains("@__total AS TotalCount", sql);
    }

    [Fact]
    public void Runtime_uses_page_first_inline_sql_for_ui_detail_load()
    {
        var controllerSource = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "Controllers",
            "Report",
            "ExportLicenceDetailReportController.cs"));
        var fastSource = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "StoredProcedureToLinq",
            "sp_ExportLicenceDetailReport_Fast.cs"));
        var v2Source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "StoredProcedureToLinq",
            "sp_ExportLicenceDetailReportV2.cs"));

        Assert.Contains("sp_ExportLicenceDetailReportV2.CreatePagedResultAsync", controllerSource);
        Assert.Contains("result.CurrencyTotals = await sp_ExportLicenceDetailReportV2.CreateCurrencyTotalsAsync", controllerSource);
        Assert.Contains("sp_ExportLicenceDetailReport_Fast.StreamResolvedChunksAsync", controllerSource);
        Assert.Contains("ExecuteSeekedAsync", v2Source);
        Assert.Contains("IX_ExportLicence_Report_NewDetail_Page", v2Source);
        Assert.Contains("IX_ExportLicenceItem_Report_Licence_Page", v2Source);
        Assert.Contains("FetchDetailRowAsync", v2Source);
        Assert.Contains("FetchItemDetailAsync", v2Source);
        Assert.Contains("IsItemPageIndexCoveredAsync", v2Source);
        Assert.Contains("itemDetailsCovered ? coveredItemKeySql : safeItemKeySql", v2Source);
        Assert.Contains("HSCode = hsCode.Code", v2Source);
        Assert.Contains("Price = item.Price", v2Source);
        Assert.Contains("Quantity = item.Quantity", v2Source);
        Assert.Contains("Amount = item.Amount", v2Source);
        Assert.Contains("FetchDelimitedLookupNamesAsync", v2Source);
        Assert.Contains("@Auto", v2Source);
        Assert.Contains("@Auto = N'auto' AND licence.[auto] = N'auto'", v2Source);
        Assert.Contains("@Auto = N'none-auto' AND (licence.[auto] IS NULL OR licence.[auto] <> N'auto')", v2Source);
        Assert.Contains("CAST(COUNT_BIG(*) OVER() AS int) AS TotalCount", v2Source);
        Assert.Contains("OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", v2Source);
        Assert.Contains("CreateCurrencyTotalsAsync", v2Source);
        Assert.Contains("GROUP BY currency.Code", v2Source);
        Assert.Contains("DelimitedLookupTable.PortOfDischarge", v2Source);
        Assert.Contains("DelimitedLookupTable.Countries", v2Source);
        Assert.Contains("ORDER BY item.HSCode, item.ItemNo, item.Id", v2Source);
        Assert.DoesNotContain("sp_ExportLicenceDetailReport_Pagination", v2Source);
        Assert.DoesNotContain("CreateUniqueIdentifierParameter", v2Source);
        Assert.DoesNotContain("SqlQueryRaw<sp_ExportLicenceDetailReportRow>", v2Source);
        Assert.DoesNotContain("[EnumeratorCancellation]", v2Source);
        Assert.Contains("var rows = await Rows(db, request).ToListAsync();", fastSource);
        Assert.DoesNotContain("var pageRows = await ExecuteAsync", fastSource);
    }

    [Fact]
    public void Export_licence_summary_reports_use_export_summary_procedure_without_changing_border_reports()
    {
        var v2Source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "StoredProcedureToLinq",
            "sp_ExportLicenceDetailReportV2.cs"));
        var summaryCallerSource = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "StoredProcedureToLinq",
            "sp_ExportLicenceSummaryReport.cs"));
        var summarySql = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StoredProcedureMigrations",
            "sp_ExportLicenceSummaryReport.sql"));

        Assert.Contains("CreateSummaryResultAsync", v2Source);
        Assert.Contains("GetSummaryRowsAsync", v2Source);
        Assert.Contains("sp_ExportLicenceSummaryReport.ExecuteAsync(db, request, dimensionName)", v2Source);
        Assert.Contains("ReportUsdConversionService.FillDailyUsdValuesAsync(db, groups)", v2Source);
        Assert.Contains("EXEC dbo.sp_ExportLicenceSummaryReport", summaryCallerSource);
        Assert.Contains("FROM dbo.ExportLicence AS el", summarySql);
        Assert.Contains("FROM dbo.ExportLicenceItem AS item", summarySql);
        Assert.Contains("Previous Daily implementation kept for rollback/reference", summarySql);
        Assert.Contains("INNER JOIN dbo.ExportLicenceItem AS item ON item.ExportLicenceId = el.Id", summarySql);
        Assert.Contains("COALESCE(SUM(item.Amount), 0) AS TotalValue", summarySql);
        Assert.Contains("@Auto nvarchar(20) = N''", summarySql);
        Assert.Contains("@Auto = N'auto' AND el.[auto] = N'auto'", summarySql);
        Assert.Contains("@Auto = N'none-auto' AND (el.[auto] IS NULL OR el.[auto] <> N'auto')", summarySql);
        Assert.DoesNotContain("BorderExportLicence", summarySql);

        foreach (var controllerName in new[]
        {
            "ExportLicenceByMethodReportController.cs",
            "ExportLicenceBySectionReportController.cs",
            "ExportLicenceBySellerCountryReportController.cs",
            "ExportLicenceCompanyListReportController.cs",
            "ExportLicenceDailyReportNewLicenceReportController.cs",
        })
        {
            var controllerSource = File.ReadAllText(Path.Combine(
                RepositoryRoot,
                "Backend",
                "Controllers",
                "Report",
                controllerName));

            Assert.Contains("sp_ExportLicenceDetailReportV2.CreateSummaryResultAsync", controllerSource);
            Assert.Contains("sp_ExportLicenceDetailReportV2.GetSummaryRowsAsync", controllerSource);
            Assert.DoesNotContain("sp_ExportLicenceDetailReport_Fast.CreateAggregateResultAsync", controllerSource);
            Assert.DoesNotContain("sp_ExportLicenceDetailReport_Fast.GetAggregateRowsAsync", controllerSource);
        }

        foreach (var controllerName in new[]
        {
            "BorderExportLicenceByMethodReportController.cs",
            "BorderExportLicenceBySectionReportController.cs",
            "BorderExportLicenceBySellerCountryReportController.cs",
            "BorderExportLicenceCompanyListReportController.cs",
            "BorderExportLicenceDailyReportNewLicenceReportController.cs",
        })
        {
            var controllerSource = File.ReadAllText(Path.Combine(
                RepositoryRoot,
                "Backend",
                "Controllers",
                "Report",
                controllerName));

            Assert.Contains("sp_ExportLicenceDetailReport_Fast.CreateAggregateResultAsync", controllerSource);
            Assert.Contains("sp_ExportLicenceDetailReport_Fast.GetAggregateRowsAsync", controllerSource);
        }
    }

    [Fact]
    public void Export_licence_total_value_uses_export_composite_summary_contract()
    {
        var post = typeof(Backend.Controllers.Report.ExportLicenceTotalValueLicencesReportController)
            .GetMethod("Post")
            ?? throw new InvalidOperationException("Post action is missing.");

        var taskResultType = Assert.Single(post.ReturnType.GetGenericArguments());
        var actionResultType = Assert.Single(taskResultType.GetGenericArguments());

        Assert.Equal(typeof(ImportLicenceTotalValueLicencesSummary), actionResultType);
    }

    [Fact]
    public void Export_licence_total_value_usd_uses_sql_summary_path()
    {
        var fastSource = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "StoredProcedureToLinq",
            "sp_ExportLicenceDetailReport_Fast.cs"));

        var methodIndex = fastSource.IndexOf(
            "public static async Task<ImportLicenceTotalValueLicencesSummary> GetTotalValueLicencesSummaryAsync",
            StringComparison.Ordinal);
        Assert.True(methodIndex >= 0, "Export total value summary method was not found.");

        var aggregateRowsIndex = fastSource.IndexOf(
            "GetAggregateRowsAsync(db, request, ReportAggregateDimension.Daily",
            methodIndex,
            StringComparison.Ordinal);
        var sqlSummaryIndex = fastSource.IndexOf(
            "sp_ExportLicenceDetailReportV2.GetSummaryRowsAsync(db, request, ReportAggregateDimension.Daily)",
            methodIndex,
            StringComparison.Ordinal);

        Assert.True(sqlSummaryIndex >= 0, "Oversea Total USD Value must use the SQL summary path.");
        Assert.True(aggregateRowsIndex < 0 || sqlSummaryIndex < aggregateRowsIndex);
    }

    private static IReadOnlyList<ReportColumn> ExtractColumns(string config)
    {
        var columnsStart = config.IndexOf("columns: [", StringComparison.Ordinal);
        Assert.True(columnsStart >= 0, "ExportLicenceDetailReport must declare columns.");

        var openBracket = config.IndexOf('[', columnsStart);
        var closeBracket = FindMatching(config, openBracket, '[', ']');
        var columnsBlock = config[(openBracket + 1)..closeBracket];

        return Regex.Matches(columnsBlock, @"\{(?<body>.*?)\}", RegexOptions.Singleline)
            .Select(match => match.Groups["body"].Value)
            .Where(body => body.Contains("dataIndex:", StringComparison.Ordinal))
            .Select(body =>
            {
                var dataIndex = Regex.Match(body, @"dataIndex:\s*'(?<value>[^']+)'").Groups["value"].Value;
                var title = Regex.Match(body, @"title:\s*'(?<value>[^']+)'").Groups["value"].Value;
                var fallback = Regex.Match(
                    body,
                    @"fallbackDataIndexes:\s*\[(?<values>.*?)\]",
                    RegexOptions.Singleline);
                var fallbackIndexes = fallback.Success
                    ? Regex.Matches(fallback.Groups["values"].Value, @"'(?<value>[^']+)'")
                        .Select(fallbackMatch => fallbackMatch.Groups["value"].Value)
                        .ToArray()
                    : [];

                return new ReportColumn(dataIndex, title, fallbackIndexes);
            })
            .ToArray();
    }

    private static string ExtractReportConfig(string reportKey)
    {
        var configPath = Path.Combine(RepositoryRoot, "Frontend", "src", "Report", "config", "reportConfigs.ts");
        var source = File.ReadAllText(configPath);
        var keyIndex = source.IndexOf($"  {reportKey}: {{", StringComparison.Ordinal);
        Assert.True(keyIndex >= 0, $"{reportKey} config was not found.");

        var openBrace = source.IndexOf('{', keyIndex);
        var closeBrace = FindMatching(source, openBrace, '{', '}');
        return source[openBrace..(closeBrace + 1)];
    }

    private static int FindMatching(string source, int openIndex, char open, char close)
    {
        var depth = 0;
        for (var index = openIndex; index < source.Length; index++)
        {
            if (source[index] == open)
            {
                depth++;
            }
            else if (source[index] == close && --depth == 0)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"Could not find matching '{close}' for '{open}'.");
    }

    private static string ToCamelCase(string value) =>
        JsonNamingPolicy.CamelCase.ConvertName(value);

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

    private sealed record ReportColumn(
        string DataIndex,
        string Title,
        IReadOnlyList<string> FallbackDataIndexes);
}

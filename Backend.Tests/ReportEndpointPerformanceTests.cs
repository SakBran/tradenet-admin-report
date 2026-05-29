using System.Diagnostics;
using System.Collections;
using System.Reflection;
using System.Text;
using API.DBContext;
using API.Model;
using Backend.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Xunit.Abstractions;

namespace Backend.Tests;

public sealed class ReportEndpointPerformanceTests(ITestOutputHelper output)
{
    private static readonly DateTime PerformanceFromDate = ReportTestHelper.FromDate;
    private static readonly DateTime PerformanceToDate = ReportTestHelper.ToDate;
    private const int PageEndpointBudgetMs = 1_000;
    private const int LookupEndpointBudgetMs = 500;
    private const int ExcelEndpointBudgetMs = 5_000;
    private static readonly string[] ReportLookupNames =
    [
        "amendremarks",
        "businesstypes",
        "countries",
        "chequenos",
        "exportimportincoterms",
        "exportimportmethods",
        "exportimportsections",
        "lineofbusinesses",
        "nrcprefixcodes",
        "nrcprefixes",
        "pathakatypes",
        "sakhans"
    ];

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Report_api_endpoints_record_performance_baseline_against_seeded_database()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var results = new List<EndpointMeasurement>();

        foreach (var controllerType in ReportTestHelper.ControllerTypes)
        {
            results.Add(await MeasureEndpointAsync(controllerType, "Post", cache));
            results.Add(await MeasureEndpointAsync(controllerType, "Excel", cache));
        }

        foreach (var lookupName in ReportLookupNames)
        {
            results.Add(await MeasureReportLookupAsync(lookupName, cache));
        }

        var markdown = BuildMarkdown(results);
        var outputPath = Environment.GetEnvironmentVariable("REPORT_PERF_MARKDOWN_PATH");
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            await File.WriteAllTextAsync(outputPath, markdown);
            output.WriteLine($"Wrote report endpoint performance markdown to {Path.GetFullPath(outputPath)}");
        }

        var failedEndpoints = results
            .Where(result => result.Error != null)
            .Select(result => $"{result.Endpoint} {result.Action}: {result.Error}")
            .ToArray();

        Assert.Empty(failedEndpoints);

        if (Environment.GetEnvironmentVariable("REPORT_PERF_FAIL_ON_SLOW") == "1")
        {
            var slowEndpoints = results
                .Where(IsOverBudget)
                .Select(result => $"{result.Endpoint} {result.Action}: {result.ElapsedMilliseconds} ms")
                .ToArray();

            Assert.Empty(slowEndpoints);
        }
    }

    private static async Task<EndpointMeasurement> MeasureEndpointAsync(
        Type controllerType,
        string action,
        IMemoryCache cache)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var db = ReportTestHelper.CreateTradeNetDbTestDbContext();
            var controller = ReportTestHelper.CreateController(controllerType, db, cache);
            var request = CreatePerformanceRequest(controllerType);
            var result = await InvokeEndpointAsync(controllerType, controller, action, request);
            var resultSummary = GetResultSummary(action, result);

            stopwatch.Stop();
            return new EndpointMeasurement(
                EndpointPath(controllerType, action),
                action,
                stopwatch.ElapsedMilliseconds,
                resultSummary,
                null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new EndpointMeasurement(
                EndpointPath(controllerType, action),
                action,
                stopwatch.ElapsedMilliseconds,
                null,
                ex.GetBaseException().Message);
        }
    }

    private static async Task<EndpointMeasurement> MeasureReportLookupAsync(
        string lookupName,
        IMemoryCache cache)
    {
        var stopwatch = Stopwatch.StartNew();
        var endpoint = $"/api/ReportLookups/{lookupName}";
        try
        {
            await using var db = ReportTestHelper.CreateTradeNetDbTestDbContext();
            var controller = new ReportLookupsController(db, cache);
            var result = await controller.Get(lookupName);

            stopwatch.Stop();
            return new EndpointMeasurement(
                endpoint,
                "Get",
                stopwatch.ElapsedMilliseconds,
                GetLookupResultSummary(result),
                null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new EndpointMeasurement(
                endpoint,
                "Get",
                stopwatch.ElapsedMilliseconds,
                null,
                ex.GetBaseException().Message);
        }
    }

    private static object CreatePerformanceRequest(Type controllerType)
    {
        var request = ReportTestHelper.CreateRequest(controllerType);

        foreach (var property in request.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (propertyType == typeof(DateTime))
            {
                if (property.Name.Contains("From", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("Start", StringComparison.OrdinalIgnoreCase))
                {
                    property.SetValue(request, PerformanceFromDate);
                }
                else
                {
                    property.SetValue(request, PerformanceToDate);
                }
            }
        }

        if (request is ReportQueryRequest pagingRequest)
        {
            pagingRequest.PageIndex = 0;
            pagingRequest.PageSize = 10;
            pagingRequest.SortColumn = null;
            pagingRequest.SortOrder = null;
            pagingRequest.FilterColumn = null;
            pagingRequest.FilterQuery = null;
            pagingRequest.IncludeTotalCount = false;
        }

        return request;
    }

    private static async Task<object?> InvokeEndpointAsync(
        Type controllerType,
        ControllerBase controller,
        string action,
        object request)
    {
        var method = controllerType.GetMethod(action, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{controllerType.Name} is missing {action}.");
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(controller, [request]));

        await task;
        return ReportTestHelper.GetTaskResult(task);
    }

    private static string GetLookupResultSummary(object? result)
    {
        var resultProperty = result?.GetType().GetProperty("Result");
        var valueProperty = result?.GetType().GetProperty("Value");
        var actionResultObject = resultProperty?.GetValue(result);
        var value = actionResultObject is OkObjectResult ok
            ? ok.Value
            : valueProperty?.GetValue(result);

        return value is IEnumerable enumerable
            ? $"Options={enumerable.Cast<object>().Count()}"
            : "Options=0";
    }

    private static string GetResultSummary(string action, object? result)
    {
        if (action == "Post")
        {
            return $"TotalCount={ReportTestHelper.GetApiResultTotalCount(result!)}";
        }

        var file = Assert.IsType<FileContentResult>(result);
        return $"Bytes={file.FileContents.Length}";
    }

    private static string EndpointPath(Type controllerType, string action)
    {
        var controllerName = controllerType.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? controllerType.Name[..^"Controller".Length]
            : controllerType.Name;

        return action == "Excel"
            ? $"/api/{controllerName}/Excel"
            : $"/api/{controllerName}";
    }

    private static bool IsOverBudget(EndpointMeasurement result)
    {
        if (result.Error != null)
        {
            return true;
        }

        var budget = result.Action switch
        {
            "Excel" => ExcelEndpointBudgetMs,
            "Get" => LookupEndpointBudgetMs,
            _ => PageEndpointBudgetMs
        };

        return result.ElapsedMilliseconds > budget;
    }

    private static string BuildMarkdown(IReadOnlyList<EndpointMeasurement> results)
    {
        var needFix = results
            .Where(IsOverBudget)
            .OrderByDescending(result => result.Error != null)
            .ThenByDescending(result => result.ElapsedMilliseconds)
            .ToArray();
        var slowestPost = results
            .Where(result => result.Action == "Post")
            .OrderByDescending(result => result.ElapsedMilliseconds)
            .Take(25)
            .ToArray();
        var slowestExcel = results
            .Where(result => result.Action == "Excel")
            .OrderByDescending(result => result.ElapsedMilliseconds)
            .Take(25)
            .ToArray();
        var slowestLookup = results
            .Where(result => result.Action == "Get")
            .OrderByDescending(result => result.ElapsedMilliseconds)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("# Performance Need To Fix");
        builder.AppendLine();
        builder.AppendLine($"Updated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine();
        builder.AppendLine("## Scope");
        builder.AppendLine();
        builder.AppendLine($"- Report API controllers measured: {ReportTestHelper.ControllerTypes.Count}");
        builder.AppendLine($"- Report lookup endpoints measured: {ReportLookupNames.Length}");
        builder.AppendLine($"- Endpoints measured: {results.Count} (`POST /api/{{Report}}`, `POST /api/{{Report}}/Excel`, and `GET /api/ReportLookups/{{lookupName}}`).");
        builder.AppendLine($"- Data source: `TradeNetDBTest` via `ReportTestHelper.CreateTradeNetDbTestDbContext()`.");
        builder.AppendLine($"- Payload window: `{PerformanceFromDate:yyyy-MM-dd HH:mm:ss}` to `{PerformanceToDate:yyyy-MM-dd HH:mm:ss}`.");
        builder.AppendLine("- Paging payload: `PageIndex=0`, `PageSize=10`, no API sort/filter, `IncludeTotalCount=false`.");
        builder.AppendLine($"- Budgets: page endpoint <= {PageEndpointBudgetMs} ms; lookup endpoint <= {LookupEndpointBudgetMs} ms; Excel endpoint <= {ExcelEndpointBudgetMs} ms.");
        builder.AppendLine("- `Need Fix` includes endpoints that throw errors or exceed the current budget.");
        builder.AppendLine("- Interpretation: this is a local `TradeNetDBTest` baseline; rerun against a production-sized restore before treating the empty/fast rows as final capacity proof.");
        builder.AppendLine("- Non-report auth/upload/chat/user endpoints are not included here because they require separate credential, multipart upload, or write-safe mutation fixtures.");
        builder.AppendLine();
        builder.AppendLine("## How To Re-run");
        builder.AppendLine();
        builder.AppendLine("```powershell");
        builder.AppendLine("$env:REPORT_PERF_MARKDOWN_PATH='C:\\Code\\Ministry of Commerce\\Tradenet\\tradenet-admin-report\\PerformanceNeedToFix.md'");
        builder.AppendLine("dotnet test Backend.Tests\\Backend.Tests.csproj --filter \"FullyQualifiedName~ReportEndpointPerformanceTests\" -p:UseAppHost=false -p:BaseOutputPath=C:\\Code\\Ministry_of_Commerce_Tradenet_test_build\\");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("Set `REPORT_PERF_FAIL_ON_SLOW=1` before running when the test should fail on over-budget endpoints.");
        builder.AppendLine();
        builder.AppendLine("## Need Fix");
        builder.AppendLine();

        if (needFix.Length == 0)
        {
            builder.AppendLine("No measured endpoints exceeded the current performance budgets.");
        }
        else
        {
            AppendResultTable(builder, needFix);
        }

        builder.AppendLine();
        builder.AppendLine("## Slowest Page Endpoints");
        builder.AppendLine();
        AppendResultTable(builder, slowestPost);
        builder.AppendLine();
        builder.AppendLine("## Slowest Excel Endpoints");
        builder.AppendLine();
        AppendResultTable(builder, slowestExcel);
        builder.AppendLine();
        builder.AppendLine("## Slowest Lookup Endpoints");
        builder.AppendLine();
        AppendResultTable(builder, slowestLookup);
        builder.AppendLine();
        builder.AppendLine("## Full Results");
        builder.AppendLine();
        AppendResultTable(
            builder,
            results
                .OrderBy(result => result.Endpoint, StringComparer.Ordinal)
                .ThenBy(result => result.Action)
                .ToArray());

        return builder.ToString();
    }

    private static void AppendResultTable(StringBuilder builder, IReadOnlyList<EndpointMeasurement> results)
    {
        builder.AppendLine("| Endpoint | Action | Elapsed ms | Result | Status |");
        builder.AppendLine("| --- | ---: | ---: | --- | --- |");

        foreach (var result in results)
        {
            var status = result.Error == null
                ? IsOverBudget(result) ? "Need fix" : "OK"
                : "Error";
            var summary = result.Error ?? result.ResultSummary ?? string.Empty;

            builder.AppendLine(
                $"| `{result.Endpoint}` | `{result.Action}` | {result.ElapsedMilliseconds} | {EscapeMarkdown(summary)} | {status} |");
        }
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private sealed record EndpointMeasurement(
        string Endpoint,
        string Action,
        long ElapsedMilliseconds,
        string? ResultSummary,
        string? Error);
}

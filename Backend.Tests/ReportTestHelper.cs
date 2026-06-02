using System.Collections;
using System.Reflection;
using API.DBContext;
using API.Model;
using API.Service.Reports;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Tests;

internal static class ReportTestHelper
{
    internal static readonly DateTime FromDate = GetDateEnvironmentVariable(
        "TRADENET_REPORT_TEST_FROM_DATE",
        new DateTime(2000, 1, 1));
    internal static readonly DateTime ToDate = GetDateEnvironmentVariable(
        "TRADENET_REPORT_TEST_TO_DATE",
        new DateTime(2100, 12, 31, 23, 59, 59));

    internal static IReadOnlyList<Type> ControllerTypes { get; } =
        typeof(BorderExportPermitByHSCodeReportController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "Backend.Controllers.Report"
                && type.Name.EndsWith("Controller", StringComparison.Ordinal)
                && !type.IsAbstract)
            .OrderBy(type => type.Name)
            .ToArray();

    internal static IEnumerable<object[]> ControllerCases() =>
        ControllerTypes.Select(type => new object[] { type });

    internal static MethodInfo GetTryCreateReportRequest(Type controllerType)
    {
        return controllerType.GetMethod(
                "TryCreateReportRequest",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{controllerType.Name} is missing TryCreateReportRequest.");
    }

    internal static object CreateRequest(Type controllerType)
    {
        var requestType = GetRequestType(controllerType);
        var request = Activator.CreateInstance(requestType)
            ?? throw new InvalidOperationException($"Could not create {requestType.FullName}.");

        PopulateRequest(request);
        return request;
    }

    internal static Type GetRequestType(Type controllerType)
    {
        return GetTryCreateReportRequest(controllerType).GetParameters()[0].ParameterType;
    }

    internal static object CreateProcedureRequest(Type controllerType)
    {
        var method = GetTryCreateReportRequest(controllerType);
        var request = CreateRequest(controllerType);
        using var db = CreateInMemoryDbContext(controllerType.Name + "_ProcedureRequest");
        var controller = CreateController(controllerType, db);

        object?[] parameters = [request, null, null];
        var ok = Assert.IsType<bool>(method.Invoke(controller, parameters));

        Assert.True(ok, $"{controllerType.Name} should accept the standard report test request.");
        Assert.NotNull(parameters[1]);

        return parameters[1]!;
    }

    internal static ControllerBase CreateController(Type controllerType, TradeNetDbContext db)
    {
        var constructorArguments = controllerType
            .GetConstructors()
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .Select(constructor => constructor.GetParameters())
            .FirstOrDefault(parameters => parameters.All(parameter =>
                parameter.ParameterType == typeof(TradeNetDbContext)
                || parameter.ParameterType == typeof(ICountryCache)
                || parameter.ParameterType == typeof(IMemoryCache)))
            ?.Select(parameter => parameter.ParameterType switch
            {
                Type type when type == typeof(ICountryCache) => new EmptyCountryCache(),
                Type type when type == typeof(IMemoryCache) => new MemoryCache(new MemoryCacheOptions()),
                _ => (object)db
            })
            .ToArray()
            ?? [db];

        var controller = Assert.IsAssignableFrom<ControllerBase>(
            Activator.CreateInstance(controllerType, constructorArguments));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = ReportTestUser.AuthenticatedPrincipal
            }
        };

        return controller;
    }

    internal static TradeNetDbContext CreateInMemoryDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new TradeNetDbContext(options);
    }

    internal static TradeNetDbContext CreateSqlServerDbContext(string databaseName = "TradeNet_Report_Tests")
    {
        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new TradeNetDbContext(options);
    }

    internal static TradeNetDbContext CreateTradeNetDbTestDbContext()
    {
        var connectionString = Environment.GetEnvironmentVariable("TRADENET_REPORT_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Server=localhost;Initial Catalog=TradeNetDBTest;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";
        }

        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new TradeNetDbContext(options);
    }

    internal static Dictionary<string, object?> ToPayloadDictionary(object request)
    {
        return request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.Name)
            .ToDictionary(property => property.Name, property => property.GetValue(request));
    }

    internal static IQueryable CreateQuery(Type controllerType, TradeNetDbContext db)
    {
        var procedureRequest = CreateProcedureRequest(controllerType);
        var procedureRequestType = procedureRequest.GetType();
        var queryTypeName = procedureRequestType.FullName?.EndsWith("Request", StringComparison.Ordinal) == true
            ? procedureRequestType.FullName[..^"Request".Length]
            : throw new InvalidOperationException($"{procedureRequestType.FullName} does not follow the *Request naming pattern.");
        var queryType = procedureRequestType.Assembly.GetType(queryTypeName)
            ?? throw new InvalidOperationException($"Could not find query type {queryTypeName}.");
        var queryMethod = queryType.GetMethod(
                "Query",
                BindingFlags.Public | BindingFlags.Static,
                [typeof(TradeNetDbContext), procedureRequestType])
            ?? throw new InvalidOperationException($"{queryType.FullName} is missing the expected Query method.");

        return Assert.IsAssignableFrom<IQueryable>(queryMethod.Invoke(null, [db, procedureRequest]));
    }

    internal static async Task<object?> InvokePostAsync(
        Type controllerType,
        Func<TradeNetDbContext> createDbContext)
    {
        await using var db = createDbContext();
        var controller = CreateController(controllerType, db);
        var request = CreateRequest(controllerType);
        var post = controllerType.GetMethod("Post", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{controllerType.Name} is missing Post.");
        var task = Assert.IsAssignableFrom<Task>(post.Invoke(controller, [request]));

        await task;
        return GetTaskResult(task);
    }

    internal static async Task<IActionResult> InvokeExcelAsync(
        Type controllerType,
        Func<TradeNetDbContext> createDbContext)
    {
        await using var db = createDbContext();
        var controller = CreateController(controllerType, db);
        var request = CreateRequest(controllerType);
        var excel = controllerType.GetMethod("Excel", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{controllerType.Name} is missing Excel.");
        var task = Assert.IsAssignableFrom<Task>(excel.Invoke(controller, [request]));

        await task;
        return Assert.IsAssignableFrom<IActionResult>(GetTaskResult(task));
    }

    internal static object? GetTaskResult(Task task)
    {
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task);
    }

    internal static int GetApiResultTotalCount(object actionResult)
    {
        var resultProperty = actionResult.GetType().GetProperty("Result");
        var valueProperty = actionResult.GetType().GetProperty("Value");
        var actionResultObject = resultProperty?.GetValue(actionResult);
        var apiResult = actionResultObject is OkObjectResult ok
            ? ok.Value
            : valueProperty?.GetValue(actionResult);

        Assert.NotNull(apiResult);
        Assert.IsAssignableFrom<IEnumerable>(
            apiResult!.GetType().GetProperty(nameof(ApiResult<object>.Data))?.GetValue(apiResult));

        return Assert.IsType<int>(
            apiResult.GetType().GetProperty(nameof(ApiResult<object>.TotalCount))?.GetValue(apiResult));
    }

    private static void PopulateRequest(object request)
    {
        foreach (var property in request.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var value = CreateValue(property.Name, propertyType);
            if (value != null || Nullable.GetUnderlyingType(property.PropertyType) != null || !propertyType.IsValueType)
            {
                property.SetValue(request, value);
            }
        }
    }

    private static object? CreateValue(string propertyName, Type propertyType)
    {
        if (propertyType == typeof(string))
        {
            return string.Empty;
        }

        if (propertyType == typeof(DateTime))
        {
            if (propertyName.Contains("From", StringComparison.OrdinalIgnoreCase)
                || propertyName.Contains("Start", StringComparison.OrdinalIgnoreCase))
            {
                return FromDate;
            }

            return ToDate;
        }

        if (propertyType == typeof(int))
        {
            return propertyName is nameof(ReportQueryRequest.PageSize) ? 10 : 0;
        }

        if (propertyType == typeof(long))
        {
            return 0L;
        }

        if (propertyType == typeof(decimal))
        {
            return 0m;
        }

        if (propertyType == typeof(double))
        {
            return 0d;
        }

        if (propertyType == typeof(float))
        {
            return 0f;
        }

        if (propertyType == typeof(bool))
        {
            return false;
        }

        if (propertyType.IsEnum)
        {
            return Enum.GetValues(propertyType).GetValue(0);
        }

        return null;
    }

    private static DateTime GetDateEnvironmentVariable(string name, DateTime fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);

        return DateTime.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private sealed class EmptyCountryCache : ICountryCache
    {
        public IReadOnlyList<ReportLookupEntry> Countries => Array.Empty<ReportLookupEntry>();

        public string ResolveCsv(string? csvIds) => string.Empty;

        public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

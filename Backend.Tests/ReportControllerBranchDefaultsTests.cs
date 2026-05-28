namespace Backend.Tests;

public sealed class ReportControllerBranchDefaultsTests
{
    [Theory]
    [MemberData(nameof(FormTypeControllers))]
    public void Report_specific_form_type_controllers_ignore_blank_request_form_type(
        Type controllerType,
        string expectedFormType)
    {
        var procedureRequest = ReportTestHelper.CreateProcedureRequest(controllerType);

        var actualFormType = Assert.IsType<string>(
            procedureRequest.GetType().GetProperty("FormType")?.GetValue(procedureRequest));

        Assert.Equal(expectedFormType, actualFormType);
    }

    [Theory]
    [MemberData(nameof(ReportTypeControllers))]
    public void Report_specific_type_controllers_ignore_blank_request_type(
        Type controllerType,
        string expectedType)
    {
        var procedureRequest = ReportTestHelper.CreateProcedureRequest(controllerType);

        var actualType = Assert.IsType<string>(
            procedureRequest.GetType().GetProperty("Type")?.GetValue(procedureRequest));

        Assert.Equal(expectedType, actualType);
    }

    public static IEnumerable<object[]> FormTypeControllers()
    {
        return ReportTestHelper.ControllerTypes
            .Select(controllerType => new
            {
                ControllerType = controllerType,
                ExpectedFormType = GetExpectedFormType(controllerType.Name)
            })
            .Where(item => item.ExpectedFormType != null
                && HasProcedureRequestProperty(item.ControllerType, "FormType"))
            .Select(item => new object[] { item.ControllerType, item.ExpectedFormType! });
    }

    public static IEnumerable<object[]> ReportTypeControllers()
    {
        return ReportTestHelper.ControllerTypes
            .Select(controllerType => new
            {
                ControllerType = controllerType,
                ExpectedType = GetExpectedType(controllerType.Name)
            })
            .Where(item => item.ExpectedType != null
                && HasProcedureRequestProperty(item.ControllerType, "Type"))
            .Select(item => new object[] { item.ControllerType, item.ExpectedType! });
    }

    private static bool HasProcedureRequestProperty(Type controllerType, string propertyName)
    {
        var method = ReportTestHelper.GetTryCreateReportRequest(controllerType);
        var outParameterType = method.GetParameters()[1].ParameterType.GetElementType();

        return outParameterType?.GetProperty(propertyName) != null;
    }

    private static string? GetExpectedFormType(string controllerName)
    {
        var reportName = TrimControllerSuffix(controllerName);

        if (reportName.StartsWith("BorderExportPermit", StringComparison.Ordinal))
        {
            return "Border Export Permit";
        }

        if (reportName.StartsWith("BorderExportLicence", StringComparison.Ordinal))
        {
            return "Border Export Licence";
        }

        if (reportName.StartsWith("BorderImportPermit", StringComparison.Ordinal))
        {
            return "Border Import Permit";
        }

        if (reportName.StartsWith("BorderImportLicence", StringComparison.Ordinal))
        {
            return "Border Import Licence";
        }

        if (reportName.StartsWith("ExportPermit", StringComparison.Ordinal))
        {
            return "Export Permit";
        }

        if (reportName.StartsWith("ExportLicence", StringComparison.Ordinal))
        {
            return "Export Licence";
        }

        if (reportName.StartsWith("ImportPermit", StringComparison.Ordinal))
        {
            return "Import Permit";
        }

        if (reportName.StartsWith("ImportLicence", StringComparison.Ordinal))
        {
            return "Import Licence";
        }

        return null;
    }

    private static string? GetExpectedType(string controllerName)
    {
        var reportName = TrimControllerSuffix(controllerName);

        if (reportName.StartsWith("Border", StringComparison.Ordinal))
        {
            return "Border";
        }

        if (reportName.StartsWith("Export", StringComparison.Ordinal)
            || reportName.StartsWith("Import", StringComparison.Ordinal))
        {
            return "Oversea";
        }

        return null;
    }

    private static string TrimControllerSuffix(string controllerName)
    {
        return controllerName.EndsWith("Controller", StringComparison.Ordinal)
            ? controllerName[..^"Controller".Length]
            : controllerName;
    }
}

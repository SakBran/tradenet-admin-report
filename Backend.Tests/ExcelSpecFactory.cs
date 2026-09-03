using API.Model.ExcelExport;

namespace Backend.Tests;

/// <summary>
/// Hand-built presentation specs for the unit tests, shaped exactly like the ones the
/// frontend posts (so a change to the DTO breaks here first).
/// </summary>
internal static class ExcelSpecFactory
{
    internal static ExcelPresentationSpec Spec(
        string controllerName = "SampleReport",
        params ExcelSpecColumn[] columns)
        => new()
        {
            FormatVersion = ExcelPresentationSpec.CurrentFormatVersion,
            ConfigKey = controllerName,
            ControllerName = controllerName,
            Title = "Sample Report",
            FileName = "SampleReport",
            HeaderLines = [],
            ShowRowNumber = true,
            RowNumberTitle = "No",
            Columns = [.. columns],
        };

    internal static ExcelSpecColumn Column(
        string dataIndex,
        string title,
        string? dataType = null,
        string? key = null,
        string? numberFormat = null,
        params string[] fallbacks)
        => new()
        {
            Key = key ?? dataIndex,
            DataIndex = dataIndex,
            Title = title,
            DataType = dataType,
            NumberFormat = numberFormat,
            FallbackDataIndexes = fallbacks.Length == 0 ? null : [.. fallbacks],
        };
}

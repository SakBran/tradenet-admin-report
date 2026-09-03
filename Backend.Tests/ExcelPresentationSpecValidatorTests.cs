using API.Model.ExcelExport;

namespace Backend.Tests;

/// <summary>
/// The spec is client-supplied and ends up in file names, worksheet names and cell
/// text, so everything it carries has to be bounded and scrubbed before it is stored.
/// </summary>
public sealed class ExcelPresentationSpecValidatorTests
{
    [Fact]
    public void A_well_formed_spec_passes_and_keeps_its_values()
    {
        var spec = ExcelSpecFactory.Spec(
            "AccountSummaryReport",
            ExcelSpecFactory.Column("voucherDate", "Entry Date", "date"),
            ExcelSpecFactory.Column("amount", "Deducted Fees", "money"));

        Assert.True(
            ExcelPresentationSpecValidator.TryValidateAndSanitize(spec, true, out var errors),
            string.Join("; ", errors));
        Assert.Equal("Sample Report", spec.Title);
        Assert.Equal(2, spec.Columns.Count);
    }

    [Fact]
    public void Control_characters_and_runs_of_whitespace_are_scrubbed()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("a", "Col 	 A"));
        spec.Title = " Account    Summary \nReport ";
        spec.HeaderLines = ["", "   ", "Line one"];

        Assert.True(
            ExcelPresentationSpecValidator.TryValidateAndSanitize(spec, true, out var errors),
            string.Join("; ", errors));
        Assert.Equal("Account Summary Report", spec.Title);
        Assert.Equal(["Line one"], spec.HeaderLines);
        Assert.Equal("Col A", spec.Columns[0].Title);
    }

    [Theory]
    [InlineData("=cmd|'/c calc'!A1")]
    [InlineData("a b")]
    [InlineData("row[0]")]
    public void A_dataIndex_that_is_not_a_property_path_is_rejected(string dataIndex)
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column(dataIndex, "Col"));

        Assert.False(ExcelPresentationSpecValidator.TryValidateAndSanitize(spec, true, out var errors));
        Assert.Contains(errors, error => error.Contains("dataIndex"));
    }

    [Fact]
    public void An_unknown_dataType_is_rejected()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("a", "Col", "currency"));

        Assert.False(ExcelPresentationSpecValidator.TryValidateAndSanitize(spec, true, out var errors));
        Assert.Contains(errors, error => error.Contains("dataType"));
    }

    [Fact]
    public void An_over_long_title_or_file_name_is_rejected()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("a", "Col"));
        spec.Title = new string('x', ExcelPresentationSpecValidator.MaxTitleLength + 1);
        spec.FileName = new string('y', ExcelPresentationSpecValidator.MaxFileNameLength + 1);

        Assert.False(ExcelPresentationSpecValidator.TryValidateAndSanitize(spec, true, out var errors));
        Assert.Contains(errors, error => error.Contains("title"));
        Assert.Contains(errors, error => error.Contains("fileName"));
    }

    [Fact]
    public void Empty_columns_are_only_allowed_for_a_typed_layout_or_a_composite()
    {
        var spec = ExcelSpecFactory.Spec("R");

        Assert.False(ExcelPresentationSpecValidator.TryValidateAndSanitize(spec, true, out _));
        Assert.True(
            ExcelPresentationSpecValidator.TryValidateAndSanitize(spec, false, out var errors),
            string.Join("; ", errors));
    }

    [Fact]
    public void A_composite_spec_may_carry_sections_instead_of_columns()
    {
        var spec = ExcelSpecFactory.Spec("TotalValue");
        spec.Sections =
        [
            new ExcelSpecSection
            {
                Key = "byCurrency",
                Title = "Total Value",
                DataPath = "totalValueByCurrency",
                ShowRowNumber = true,
                RowNumberTitle = "Sr.No.",
                Columns =
                [
                    ExcelSpecFactory.Column("totalValue", "Total Value", "money", numberFormat: "#,##0.0000"),
                ],
            },
        ];
        spec.SummaryLines = [new ExcelSpecSummaryLine { Label = "Total USD Value", DataPath = "totalUsdValue" }];

        Assert.True(
            ExcelPresentationSpecValidator.TryValidateAndSanitize(spec, true, out var errors),
            string.Join("; ", errors));
    }

    [Fact]
    public void Currency_totals_columns_must_name_real_columns()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("currency", "Currency", key: "Currency"));
        spec.CurrencyTotalsColumns = new ExcelCurrencyTotalsPlacement
        {
            LabelColumnKey = "Currency",
            ValueColumnKey = "NotAColumn",
        };

        Assert.False(ExcelPresentationSpecValidator.TryValidateAndSanitize(spec, true, out var errors));
        Assert.Contains(errors, error => error.Contains("valueColumnKey"));
    }

    [Theory]
    [InlineData("Account Summary.xlsx", "Account Summary")]
    [InlineData("../../etc/passwd", "etcpasswd")]
    [InlineData("bad:name*here?", "badnamehere")]
    [InlineData("   ", null)]
    public void SanitizeFileNameBase_strips_the_extension_and_every_unsafe_character(string input, string? expected)
        => Assert.Equal(expected, ExcelPresentationSpecValidator.SanitizeFileNameBase(input));

    [Fact]
    public void SanitizeTitle_returns_null_for_nothing_usable()
    {
        Assert.Null(ExcelPresentationSpecValidator.SanitizeTitle("   "));
        Assert.Equal("Report", ExcelPresentationSpecValidator.SanitizeTitle(" Report "));
    }

    [Fact]
    public void ValidateAndSanitize_throws_a_spec_exception_carrying_every_error()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("not a path", "Col", "nope"));

        var exception = Assert.Throws<ExcelPresentationSpecException>(
            () => ExcelPresentationSpecValidator.ValidateAndSanitize(spec));

        Assert.True(exception.Errors.Count >= 2);
    }
}

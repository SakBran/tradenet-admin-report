# Implement — group D (the 4 `*TotalValueLicencesReport` composites) — read `_preamble.md`, then `Contract.md` §9

The UI page (`Frontend/src/Report/Page/<Key>TotalValueLicencesReport.tsx`) renders TWO tables plus a "Total USD
Value" line from a summary object; today's Excel dumps `ReportAggregateResult` rows. You may edit **only**
`controllerFile` and create `Backend.Tests/ExcelParity/<Controller>LayoutTests.cs`. Do not run dotnet/npm.

1. Read `Post`: it returns `ActionResult<ImportLicenceTotalValueLicencesSummary>` (or the family's sibling) built by
   a `GetTotalValueLicencesSummaryAsync`-style helper in the `sp_*_Fast` file. Note the exact helper and the
   summary's property names (`TotalValueByCurrency`, `TotalLicencesByPaThaKaType`, `TotalUsdValue`) and row types
   (`TotalValueByCurrencyRow { Currency, TotalValue }`, `TotalLicencesByPaThaKaTypeRow { PaThaKaType, NoOfLicences }`)
   in `Backend/Service/Reports/ImportLicenceTotalValueLicencesSummary.cs`.
2. Read the page to copy section order and labels exactly (`valueColumns`: Sr.No., Total Value, Currency;
   `licenceColumns`: Sr.No., Total Licences, Pa Tha Ka Type; the USD line label).
3. Implement `IExcelReportLayoutProvider` on the controller:
   ```csharp
   [NonAction]
   public ExcelReportLayout GetExcelLayout(object request)
   {
       var r = (<Name>Request)request;
       return new ExcelReportLayout
       {
           TitleLines = new[] { ExcelReportTitle.DateRange("<page heading label>", r.FromDate, r.ToDate) },
           Sections = new[]
           {
               new ExcelReportSection { Title = "Total Value", Columns = new[] {
                   ExcelColumn.RowNumber("Sr.No."),
                   ExcelColumn.Money4<TotalValueByCurrencyRow>("Total Value", x => x.TotalValue),
                   ExcelColumn.Text<TotalValueByCurrencyRow>("Currency", x => x.Currency) } },
               new ExcelReportSection { Title = "Total Licences", Columns = new[] {
                   ExcelColumn.RowNumber("Sr.No."),
                   ExcelColumn.Number<TotalLicencesByPaThaKaTypeRow>("Total Licences", x => x.NoOfLicences),
                   ExcelColumn.Text<TotalLicencesByPaThaKaTypeRow>("Pa Tha Ka Type", x => x.PaThaKaType) } },
           },
       };
   }
   ```
   Use the real type/member names from `ExcelReportLayout.cs` (read it; the core added `Sections`,
   `ExcelReportSection`, `Money4`). Add `[ExcelFormatVersion(<current+1>)]` on the class (current = 1 if absent → 2).
4. Rewrite the private `WriteRowsAsync` to emit the sections in page order using the same helper `Post` uses:
   ```csharp
   var summary = await <sp>.GetTotalValueLicencesSummaryAsync(_context, procedureRequest!);   // same call as Post
   sink.BeginSection(0); sink.Append(summary.TotalValueByCurrency);
   sink.BeginSection(1); sink.Append(summary.TotalLicencesByPaThaKaType);
   sink.AppendNote($"Total USD Value: {summary.TotalUsdValue.ToString("N4", CultureInfo.InvariantCulture)}");
   ```
5. Footer: the page has no footer → do NOT implement `IExcelFooterTotalsProvider`; the default resolver will find
   `Post` returns a non-`IReportTotals` type and yield no footer. State this in `notes`.
6. Unit test (`Backend.Tests/ExcelParity/<Controller>LayoutTests.cs`, xunit): `GetExcelLayout` is `[NonAction]`;
   two sections; headers equal the page labels in order; `ExcelFormatVersion` > 1. Construct the controller with
   `null!` dependencies like `StreamingExcelWriterTests` does for AccountSummary.

Return `REPORT` (shape in `implement-verify-only.md`); `rulesVerified.columnsExact` refers to sections vs page labels.

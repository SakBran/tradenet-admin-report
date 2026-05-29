# Report LINQ No Data Investigation

## Finding

Most empty report results were caused by missing internal discriminator filters, not by the visible `All` id filters.

The converted LINQ reports often branch like this:

```csharp
return request.FormType switch
{
    "Export Licence" => ExportLicenceQuery(db, request),
    "Import Licence" => ImportLicenceQuery(db, request),
    _ => EmptyQuery(db)
};
```

or:

```csharp
return request.Type switch
{
    "Oversea" => OverseaRows(db, request),
    "Border" => BorderRows(db, request),
    _ => EmptyRows(db)
};
```

The generated frontend configs had `FormType` and `Type` defaults as empty strings. When the user selected `All` on the visible id filters, the request still included `FormType: ""` or `Type: ""`, so the LINQ conversion returned the empty branch before querying real report rows.

## Original Admin Reference

The old admin project sets these values before calling the stored procedures:

- `ReportsController.cs` sets `model.FormType = AppConfig.ExportLicence`, `AppConfig.ImportLicence`, etc.
- `ReportsController.cs` sets `model.Type = AppConfig.Oversea` or `AppConfig.Border`.
- `Business/Reports.cs` passes those values directly into stored procedures such as `dbo.sp_NewReport`, `dbo.sp_AmendReport`, `dbo.sp_HSCodeReport`, and detail report procedures.

So the stored procedures work because the old admin never posts blank `FormType`/`Type` for those page-specific reports.

## Fix Applied

`Frontend/src/Report/Page/GenericReportPage.tsx` now derives hidden internal filter values from the report controller name:

- `BorderExportLicence*` -> `FormType = "Border Export Licence"`
- `BorderImportLicence*` -> `FormType = "Border Import Licence"`
- `BorderExportPermit*` -> `FormType = "Border Export Permit"`
- `BorderImportPermit*` -> `FormType = "Border Import Permit"`
- `ExportLicence*` -> `FormType = "Export Licence"`
- `ImportLicence*` -> `FormType = "Import Licence"`
- `ExportPermit*` -> `FormType = "Export Permit"`
- `ImportPermit*` -> `FormType = "Import Permit"`
- `Border*` reports with `Type` -> `Type = "Border"`
- Non-border licence/permit reports with `Type` -> `Type = "Oversea"`

Those derived fields are removed from the visible filter form but still included in the report API request.

## Other Risk Areas To Check Next

- Converted LINQ queries that use inner joins where the stored procedure used `LEFT JOIN` can still lose rows.
- Some date fields differ by report (`CreatedDate`, `LicenceDate`, `PaymentDate`, `IssuedDate`). Compare each LINQ conversion with the stored procedure when counts still disagree.
- Some controllers should keep user-facing `FormType` filters, such as account/payment summary reports. The shared fix only derives values for controller-name prefixes that represent a single licence/permit form type.

## Verification

- `npm run build` passed after the frontend fix.

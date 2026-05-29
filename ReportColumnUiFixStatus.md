# Report Column UI Fix Status

Generated: 2026-05-29T07:20:22.951Z

Old source: `C:\Code\Ministry of Commerce\Tradenet\tradenet-2.0-admin\TradenetAdmin`
New source: `Frontend/src/Report/config/reportConfigs.ts`

The frontend report table columns were reordered and relabeled from the old RDLC visible headers. Filters were relabeled to old UI wording where the current generic report screen supports the same filter.

A report is marked `Finished` when every old visible table header has a frontend column mapping. `Not finished` means the label is present, but one or more columns still need backend/computed data or a runtime dynamic header to fully match the old report behavior.

## Summary

- Reports checked: 125
- Finished: 36
- Not finished: 89

## Per-Report Status

### AccountSummaryReport

Status: Not finished
Old source: `AccountSummaryReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Remark`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceActualAmendmentReport

Status: Not finished
Old source: `BorderAmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceAmendmentReport

Status: Not finished
Old source: `BorderAmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceByHSCodeReport

Status: Not finished
Old source: `BorderHSCodeReport.rdlc`, `HSCodeDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceByMethodReport

Status: Not finished
Old source: `BorderExportLicenceByMethodReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceBySectionReport

Status: Not finished
Old source: `BorderExportLicenceBySectionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceBySellerCountryReport

Status: Not finished
Old source: `BorderExportLicenceByBuyerCountryReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceCancellationReport

Status: Not finished
Old source: `BorderCancelReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceCompanyListReport

Status: Not finished
Old source: `BorderExportLicenceByCompanyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceDailyReportNewLicenceReport

Status: Not finished
Old source: `BorderExportLicenceByDailyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Date`, `Total Value`, `Currency`
Old aggregate labels kept on detail-backed columns: `Total Value`, `Currency`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceDetailReport

Status: Not finished
Old source: `BorderExportLicenceDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Application Date`, `Application No`, `Commodity Type`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceExtensionReport

Status: Finished
Old source: `BorderExtensionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceNewReportNewReport

Status: Finished
Old source: `BorderNewReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceTotalValueLicencesReport

Status: Not finished
Old source: `BorderExportLicenceByTotalValueLicenceReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Total Value`
Old aggregate labels kept on detail-backed columns: `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportLicenceVoucherReport

Status: Not finished
Old source: `BorderVoucherReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: `=Parameters!header2.Value`, `=Parameters!header3.Value`

### BorderExportPermitActualAmendmentReport

Status: Not finished
Old source: `BorderAmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitAmendmentReport

Status: Not finished
Old source: `BorderAmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitByHSCodeReport

Status: Not finished
Old source: `BorderHSCodeReport.rdlc`, `HSCodeDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitBySectionReport

Status: Not finished
Old source: `BorderExportPermitBySectionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitBySellerCountryReport

Status: Not finished
Old source: `BorderExportPermitByBuyerCountryReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitCancellationReport

Status: Not finished
Old source: `BorderCancelReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitCompanyListReport

Status: Not finished
Old source: `BorderExportPermitByCompanyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitDailyReportNewPermitReport

Status: Not finished
Old source: `BorderExportPermitByDailyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Date`, `Total Value`, `Currency`
Old aggregate labels kept on detail-backed columns: `Total Value`, `Currency`
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitDetailReport

Status: Finished
Old source: `BorderExportPermitDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitExtensionReport

Status: Finished
Old source: `BorderExtensionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitNewReportNewReport

Status: Finished
Old source: `BorderNewReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderExportPermitVoucherReport

Status: Not finished
Old source: `BorderVoucherReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: `=Parameters!header2.Value`, `=Parameters!header3.Value`

### BorderImportLicenceActualAmendmentReport

Status: Not finished
Old source: `BorderAmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceAmendmentReport

Status: Not finished
Old source: `BorderAmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceByHSCodeReport

Status: Not finished
Old source: `BorderHSCodeReport.rdlc`, `HSCodeDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceByMethodReport

Status: Not finished
Old source: `BorderImportLicenceByMethodReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceBySectionReport

Status: Not finished
Old source: `BorderImportLicenceBySectionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceBySellerCountryReport

Status: Not finished
Old source: `BorderImportLicenceBySellerCountryReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceCancellationReport

Status: Not finished
Old source: `BorderCancelReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceCompanyListReport

Status: Not finished
Old source: `BorderImportLicenceByCompanyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceDailyReportNewLicenceReport

Status: Not finished
Old source: `BorderImportLicenceByDailyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Date`, `Total Value`, `Currency`
Old aggregate labels kept on detail-backed columns: `Total Value`, `Currency`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceDetailReport

Status: Finished
Old source: `BorderImportLicenceDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceDetailReportPending

Status: Finished
Old source: `BorderImportLicenceDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceExtensionReport

Status: Finished
Old source: `BorderExtensionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceNewReportNewReport

Status: Finished
Old source: `BorderNewReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicencePendingReport

Status: Finished
Old source: `PendingLicenceReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceTotalValueLicencesReport

Status: Not finished
Old source: `BorderImportLicenceByTotalValueLicenceReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Total Value`
Old aggregate labels kept on detail-backed columns: `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportLicenceVoucherReport

Status: Not finished
Old source: `VoucherReport.rdlc`, `BorderVoucherReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: `=Parameters!header2.Value`, `=Parameters!header3.Value`

### BorderImportPermitActualAmendmentReport

Status: Not finished
Old source: `AmendReport.rdlc`, `BorderAmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitAmendmentReport

Status: Not finished
Old source: `AmendReport.rdlc`, `BorderAmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitByHSCodeReport

Status: Not finished
Old source: `BorderHSCodeReport.rdlc`, `HSCodeDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitBySectionReport

Status: Not finished
Old source: `BorderImportPermitBySectionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitBySellerCountryReport

Status: Not finished
Old source: `BorderImportPermitBySellerCountryReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitCancellationReport

Status: Not finished
Old source: `BorderCancelReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitCompanyListReport

Status: Not finished
Old source: `BorderImportPermitByCompanyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitDailyReportNewPermitReport

Status: Not finished
Old source: `BorderImportPermitByDailyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Date`, `Total Value`, `Currency`
Old aggregate labels kept on detail-backed columns: `Total Value`, `Currency`
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitDetailReport

Status: Finished
Old source: `BorderImportPermitDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitExtensionReport

Status: Finished
Old source: `BorderExtensionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitNewReportNewReport

Status: Finished
Old source: `BorderNewReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### BorderImportPermitVoucherReport

Status: Not finished
Old source: `BorderVoucherReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: `=Parameters!header2.Value`, `=Parameters!header3.Value`

### CardListsByCompanyRegistrationNumber

Status: Finished
Old source: `CardListsByPaThaKa.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ChequeNoReport

Status: Finished
Old source: `ChequeNoReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### CompanyProfile

Status: Finished
Old source: `CompanyProfileReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### EIRCardBindReport

Status: Finished
Old source: `PathakaBindReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceActualAmendmentReport

Status: Not finished
Old source: `AmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceAmendmentReport

Status: Not finished
Old source: `AmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceByHSCodeReport

Status: Not finished
Old source: `HSCodeReport.rdlc`, `HSCodeDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceByMethodReport

Status: Not finished
Old source: `ExportLicenceByMethodReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceBySectionReport

Status: Not finished
Old source: `ExportLicenceBySectionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceBySellerCountryReport

Status: Not finished
Old source: `ExportLicenceByBuyerCountryReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceCancellationReport

Status: Not finished
Old source: `CancelReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceCompanyListReport

Status: Not finished
Old source: `ExportLicenceByCompanyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceDailyReportNewLicenceReport

Status: Not finished
Old source: `ExportLicenceByDailyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Date`, `Total Value`, `Currency`
Old aggregate labels kept on detail-backed columns: `Total Value`, `Currency`
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceDetailReport

Status: Not finished
Old source: `ExportLicenceDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Application Date`, `Application No`, `Commodity Type`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceExtensionReport

Status: Finished
Old source: `ExtensionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceNewReportNewReport

Status: Not finished
Old source: `NewLicenceReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceTotalValueLicencesReport

Status: Not finished
Old source: `ExportLicenceByTotalValueLicenceReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Total Value`
Old aggregate labels kept on detail-backed columns: `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ExportLicenceVoucherReport

Status: Not finished
Old source: `VoucherReport_Export.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: `=Parameters!header2.Value`, `=Parameters!header3.Value`

### ExportPermitActualAmendmentReport

Status: Not finished
Old source: `AmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitAmendmentReport

Status: Not finished
Old source: `AmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitByHSCodeReport

Status: Not finished
Old source: `HSCodeReport.rdlc`, `HSCodeDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitBySectionReport

Status: Not finished
Old source: `ExportPermitBySectionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitBySellerCountryReport

Status: Not finished
Old source: `ExportPermitByBuyerCountryReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitCancellationReport

Status: Not finished
Old source: `CancelReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitCompanyListReport

Status: Not finished
Old source: `ExportPermitByCompanyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitDailyReportNewPermitReport

Status: Not finished
Old source: `ExportPermitByDailyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Date`, `Total Value`, `Currency`
Old aggregate labels kept on detail-backed columns: `Total Value`, `Currency`
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitDetailReport

Status: Finished
Old source: `ExportPermitDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitExtensionReport

Status: Finished
Old source: `ExtensionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitNewReportNewReport

Status: Not finished
Old source: `NewLicenceReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ExportPermitVoucherReport

Status: Not finished
Old source: `VoucherReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: `=Parameters!header2.Value`, `=Parameters!header3.Value`

### ImportLicenceActualAmendmentReport

Status: Not finished
Old source: `AmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceAmendmentReport

Status: Not finished
Old source: `AmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceByHSCodeReport

Status: Not finished
Old source: `HSCodeReport.rdlc`, `HSCodeDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceByMethodReport

Status: Not finished
Old source: `ImportLicenceByMethodReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceBySectionReport

Status: Not finished
Old source: `ImportLicenceBySectionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceBySellerCountryReport

Status: Not finished
Old source: `ImportLicenceBySellerCountryReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceCancellationReport

Status: Not finished
Old source: `CancelReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceCompanyListReport

Status: Not finished
Old source: `ImportLicenceByCompanyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceDailyReportNewLicenceReport

Status: Not finished
Old source: `ImportLicenceByDailyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Date`, `Total Value`, `Currency`
Old aggregate labels kept on detail-backed columns: `Total Value`, `Currency`
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceDetailReport

Status: Finished
Old source: `ImportLicenceDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceDetailReportPending

Status: Finished
Old source: `ImportLicenceDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceExtensionReport

Status: Finished
Old source: `ExtensionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceNewReportNewReport

Status: Not finished
Old source: `NewLicenceReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportLicencePendingReport

Status: Finished
Old source: `PendingLicenceReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceTotalValueLicencesReport

Status: Not finished
Old source: `ImportLicenceByTotalValueLicenceReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Total Value`
Old aggregate labels kept on detail-backed columns: `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ImportLicenceVoucherReport

Status: Not finished
Old source: `VoucherReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: `=Parameters!header2.Value`, `=Parameters!header3.Value`

### ImportPermitActualAmendmentReport

Status: Not finished
Old source: `AmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitAmendmentReport

Status: Not finished
Old source: `AmendReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitByHSCodeReport

Status: Not finished
Old source: `HSCodeReport.rdlc`, `HSCodeDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitBySectionReport

Status: Not finished
Old source: `ImportPermitBySectionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitBySellerCountryReport

Status: Not finished
Old source: `ImportPermitBySellerCountryReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitCancellationReport

Status: Not finished
Old source: `CancelReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitCompanyListReport

Status: Not finished
Old source: `ImportPermitByCompanyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `No of Licences`, `Total Value`
Old aggregate labels kept on detail-backed columns: `No of Licences`, `Total Value`
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitDailyReportNewPermitReport

Status: Not finished
Old source: `ImportPermitByDailyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `Date`, `Total Value`, `Currency`
Old aggregate labels kept on detail-backed columns: `Total Value`, `Currency`
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitDetailReport

Status: Finished
Old source: `ImportPermitDetailReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitExtensionReport

Status: Finished
Old source: `ExtensionReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitNewReportNewReport

Status: Not finished
Old source: `NewLicenceReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `HSCode`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ImportPermitVoucherReport

Status: Not finished
Old source: `VoucherReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: `=Parameters!header2.Value`, `=Parameters!header3.Value`

### ListOfCompany

Status: Finished
Old source: `PaThaKaAllReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ListOfDirectorsByCompanyRegistrationNo

Status: Finished
Old source: `DirectorListByCompanyRegistrationNoReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ListOfDirectors

Status: Finished
Old source: `DirectorListReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ListOfTopCapitalCompany

Status: Finished
Old source: `TopCapitalCompanyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### ListOfValidAndInvalidCompany

Status: Finished
Old source: `ValidInvalidCompanyReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### MemberRegistrationReport

Status: Finished
Old source: `MemberRegistrationReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### MPUReport

Status: Not finished
Old source: `MPUReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `MPU`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### MPUReportV3

Status: Not finished
Old source: `MPUReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: `MPU`
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### OnlineFeesReport

Status: Finished
Old source: `OnlineFeesReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### PaThaKaRegisteredBusinessOrganizationReport

Status: Finished
Old source: `RegisteredBusinessOrganizationReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### RegistrationByBusinessType

Status: Finished
Old source: `PaThaKaRegistrationByBusinessTypeReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_

### RegistrationByVoucher

Status: Finished
Old source: `PaThaKaRegistrationByVoucherReport.rdlc`
Table labels: Finished
Filters: Finished
Columns needing data/computed support: _None_
Old aggregate labels kept on detail-backed columns: _None_
Runtime dynamic headers needing manual label choice: _None_


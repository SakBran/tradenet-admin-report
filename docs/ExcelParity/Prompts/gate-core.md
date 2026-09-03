# Gate: core (read `_preamble.md`, then `_gate-common.md`)

Purpose: prove the one-time core (backend + frontend) compiles, its unit tests pass, and the fixture generator
produced every fixture — before any per-report agent starts.

Steps (common procedure §1–§5) with these specifics:
- §3 filter: `FullyQualifiedName~ExcelExport|FullyQualifiedName~StreamingExcelWriter|FullyQualifiedName~ExcelLayoutBuilder|FullyQualifiedName~ExcelFooter|FullyQualifiedName~ExcelSpecContract|FullyQualifiedName~ExcelRowTypeResolver|FullyQualifiedName~RequireExcelPresentationSpec|FullyQualifiedName~ExcelPresentationSpec`.
  `results[]` here is one entry per **controller in the manifest** as far as the contract theory reports them (the
  theory runs over all fixtures already); controllers with no contract result yet get `passed:false, error:"no contract result"`.
  Additionally assert (put failures into `failures[]`): every `IStreamingExcelReport` controller is covered by at
  least one fixture (`Every_streaming_controller_has_a_spec_fixture`), and every registered handler has `FormatVersion >= 2`.
- §4 frontend: REQUIRED. Also check `Backend.Tests/Fixtures/ExcelSpecs/index.json` exists and its entry count
  equals the number your prompt gives. Expect **191 entries / 164 distinct configKeys**: 167 frontend configs
  minus the 3 excluded (CardListsByCompanyRegistrationNumber, DataImport, ImportLicenceDataImport) = 164 base
  (equivalently 160 controllers + 4 alias configs), plus 27 ApplyType variant entries (6 each for
  BorderExportLicenceVoucherReport and BorderExportPermitVoucherReport, whose ApplyType also offers De-Cancel;
  5 each for the other three voucher configs). The index lists one entry per file, each carrying `variant`.
  Do NOT let a repair agent drive the fixture count toward 167 — that would be wrong.
- `ok` additionally requires: `buildOk`, unit tests green, fixtures present, and the frontend build green.
  Lint and vitest failures matching `docs/ExcelParity/known-failures.json` (see §4b) do NOT block `ok`.

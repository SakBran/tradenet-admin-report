# Core — frontend (read `_preamble.md`, then `Contract.md` in full)

Implement the one-time frontend core exactly as `Contract.md` §3, §8 and §10 specify. You own
`Frontend/src/Report/excel/**`, `Frontend/src/Report/reportPresentation.ts`, `Frontend/src/Report/config/reportTypes.ts`
(only `hidden?: boolean`), `Frontend/src/Report/Page/GenericReportPage.tsx`, `Frontend/src/components/My Components/Table/BasicTable.tsx`
(only the dead `xlsx` path removal), `Frontend/package.json`, and `Backend.Tests/Fixtures/ExcelSpecs/**` (generated
output only). Do not touch other `Backend/` files and do not touch bespoke pages (a later phase does).

Order of work:
1. `reportTypes.ts`: `hidden?: boolean` on `ReportColumnConfig`.
2. `reportPresentation.ts`: move `formTypePrefixes`/`getDerivedFilterValues` out of `GenericReportPage.tsx`; add
   `formatLegacyReportDate`, `buildReportHeaderLines` (= `[...reportHeading, reportSubtitle ? subtitle(applied) : title]`, blanks filtered — NO From/To rows, the backend adds them), `isLegacyReportViewer`, `resolveRowNumberTitle`, `resolveReportColumns`, `getReportConfigKey`.
3. `excel/excelTypes.ts` (contract types, `EXCEL_PRESENTATION_FORMAT_VERSION = 1`), `excel/buildExcelPresentation.ts`
   (`toPresentationColumn` drops `drilldown`; `buildExcelPresentationFromInput`; `buildExcelPresentation`), `excel/excelEnqueue.ts`
   (`enqueueExcelExport`, `ExcelSpecRejectedError`, 400 → toast "reload the page (Ctrl+F5)").
4. `GenericReportPage.tsx`: use the shared helpers; `generateExcel` posts `{ ...buildRequest(currentFilters, query), excel: spec }`
   via `enqueueExcelExport`; `reportHeaderLines` from `buildReportHeaderLines` (still gated on `hasAppliedFilters`).
   Remove the now-duplicated local helpers.
5. `BasicTable.tsx`: delete the `xlsx` import, `exportClientTableToExcel`, and the `!onExcel` branch; `npm uninstall xlsx`.
6. `excel/bespoke/index.ts` registry (empty map for now — bespoke builders come in a later phase; export the type).
7. `excel/buildExcelPresentation.test.ts` (pure parity test over all configs + voucher ApplyType variants; see plan).
8. `excel/exportExcelSpecFixtures.test.ts` (verify mode default / write mode `EXCEL_SPEC_FIXTURES=write`); sample
   filters `FromDate 2026-02-01T00:00:00`, `ToDate 2026-02-28T23:59:59`, `Date 2026-02-15T00:00:00` + derived FormType/Type;
   output `Backend.Tests/Fixtures/ExcelSpecs/<ConfigKey>[.ApplyType-<opt>].json` + `index.json` (schema in `Contract.md` §10);
   deterministic (no timestamps, sorted, 2-space JSON, trailing newline). Add `"fixtures:excel"` script to `package.json`.
   Run it in write mode once so the fixtures exist for the backend contract test.
9. Run `cd Frontend && npm run build && npm run lint && npx vitest run`; fix what you broke.

Return `REPORT` with `controller: "core-frontend"`, `edits` per file, `concerns` (e.g. configs whose subtitle lacks the
title, configs with `=Parameters` placeholders surviving, alias handling), `notes` with the fixture count produced
and the exact regenerate command.

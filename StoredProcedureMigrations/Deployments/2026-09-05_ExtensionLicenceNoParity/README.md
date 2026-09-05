# Extension reports — "Licence No" column parity (2026-09-05)

**There is nothing to deploy to the database.** This folder holds a verification query only.

## What changed

Customer complaint: *Export Licence Extension Report — "License No" column တွင် Extension No
ပဲပြနေပါတယ်။*

Both the `Licence No` and the `Extension No` column were bound to the same field
(`licenceNo`), so the grid printed the extension's E-number twice. Tradenet 2.0 binds them to
different fields — `ExtensionReport.rdlc:303` header `Licence No` → `:868`
`=Fields!OldLicenceNo.Value` (the **parent**), `:358` `Extension No` → `:921`
`=Fields!LicenceNo.Value` (the **E-number**). `BorderExtensionReport.rdlc:374/993` and
`:429/1046` say the same for the Sakhan variants.

Fixed in `Frontend/src/Report/config/reportConfigs.ts` for four reports:

- `ExportLicenceExtensionReport` (the complaint)
- `ExportPermitExtensionReport`
- `BorderImportLicenceExtensionReport`
- `BorderImportPermitExtensionReport`

Locked by `Frontend/src/Report/config/reportConfigs.extension.test.ts`, which covers all seven
correct Extension reports and asserts the two bindings are never equal.

## Why no procedure ships here

An extension is the same row (`ApplyType='Extension'`) carrying both numbers — there is no
self-join to a parent. Every branch of `sp_ExtensionReport_pagination.sql` already projects
`OldLicenceNo` next to `LicenceNo` (lines 57, 102, 147, 207/235, 301/329, 382, 437), and
`sp_ExtensionReport.cs` already carries both on the DTO. The frontend simply never rendered
the parent. The Excel path streams raw DTO rows and already contained both numbers, so no
`ExcelFormatVersion` bump is needed either.

## How to verify

Run `VerifyDeployment.sql` against PROD. It is read-only and safe to run whole-file.

Section 1 is the one that matters: it reports, per report, how many `Extension` rows have a
**blank parent number**. Those rows will now render an empty `Licence No` cell — which is what
the old report did too, but confirm the share is small before shipping. `BlankParentPct` near 0
means ship it; a high percentage means raise it with the customer first.

## Still open

`ImportPermitExtensionReport` is bound the *other* way round (`Licence No` → `licenceNo`,
`Extension No` → `oldLicenceNo`) and `Frontend/src/Report/config/reportConfigs.importPermit.test.ts:90-100`
asserts that inversion. It contradicts the RDLC and the other seven reports, but it may encode
a deliberate customer decision — confirm before flipping it.

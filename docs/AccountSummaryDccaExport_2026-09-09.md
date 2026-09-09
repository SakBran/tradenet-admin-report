# Account Summary Report — DCCA export

Date: 2026-09-09 (rewritten same day after customer feedback)

## Request

> In the **OldReport → AccountSummary** screen, after searching, pressing the **black Export
> button** produces the format used to **import into DCCA**. … Please also provide **one more
> Excel format so it can be imported into DCCA**.

Then, after the first attempt:

> For DCCA Excel we don't need the column and column header. Start Date and End Date.
> Need to be byte identical as old report DCCA Feature.

## What the customer actually received the first time — and why

The first implementation (`9d1ae35`) rendered the DCCA sheet through the shared
`StreamingExcelWriter`. The file they got was **not that sheet at all**. Downloaded from PROD
(job `AccountSummaryReport-DCCA_20260909_050307.xlsx`, 27,915 rows):

```
r1  Account Summary Report (01/09/2026) To (09/09/2026)
r2  Account Summary Report (DCCA)
r3  From Date: 01/09/2026            <- the "Start Date"
r4  To Date: 09/09/2026              <- the "End Date"
r5  Exported: 09/09/2026 11:33
r6  No | Entry Date | Company Registration No | Company Name |
    Voucher No | Transaction Title | Deducted Fees | Remark      <- the standard 8 columns
r27922  | Total | | | | | 497760000 |
```

Its worksheet was named `Account Summary Report`, never `Sheet1` — so it was produced by a build
predating the DCCA commit. The filename and job title were DCCA-correct only because they ride on
the presentation spec, which the old build already honoured.

**Root cause: two Excel workers serve the queue, and one runs a stale build.**
`GET /api/ExcelExport/jobs`:

| `processedBy` | jobs |
|---|---|
| `TN2-ADMIN01:3bc29cfb7eed45cd95967c3d1a64052a` | 7 |
| `null` | 6 |

`ffc840d` made `processedBy` non-null on completion, so `null` means a worker older than that —
several commits before the DCCA work. The two interleave hour by hour, and the DCCA job was
claimed by the stale one. This is the unresolved operations item from
`BorderExportPermitComplaints_2026-09-07.md:59-92`. **No code change fixes it**; see the last
section.

## The rewrite: reproduce the old file instead of describing it

The old system used **EPPlus 4.1** to load `Content/excel-template/TransactionFees.xlsx`, fill
cells and save. We cannot use EPPlus: the backend has **no Excel library at all**, EPPlus 5+ is
Polyform Noncommercial (a paid licence for a ministry) and EPPlus 4.1 is .NET Framework only.

The key insight: **8 of the 10 parts EPPlus emitted do not depend on the data.** Only
`xl/worksheets/sheet1.xml` and `xl/sharedStrings.xml` vary. So the export now:

- ships a **skeleton** — the real legacy *output* with its 543 data rows deleted — as an
  `EmbeddedResource`, and copies those 8 parts out of it **byte-for-byte**;
- builds `sheet1.xml` by **splicing** the skeleton's own bytes at `<dimension`, `<sheetData>` and
  `</sheetData>`. Nothing about the prologue is retyped, so `sheetViews`, `sheetFormatPr`, all
  seven `<col>` elements (with their re-serialised attribute order), the header row,
  `pageMargins`, `pageSetup` and `<headerFooter />` cannot drift.

**The skeleton is derived from the OUTPUT, not the template — this matters.** EPPlus re-serialised
five of the eight static parts when it saved: `styles.xml` 2383→2975 bytes (gains
`<numFmts count="0" />`, loses `x14ac:knownFonts="1"`, `<xf>` attributes reordered),
`workbook.xml` 1167→1294 (gains `fullCalcOnLoad="1"`), plus smaller changes to
`[Content_Types].xml` and both `.rels`. Only `theme1.xml`, `docProps/core.xml` and
`docProps/app.xml` are byte-identical in both. A template-derived skeleton would therefore
mismatch five parts. `DccaWorkbookSkeleton.Validate()` guards the mixup with semantic markers the
template provably cannot satisfy (hashes alone would not — whoever made that mistake would update
them in the same commit).

## The measured target

10 parts in this zip order — `sharedStrings.xml` **last**, which is also convenient since the
string table is only complete once every row has streamed:

```
[Content_Types].xml  _rels/.rels  xl/workbook.xml  xl/_rels/workbook.xml.rels
xl/theme/theme1.xml  xl/styles.xml  xl/worksheets/sheet1.xml
docProps/core.xml  docProps/app.xml  xl/sharedStrings.xml
```

Dropped vs the template: `xl/printerSettings/printerSettings1.bin` and
`xl/worksheets/_rels/sheet1.xml.rels` — legal **only because** `r:id` is also stripped from
`<pageSetup orientation="portrait" />`. The two facts are a coupled invariant and are asserted
together; keeping the `r:id` while dropping the part makes Excel offer to repair the file.

- `<dimension ref="A1:I{last}"/>`, `<headerFooter />` after pageSetup
- Header `<row r="1" ht="18" customHeight="1">` (no `spans`, no `x14ac:dyDescent`)
- **No `<row r="2">` element at all** — the old loop is `int row = 2; foreach { row++; … }`, so
  r1 jumps to r3
- Per-cell styles: `A s="0"` numeric, `B s="3" t="s"`, `C D E G H I s="0" t="s"`, `F s="0"` numeric
  (census on the 543-row file: `4344×s="0"`, `543×s="3"`, `8×s="1"`, `1×s="2"`)
- `sheet1.xml` has a UTF-8 BOM and `standalone="yes"?>`; `sharedStrings.xml` has **no** BOM and
  `standalone="yes" ?>` (with a space). `<col …/>` has no space before `/>`; other self-closing
  tags do.
- Shared strings rebuilt in **first-use order**, and every string cell is shared — dates included,
  and the empty Remark interned as `<t></t>`. `count == uniqueCount` (both the unique total).

## EPPlus's rules were recovered from the binary, not guessed

`tradenet-2.0-admin/TradenetAdmin/bin/EPPlus.dll` (1,250,304 B) is in the repo. Its string heap
holds the rules verbatim:

```
&  &amp;  <  &lt;  >  &gt;  (_x[0-9A-F]{4,4}_)  _x005F  _x00{0}_
count="{0}" uniqueCount="{0}">  <si>  </si>  "  "  "\t"  "\n"
<si><t xml:space="preserve">  <si><t>  </t></si>  </sst>
```

So: escape `&`, `<`, `>` and **nothing else** (the real export has one raw `'` and zero `&apos;`);
double `_x005F` in front of anything already shaped like `_xNNNN_`; encode control characters as
`_x00NN_`; and use `xml:space="preserve"` iff the value starts or ends with a space, contains a
double space, a tab or a newline. **That predicate reproduces all 17 of the 543-row file's
preserved strings with 0 mismatches across all 498 entries** — verified.

This was worth doing rather than guessing: 17 company names arrive padded
(`"Shwe Htut Khaung Co., Ltd.                    "`) or double-spaced
(`"HONEYS  GARMENT  INDUSTRY  LIMITED"`). Dropping the attribute would silently trim them on
import — a data bug, not a cosmetic one.

**Numeric fidelity.** `sp_AccountSummaryReport_pagination.sql` declares `Amount float`. The old
chain was `float → .NET Framework double.ToString()` (which defaulted to **G15**) → `decimal.Parse`
(`Business/Reports.cs:4770`) → EPPlus formatting a `decimal`. .NET Core's default is
shortest-round-trippable, not G15, so a float artefact like `1234.5600000000001` would now be
emitted verbatim where the old file has `1234.56`. `DccaXmlText.Number` reproduces the round trip
(`decimal.Parse(v.ToString("G15", Invariant), Invariant).ToString(Invariant)`), closing the
`double`-vs-`decimal` item this doc previously left open.

**Nulls.** The old code read every string column as `DataRow[...].ToString()`, which yields `""`
for `DBNull` and never null — so coercing null to `""` is exact legacy reproduction, not a
divergence. (`VoucherDate` cannot be null anyway: the procedure's date predicate excludes NULLs.)

## Implementation

`Backend/Service/ExcelExport/Dcca/` — `Resources/TransactionFeesSkeleton.xlsx` (6,469 B),
`DccaWorkbookSkeleton.cs`, `DccaWorkbookWriter.cs`, `DccaXmlText.cs`, `DccaSpoolFile.cs`.

**The seam is 8 lines.** A new opt-in `ICustomExcelWriter { CanWriteCustomExcel; WriteCustomExcelAsync }`
(`IStreamingExcelReport.cs`, alongside the four existing opt-ins), dispatched at the top of
`ControllerStreamingExcelReportJobHandler.GenerateAsync` right after the request is deserialized —
before layout, header block, footer probe or `StreamingExcelWriter`. It is per-**request**, so the
report's normal export is untouched. For the other ~160 controllers the `is` test simply fails.

**The shared writer is byte-identical to its pre-DCCA state.** The first attempt had added
`ExcelReportLayout.SuppressStandardHeaderBlock` / `.BlankRowsAfterHeader` / `.WorksheetTitle` and
matching `StreamingExcelWriter` support; all of it was reverted, because a template-splicing writer
needs none of it. `git diff 6c4450e` is empty for `ExcelReportLayout.cs`, `ExcelLayoutBuilder.cs`,
`StreamingExcelWriter.cs` and `StreamingExcelWriterTests.cs`.

`AccountSummaryReportController` implements `ICustomExcelWriter` and reuses the **same** row stream
as the normal export (`includeTotalCount: false`, same ordering), so the two exports cannot
disagree about the data and the `COUNT(*)` that times this report out is still skipped.
`GetExcelLayout` now returns the standard layout for **every** request — deliberately a plain
return rather than a throw, so a regression in the dispatch produces a wrong file a test catches
instead of an opaque failed job. `[ExcelFormatVersion(2)] → (3)` (handler version 3 → 4), because
the DCCA bytes changed.

**Streaming.** `<dimension>` sits at the top of the sheet but the extent is only known at the end,
and string indices are assigned as rows arrive. Pre-counting is not an option — that `COUNT(*)` is
the thing that times this report out. So rows and new strings are staged to two
deflate-compressed `DeleteOnClose` temp files (~343 B/row raw, so a million rows is ~38 MB spooled
rather than ~343 MB), then spliced in. Row memory is O(1); only the unique-string dictionary grows.
The `ZipArchive` is created **only** in `FinishAsync` — a constructor-opened archive would write a
central directory into the output during `Dispose` on cancellation, and `ExcelExportWorker`
deliberately skips deleting the file on shutdown-cancellation, orphaning a workbook the cleanup
worker could never reclaim. Entry timestamps are pinned to 1980-01-01 so two exports of the same
data are byte-identical. Guarded at Excel's 1,048,574-row limit.

## Verification

**All ten parts SHA-256-identical to the real 2021 export**, proven by replaying its own 543 rows
back through the new writer (`Replaying_the_legacy_rows_reproduces_every_part_byte_for_byte`). That
file is deliberately **not committed** — real company names and voucher numbers — so the test is
gated on `TRADENET_DCCA_LEGACY_XLSX`; a committed SHA-256 manifest
(`Backend.Tests/Fixtures/Dcca/legacy-static-parts.sha256`) pins the eight static parts
unconditionally. Negative control: pointing the variable at the authoring template makes the test
fail, as it must.

Structural validation of generated files (data rows, and the zero-row case): every part well-formed,
full content-type coverage, no dangling relationships, no `r:id` in the sheet, dimension matching
the emitted rows, `count == uniqueCount == <si>` count, every shared-string index in range.
A deliberately nasty row confirmed padding and interior double-spaces preserved, `& < >` escaped
with the apostrophe left raw, Burmese script intact, the empty Remark interned, and
`1234.5600000000001` collapsed to `1234.56`.

Test runs: backend excluding the DB-bound suites — **21 failed / 1749 passed**, the failing set
*byte-identical* to the `main` baseline (those 21 are the repo's known red baseline).
Frontend untouched.

## The fidelity guarantee to state to the customer

> All **10 XML parts** are **byte-for-byte identical** to what the old system produced — verified
> by replaying the real 2 Sep 2021 export (543 rows) and comparing SHA-256 per part, including the
> Burmese company names, the `&` escapes, the 17 preserved-whitespace strings, the empty Remark,
> the missing row 2 and every style reference. Confirmed a second time against an export the old
> system generated on **17 Jun 2026**, and then against the file **production actually served on
> 9 Sep 2026** (29,321 rows).
>
> The **.xlsx container** cannot be byte-identical to the 2021 file, and never could be: a ZIP
> stamps the generation timestamp into every entry header, so **two runs of the OLD system minutes
> apart already produced different bytes**. What we guarantee instead is that two runs of the NEW
> system over the same data produce an identical file, and that
> `unzip -p file <part> | sha256sum` matches the old file for all ten parts.

## Second oracle: a June 2026 export from the old system

The customer supplied `AccountSummary.xlsx`, an export the OLD system generated on 17 Jun 2026
(zip stamps `2026-06-17 09:35`, 138 data rows) — five years newer than the 2021 file the skeleton
was cut from. Measured against the shipped skeleton:

| Checked | Result |
|---|---|
| The 8 data-independent parts | **byte-identical** (same SHA-256 as 2021) |
| Zip entry order, 10 entries, `sharedStrings.xml` last | identical |
| `sheet1.xml` bytes before `<dimension` | **byte-identical** |
| `<cols>` + header `<row r="1" ht="18" customHeight="1">` | **byte-identical** |
| `</sheetData>…<pageMargins/><pageSetup/><headerFooter/>` | **byte-identical** |
| Shared strings | `count="163" uniqueCount="163"`, strict first-use order, `<t></t>` for empty Remark |
| Style census | `s="0"`×1104, `s="3"`×138, `s="1"`×8, `s="2"`×1 — 138 rows through the writer's per-cell scheme |

So the old system's output format has not drifted since 2021, and the skeleton reproduces both.
`Backend.Tests/Fixtures/Dcca/legacy-oracles.sha256` records the sha256 and row count of both known
oracles; `TRADENET_DCCA_LEGACY_XLSX` now takes a `;`-separated list, and a path whose hash is not
in that manifest **fails** rather than passing — otherwise pointing the test at our own output
would "verify" byte-identity we never proved.

## Confirmed in production, 9 Sep 2026

Job `AccountSummaryReport-DCCA_20260909_082928.xlsx`, 29,321 rows, `processedBy
TN2-ADMIN01:785825ef...`:

- 10 entries in the expected order, `sharedStrings.xml` last, all zip stamps `1980-01-01` (the
  writer's pinned timestamp)
- worksheet `Sheet1`; `cellXfs count="4"` (EPPlus's table, not `StreamingExcelWriter`'s 11 or 13)
- the 8 static parts **SHA-256-identical to the committed 2021 manifest**
- `<dimension ref="A1:I29323" />`; rows 1, 3, 4, … 29323 — **no row 2**
- prologue, `<cols>` + header row, and the `pageSetup`/`headerFooter` tail byte-identical to the old
  export
- `count == uniqueCount == 30923`, BOM on `sheet1.xml` and none on `sharedStrings.xml`, `<t></t>`
  for the empty Remark, the `Transation Title` typo intact
- **316 strings carry `xml:space="preserve"`** — the padded and double-spaced company names that
  guessed escaping would have silently trimmed on import

Two earlier jobs the same morning (`…_050307`, `…_081829`) both had `processedBy: null` and were
the stale worker's 6-part standard sheet. The build that produced them reported
`cellXfs count="11"`, i.e. older than 2026-09-07 — it had never contained any DCCA code.

## The stale worker can no longer claim a job

`ExcelExportJobStatus` gained `QueuedV2 = 4` and `ProcessingV2 = 5`. `EnqueueAsync` writes
`QueuedV2`, the worker claims as `ProcessingV2`, and a retry requeues as `QueuedV2`
(`ExcelExportWorker.RequeueStatus`). An older API instance's candidate/claim query is compiled
against the literals `0` and `1`, so it matches nothing and starves — deterministic exclusion
instead of a coin toss.

- **`Status` is a plain `int` and the backend never calls `Migrate()`/`EnsureCreated()`**, so this
  needed zero schema change. That was the deciding constraint: a new column would simply not exist
  on TemplateDB.
- **Both** states are guarded. Poisoning only the queue leaves the orphan-reclaim branch
  (`Processing` + expired lease) open, so a stale worker would take the job the moment a lease
  lapsed — exactly when a reclaim is due.
- `Queued`/`Processing` stay claimable so rows written before this build still drain.
- The wire contract is unchanged: `ExcelExportController.StatusName` reports both queued values as
  `Queued` and both processing values as `Processing`. `ExportsDrive.tsx` polls on those strings and
  indexes its tag colours by them, so leaking `QueuedV2` would have stopped the auto-refresh. **No
  frontend change.**
- `ExcelExportStaleWorkerGuardTests` transcribes the old build's predicate **with raw ints** and
  asserts it finds nothing — it is a query inside a binary we do not control, so it must keep
  finding no work even if the enum is later renamed. The mirror test asserts the *real*
  `ExcelExportWorker.Claimable` still matches, so the guard cannot lock this build out too.

**Accepted trade-off:** if the current build's worker is ever down, exports sit visibly at "Queued"
instead of returning a stale file. A wrong file imported into a government accounting system is
worse than a stalled export.

## Which build produced a file is now one field

`ExcelExportWorker` appends a build stamp to its worker id — `"<machine>:<guid>@<sha>"` — read from
the assembly's `AssemblyInformationalVersion`, and logs it once at startup. `deploy.ps1` sets
`$env:SourceRevisionId` from `git rev-parse --short HEAD` before build/publish; it is set as an
environment variable rather than a `-p:` argument so MSBuild picks it up as a global property and
the existing command lines stay untouched. Verified locally: `1.0.0+deadbeef99` reached the DLL via
the environment alone.

Reading `processedBy` now answers "is the deployed build current?" directly: `null` means a worker
older than `ffc840d`, no `@` means older than this commit.

## Still owed

- **A trial import into DCCA.** Everything above verifies the file against the old *export*; only
  DCCA's importer can confirm it accepts it.
- **Row order.** Column A's serial and every string index follow it. Old:
  `dbo.sp_AccountSummaryReport`, no client sort (`Business/Reports.cs:4746-4755`). New:
  `sp_AccountSummaryReport_pagination` with a null sort → `ORDER BY PaymentDate, SortOrder, Id`.
  Needs a live-DB comparison before parity is claimed.

## Operations item (not code)

The guard makes a stale worker harmless, but it does not remove it: it still runs, burns CPU and
can claim legacy `Queued` rows. Find and stop it.

- `GET /api/ExcelExport/jobs` → `processedBy`. Since this commit, a value without `@<sha>` — or a
  `null` — identifies a worker that is not this build.
- Prime suspects: the retired UAT site `P:\WEBSITES\tradenet-admin-backend` (`deploy.ps1:13`,
  commented out as a target but never decommissioned, still pointed at the same TemplateDB), or a
  `dotnet run` left on the Build Server. `ExcelExportServiceCollectionExtensions` registers the
  worker unconditionally — there is no opt-out flag, so any running copy competes for jobs.
- `C:\ProgramData\TradeNetDeploy\auto-deploy.log` — confirm the watcher saw the commit and logged
  `Deploy finished OK`. `tools/auto-deploy-watch.ps1:66-84` does `git reset --hard origin/main`
  *before* deploying and **never retries a failure**, so a failed deploy leaves PROD on the
  previous build permanently with only a log line as evidence.

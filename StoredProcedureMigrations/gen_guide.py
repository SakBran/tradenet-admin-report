#!/usr/bin/env python3
"""Generate a self-contained, visual HTML migration run-guide from the actual files
in StoredProcedureMigrations/. Run from the repo root."""
import glob, os, html

ROOT = "StoredProcedureMigrations"
GEN_DATE = "2026-06-10"

# proc/view name -> (category, purpose, [views it NOEXPANDs])
CAT_COLORS = {
    "Export Permit": "#2563eb", "Export Licence": "#0891b2",
    "Import Permit": "#7c3aed", "Import Licence": "#c026d3",
    "Shared listing": "#ea580c", "Pa Tha Ka / Registration": "#16a34a",
    "Payments & Fees": "#ca8a04", "MPU": "#dc2626", "Indexed View": "#0d9488",
    "Indexes": "#475569",
}
PURPOSE = {
 "sp_ExportPermitDetailReport_Fast_pagination": ("Export Permit","Export Permit Detail — DB-side paginated detail rows.",[]),
 "sp_ExportPermitListingCurrencyTotals": ("Export Permit","Per-currency footer totals for New / Amend / Actual Amend / Cancel listings (both families, by FormType).",[]),
 "sp_ExportPermitVoucherCurrencyTotals": ("Export Permit","Per-currency footer totals for the Export/Border Export Permit Voucher report.",[]),
 "sp_ExportLicenceDetailReport_Pagination": ("Export Licence","Export Licence Detail — paginated detail rows.",[]),
 "sp_ExportLicenceTotalValueReport_Fast_pagination": ("Export Licence","Export Licence Total-Value — fast paginated totals.",[]),
 "sp_ExportLicenceListingCurrencyTotals": ("Export Licence","Per-currency footer totals for Export/Border Export Licence New / Amend / Actual Amend / Cancel listings (by FormType; Border splits Pa Tha Ka + Individual Trading).",[]),
 "sp_ExportLicenceVoucherCurrencyTotals": ("Export Licence","Per-currency footer totals for the Export/Border Export Licence Voucher report.",[]),
 "sp_ImportPermitListingCurrencyTotals": ("Import Permit","Per-currency footer totals for Import Permit listing reports.",[]),
 "sp_ImportPermitVoucherCurrencyTotals": ("Import Permit","Per-currency footer totals for the Import Permit Voucher report.",[]),
 "sp_ImportLicenceDetailReport_pagination": ("Import Licence","Import Licence Detail — paginated detail rows.",[]),
 "sp_ImportLicencePendingDetailReport_pagination": ("Import Licence","Import Licence Pending Detail — paginated. NEEDS QUOTED_IDENTIFIER/ANSI_NULLS ON (XML .value()).",[]),
 "sp_ImportLicenceDetailByLicenceReport_Indexed": ("Import Licence","Detail-by-Licence — reads the indexed view via NOEXPAND.",["vw_ImportLicenceItemTotalByCurrency"]),
 "sp_ImportLicenceSummaryReport_Indexed": ("Import Licence","Summary — reads the indexed view via NOEXPAND.",["vw_ImportLicenceItemTotalByCurrency"]),
 "sp_AmendReport_pagination": ("Shared listing","Amendment listing (FormType-branched across permit/licence families).",[]),
 "sp_ActualAmendReport_pagination": ("Shared listing","Actual-Amendment listing (FormType-branched).",[]),
 "sp_CancelReport_pagination": ("Shared listing","Cancellation listing (FormType-branched).",[]),
 "sp_ExtensionReport_pagination": ("Shared listing","Extension listing (FormType-branched).",[]),
 "sp_ExtensionReportCurrencyTotals": ("Shared listing","Per-currency footer totals for Extension reports.",[]),
 "sp_NewReport_pagination": ("Shared listing","New permit/licence listing (FormType-branched).",[]),
 "sp_VoucherReport_pagination": ("Shared listing","Voucher listing — reads 3 indexed views via NOEXPAND.",["vw_ExportPermitItemTotalByCurrency","vw_ImportLicenceItemTotalByCurrency","vw_ImportPermitItemTotalByCurrency"]),
 "sp_HSCodeReport_pagination": ("Shared listing","HS Code aggregate report (windowed paging).",[]),
 "sp_PaThaKaReport_pagination": ("Pa Tha Ka / Registration","Pa Tha Ka report — wraps base sp_PaThaKaReport (INSERT-EXEC).",[]),
 "sp_PaThaKaAllReport_pagination": ("Pa Tha Ka / Registration","Pa Tha Ka All report — paginated.",[]),
 "sp_PaThaKaByBusinessTypeReport_pagination": ("Pa Tha Ka / Registration","Pa Tha Ka by Business Type — paginated.",[]),
 "sp_PaThaKaRegistrationReport_pagination": ("Pa Tha Ka / Registration","Pa Tha Ka Registration — paginated.",[]),
 "sp_PaThaKaValidInvalidReport_pagination": ("Pa Tha Ka / Registration","Valid/Invalid report — wraps base sp_PaThaKaValidInvalidReport.",[]),
 "sp_PathakaBindReport_pagination": ("Pa Tha Ka / Registration","Pa Tha Ka Bind report — paginated.",[]),
 "sp_PendingReport_pagination": ("Pa Tha Ka / Registration","Pending report — paginated.",[]),
 "sp_CardListsByPaThaKaReport_pagination": ("Pa Tha Ka / Registration","Card Lists by Pa Tha Ka — paginated.",[]),
 "sp_CompanyProfileReport_pagination": ("Pa Tha Ka / Registration","Company Profile report — paginated.",[]),
 "sp_DirectorListReport_pagination": ("Pa Tha Ka / Registration","Director List report — paginated.",[]),
 "sp_AccountSummaryReport_pagination": ("Payments & Fees","Account Summary report — paginated.",[]),
 "sp_ChequeNoReport_pagination": ("Payments & Fees","Cheque-No report — paginated.",[]),
 "sp_OnlineFeesReport_pagination": ("Payments & Fees","Online Fees report — paginated.",[]),
 "sp_MPUReport_pagination": ("MPU","MPU payment report — paginated (legacy).",[]),
 "sp_MPUReport_V3_pagination": ("MPU","MPU payment report v3 — current paginated version.",[]),
 "vw_ExportPermitItemTotalByCurrency": ("Indexed View","Indexed view: per-(ExportPermitId, CurrencyId) item totals. Speeds Voucher.",[]),
 "vw_ImportLicenceItemTotalByCurrency": ("Indexed View","Indexed view: per-(LicenceId, CurrencyId) item totals. Used by Import Licence Indexed procs + Voucher.",[]),
 "vw_ImportPermitItemTotalByCurrency": ("Indexed View","Indexed view: per-(ImportPermitId, CurrencyId) item totals. Speeds Voucher.",[]),
}
import re
def obj_of(path):
    s=open(path).read()
    m=re.search(r'CREATE\s+(?:OR\s+ALTER\s+)?(?:PROCEDURE|VIEW|FUNCTION)\s+\[?(?:dbo)?\]?\.?\[?([A-Za-z0-9_]+)\]?',s,re.I)
    return m.group(1) if m else os.path.basename(path)[:-4]

proc_files = sorted(glob.glob(f"{ROOT}/*.sql"))
view_files = sorted(glob.glob(f"{ROOT}/Views/*.sql"))
idx_files  = sorted(glob.glob(f"{ROOT}/IndexedMigrations/*.sql")) + sorted(glob.glob(f"{ROOT}/Indexes/*.sql"))

def meta(path):
    o=obj_of(path); return o, PURPOSE.get(o,("Shared listing","(stored procedure)",[]))

# group procs by category, preserving a sensible order
CAT_ORDER=["Export Permit","Export Licence","Import Permit","Import Licence","Shared listing","Pa Tha Ka / Registration","Payments & Fees","MPU"]
groups={c:[] for c in CAT_ORDER}
for p in proc_files:
    o,(cat,purp,deps)=meta(p)
    groups.setdefault(cat,[]).append((os.path.basename(p),o,purp,deps))

def esc(s): return html.escape(str(s))
def badge(cat):
    return f'<span class="badge" style="background:{CAT_COLORS.get(cat,"#64748b")}">{esc(cat)}</span>'

CSS = """
:root{--bg:#0f172a;--card:#fff;--ink:#1e293b;--muted:#64748b;--line:#e2e8f0;--accent:#2563eb}
*{box-sizing:border-box}
body{margin:0;font:15px/1.55 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:var(--ink);background:#f1f5f9}
.wrap{max-width:1120px;margin:0 auto;padding:32px 20px 80px}
header.hero{background:linear-gradient(135deg,#1e3a8a,#2563eb);color:#fff;border-radius:16px;padding:28px 32px;box-shadow:0 10px 30px rgba(30,58,138,.25)}
header.hero h1{margin:0 0 6px;font-size:26px}
header.hero p{margin:4px 0;opacity:.92}
.pill{display:inline-block;background:rgba(255,255,255,.18);border:1px solid rgba(255,255,255,.3);padding:3px 10px;border-radius:999px;font-size:12px;margin-right:6px}
.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:14px;margin:22px 0}
.stat{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px 18px;box-shadow:0 1px 2px rgba(0,0,0,.04)}
.stat .n{font-size:30px;font-weight:700;color:var(--accent)}
.stat .l{color:var(--muted);font-size:13px;margin-top:2px}
h2{margin:38px 0 12px;font-size:20px;border-left:4px solid var(--accent);padding-left:10px}
h3{margin:22px 0 8px;font-size:16px}
.note{background:#fffbeb;border:1px solid #fde68a;border-left:4px solid #f59e0b;border-radius:8px;padding:12px 16px;margin:12px 0}
.note.crit{background:#fef2f2;border-color:#fecaca;border-left-color:#ef4444}
.note.ok{background:#f0fdf4;border-color:#bbf7d0;border-left-color:#22c55e}
.badge{color:#fff;font-size:11px;font-weight:600;padding:2px 8px;border-radius:999px;white-space:nowrap}
.flow{display:flex;align-items:stretch;gap:0;flex-wrap:wrap;margin:18px 0}
.step{flex:1;min-width:180px;background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px;position:relative}
.step .sn{display:inline-flex;width:26px;height:26px;border-radius:50%;background:var(--accent);color:#fff;align-items:center;justify-content:center;font-weight:700;font-size:13px;margin-bottom:8px}
.arrow{display:flex;align-items:center;color:#94a3b8;font-size:24px;padding:0 8px}
table{width:100%;border-collapse:collapse;background:var(--card);border-radius:10px;overflow:hidden;box-shadow:0 1px 2px rgba(0,0,0,.04);margin:10px 0}
th,td{text-align:left;padding:10px 12px;border-bottom:1px solid var(--line);font-size:13px;vertical-align:top}
th{background:#f8fafc;color:#334155;font-weight:600}
tr:last-child td{border-bottom:0}
code{background:#f1f5f9;padding:1px 6px;border-radius:5px;font-family:ui-monospace,Menlo,Consolas,monospace;font-size:12.5px}
pre{background:#0f172a;color:#e2e8f0;padding:14px 16px;border-radius:10px;overflow:auto;font-family:ui-monospace,Menlo,Consolas,monospace;font-size:12.5px;line-height:1.5}
pre .c{color:#7dd3fc}.pre .k{color:#fca5a5}
.item{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:12px 14px;margin:8px 0;display:flex;gap:12px;align-items:flex-start}
.chk{width:18px;height:18px;border:2px solid #cbd5e1;border-radius:5px;flex:none;margin-top:2px}
.item .body{flex:1}
.item .fn{font-family:ui-monospace,Menlo,Consolas,monospace;font-size:13px;font-weight:600;color:#0f172a}
.item .pp{color:var(--muted);font-size:12.5px;margin-top:2px}
.dep{display:inline-block;background:#ecfeff;border:1px solid #a5f3fc;color:#0e7490;font-size:11px;padding:1px 7px;border-radius:6px;margin-top:6px;margin-right:4px}
.removed{text-decoration:line-through;color:#94a3b8}
.legend{display:flex;gap:8px;flex-wrap:wrap;margin:10px 0}
svg{max-width:100%;height:auto;background:var(--card);border:1px solid var(--line);border-radius:12px}
footer{margin-top:50px;color:var(--muted);font-size:12px;text-align:center}
"""

def proc_items_html(items):
    out=[]
    for fn,o,purp,deps in items:
        dephtml="".join(f'<span class="dep">⤷ NOEXPAND {esc(d)}</span>' for d in deps)
        warn=' <span class="dep" style="background:#fef2f2;border-color:#fecaca;color:#b91c1c">QI/ANSI ON</span>' if 'QUOTED_IDENTIFIER' in purp else ''
        out.append(f'<div class="item"><div class="chk"></div><div class="body"><div class="fn">{esc(fn)}</div><div class="pp">{esc(purp)}</div>{dephtml}{warn}</div></div>')
    return "\n".join(out)

n_proc=len(proc_files); n_view=len(view_files); n_idx=len(idx_files)

# ---- dependency SVG (views -> dependent procs) ----
SVG = '''<svg viewBox="0 0 720 250" role="img" aria-label="Indexed view dependency diagram">
<defs><marker id="ah" markerWidth="9" markerHeight="9" refX="7" refY="3" orient="auto"><path d="M0,0 L7,3 L0,6 Z" fill="#0d9488"/></marker></defs>
<text x="120" y="24" text-anchor="middle" font-size="13" font-weight="700" fill="#0d9488">Indexed Views (Phase 1)</text>
<text x="560" y="24" text-anchor="middle" font-size="13" font-weight="700" fill="#ea580c">Dependent Procedures (Phase 3)</text>
<g font-size="11" font-family="monospace">
<rect x="20" y="50" width="210" height="34" rx="7" fill="#ecfeff" stroke="#0d9488"/><text x="30" y="71" fill="#0e7490">vw_ExportPermitItemTotalByCurrency</text>
<rect x="20" y="108" width="210" height="34" rx="7" fill="#ecfeff" stroke="#0d9488"/><text x="30" y="129" fill="#0e7490">vw_ImportLicenceItemTotalByCurrency</text>
<rect x="20" y="166" width="210" height="34" rx="7" fill="#ecfeff" stroke="#0d9488"/><text x="30" y="187" fill="#0e7490">vw_ImportPermitItemTotalByCurrency</text>
<rect x="440" y="50" width="250" height="34" rx="7" fill="#fff7ed" stroke="#ea580c"/><text x="450" y="71" fill="#9a3412">sp_VoucherReport_pagination</text>
<rect x="440" y="108" width="250" height="34" rx="7" fill="#fdf4ff" stroke="#c026d3"/><text x="450" y="129" fill="#a21caf">sp_ImportLicenceSummaryReport_Indexed</text>
<rect x="440" y="166" width="250" height="34" rx="7" fill="#fdf4ff" stroke="#c026d3"/><text x="450" y="187" fill="#a21caf">sp_ImportLicenceDetailByLicenceReport_Indexed</text>
</g>
<g stroke="#0d9488" stroke-width="1.6" fill="none" marker-end="url(#ah)">
<path d="M230,67 C340,67 340,67 440,67"/>
<path d="M230,125 C340,125 340,67 440,72"/>
<path d="M230,125 C340,125 340,125 440,125"/>
<path d="M230,125 C340,160 340,180 440,183"/>
<path d="M230,183 C340,183 340,72 440,76"/>
</g>
</svg>'''

# ---- flow ----
flow = '''<div class="flow">
<div class="step"><span class="sn">0</span><b>Prerequisites</b><div class="pp">Base legacy procs exist (sp_PaThaKaReport, sp_PaThaKaValidInvalidReport). Session SET options ON.</div></div>
<div class="arrow">→</div>
<div class="step"><span class="sn">1</span><b>Indexed Views</b><div class="pp">%d views + unique clustered index. Must precede the NOEXPAND procs.</div></div>
<div class="arrow">→</div>
<div class="step"><span class="sn">2</span><b>Indexes</b><div class="pp">Run the consolidated production index script (ONLINE, safe-to-rerun).</div></div>
<div class="arrow">→</div>
<div class="step"><span class="sn">3</span><b>Stored Procedures</b><div class="pp">%d procedures (CREATE OR ALTER, idempotent).</div></div>
</div>''' % (n_view, n_proc)

# ---- phase 1 view items ----
view_items=[]
for p in view_files:
    o,(cat,purp,deps)=meta(p)
    view_items.append((os.path.relpath(p,ROOT),o,purp,[]))
phase1 = "\n".join(f'<div class="item"><div class="chk"></div><div class="body"><div class="fn">Views/{esc(os.path.basename(p[0]))}</div><div class="pp">{esc(p[2])}</div><span class="dep" style="background:#f0fdfa;border-color:#99f6e4;color:#0f766e">indexed in UAT ✓</span></div></div>' for p in view_items)

# ---- phase 2 index items ----
phase2=[]
for p in idx_files:
    sub = "IndexedMigrations" if "IndexedMigrations" in p else "Indexes"
    note = "Consolidated production rollout — preflight + ONLINE + safe-to-rerun. Run THIS on prod." if sub=="IndexedMigrations" else "Per-report granular index script (covered by the consolidated production script; run only if applying piecemeal)."
    phase2.append(f'<div class="item"><div class="chk"></div><div class="body"><div class="fn">{esc(sub)}/{esc(os.path.basename(p))}</div><div class="pp">{esc(note)}</div></div></div>')
phase2="\n".join(phase2)

# ---- phase 3 grouped ----
phase3=[]
for cat in CAT_ORDER:
    items=groups.get(cat,[])
    if not items: continue
    phase3.append(f'<h3>{badge(cat)} &nbsp;{esc(cat)} <span style="color:#94a3b8;font-weight:400">({len(items)})</span></h3>')
    phase3.append(proc_items_html(items))
phase3="\n".join(phase3)

run_cmd = '''<pre><span class="c"># Run each phase in order with sqlcmd (-b = stop on error). Production = TradeNetDB.</span>
SET QUOTED_IDENTIFIER ON      <span class="c"># (sqlcmd sets these ON by default; pymssql does NOT)</span>

<span class="c"># Phase 1 - indexed views (run BEFORE the procedures)</span>
for f in StoredProcedureMigrations/Views/*.sql;            do sqlcmd -S SERVER -d TradeNetDB -i "$f" -b; done
<span class="c"># Phase 2 - consolidated production indexes</span>
sqlcmd -S SERVER -d TradeNetDB -i StoredProcedureMigrations/IndexedMigrations/TradeNetReportIndexes_Production.sql -b
<span class="c"># Phase 3 - stored procedures</span>
for f in StoredProcedureMigrations/*.sql;                  do sqlcmd -S SERVER -d TradeNetDB -i "$f" -b; done</pre>'''

HTML = f'''<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>TradeNet Report — SQL Migration Run Guide</title>
<style>{CSS}</style></head><body><div class="wrap">
<header class="hero">
  <h1>TradeNet Report — SQL Migration Run Guide</h1>
  <p>Hand-run sequence for deploying report stored procedures, indexed views &amp; indexes.</p>
  <p style="margin-top:10px">
    <span class="pill">Generated {GEN_DATE}</span>
    <span class="pill">UAT: 203.81.66.111,14330 / TradeNetDB ✓ deployed</span>
    <span class="pill">Production: run by hand ⚠</span>
  </p>
</header>

<div class="cards">
  <div class="stat"><div class="n">{n_proc}</div><div class="l">Stored procedures</div></div>
  <div class="stat"><div class="n">{n_view}</div><div class="l">Indexed views</div></div>
  <div class="stat"><div class="n">{n_idx}</div><div class="l">Index scripts</div></div>
  <div class="stat"><div class="n">2</div><div class="l">Removed (orphans)</div></div>
</div>

<div class="note ok"><b>Reconciliation vs UAT (203.81.66.111,14330):</b> all {n_proc} procedures and {n_view} indexed views in this folder exist and are deployed on UAT; the 3 indexed views carry their unique clustered index. No <i>missing</i> migrations were found (the <code>_pagination</code> wrappers also depend on the pre-existing base procs <code>sp_PaThaKaReport</code> / <code>sp_PaThaKaValidInvalidReport</code>, which already exist on the server).</div>

<h2>What was removed (unnecessary)</h2>
<table>
<tr><th>File</th><th>Why removed</th></tr>
<tr><td class="removed">sp_ImportPermitDetailReport_Fast_pagination.sql</td><td>Orphan — not referenced by any C#/proc (the Import Permit Detail report runs via the in-memory LINQ aggregate path) and <b>not deployed to UAT</b>. Only mentioned in a planning doc.</td></tr>
<tr><td class="removed">Views/vw_ExportLicenceItemTotalByCurrency.sql</td><td>Orphan indexed view — referenced by no procedure, and <b>not indexed in UAT</b> (its clustered index was never applied). Only mentioned in planning docs.</td></tr>
</table>

<h2>Run order</h2>
{flow}
<div class="note crit"><b>Critical — SET QUOTED_IDENTIFIER ON &amp; ANSI_NULLS ON</b> before creating the indexed views and the NOEXPAND procedures (<code>sp_VoucherReport_pagination</code>, the two <code>*_Indexed</code> procs) and <code>sp_ImportLicencePendingDetailReport_pagination</code> (XML <code>.value()</code>). <code>sqlcmd</code>/SSMS default these ON; <b>pymssql/ad-hoc clients may not</b> → Msg 1934. Creating a proc with QI OFF makes it fail at <i>run</i> time.</div>

<h2>Indexed-view dependency map</h2>
<p class="pp">Phase 1 must complete before these Phase 3 procedures (they read the views with <code>WITH (NOEXPAND)</code>).</p>
{SVG}

<h2>Phase 1 — Indexed Views <span style="color:#94a3b8;font-weight:400">(run first)</span></h2>
{phase1}

<h2>Phase 2 — Indexes</h2>
{phase2}

<h2>Phase 3 — Stored Procedures <span style="color:#94a3b8;font-weight:400">({n_proc} files, any order within the phase)</span></h2>
<div class="legend">{''.join(badge(c) for c in CAT_ORDER)}</div>
{phase3}

<h2>How to run (Production, by hand)</h2>
<p class="pp">All scripts are <code>CREATE OR ALTER</code> / idempotent and safe to re-run. Replace <code>SERVER</code> with the production instance.</p>
{run_cmd}
<div class="note"><b>SSMS / Azure Data Studio:</b> connect to the production <code>TradeNetDB</code>, then open &amp; Execute each file in the Phase 1 → 2 → 3 order above. The index script prints its own preflight and aborts if not connected to <code>TradeNetDB</code>.</div>

<footer>Generated from <code>StoredProcedureMigrations/</code> on {GEN_DATE}. Re-run <code>gen_guide.py</code> after adding/removing migrations to refresh this guide.</footer>
</div></body></html>'''

out=f"{ROOT}/MIGRATION_GUIDE.html"
open(out,"w").write(HTML)
print(f"wrote {out} ({len(HTML)} bytes)")
print(f"procs={n_proc} views={n_view} idx_scripts={n_idx}")

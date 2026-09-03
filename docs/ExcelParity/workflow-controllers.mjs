export const meta = {
  name: 'excel-parity-controllers-first',
  description: 'Controllers first: batched agents change all 160 report controllers, then one build gate, then verification, pages and status',
  phases: [
    { title: 'Controllers', detail: 'batched per-family agents edit every controller; no builds, no verification yet' },
    { title: 'Build gate', detail: 'one build + full contract test, then targeted repairs until green' },
    { title: 'Verify', detail: 'batched skeptics over the judgment-heavy reports, then repairs' },
    { title: 'Bespoke pages', detail: 'one agent per bespoke frontend page, then the frontend gate' },
    { title: 'Final gate + status', detail: 'full gate, e2e spreadsheet check, completeness critic, Status.md' },
  ],
}

const A = args || {}
if (!A.repoRoot || !Array.isArray(A.implBatches)) throw new Error('args.repoRoot and args.implBatches are required')
const IMPL = A.implBatches
const SKEPT = A.skepticBatches || []
const PAGES = A.pages || []
const MAX_REPAIRS = A.maxRepairs || 3
const RUN_STAMP = A.runStamp || 'unstamped'
const PV = A.promptVersion || 1
const P = (n) => `docs/ExcelParity/Prompts/${n}.md`
const RULES = [
  `Repo: ${A.repoRoot} (branch fix/account-summary). Prompt version ${PV}.`,
  'Read docs/ExcelParity/Prompts/_preamble.md first, then docs/ExcelParity/Contract.md.',
  'Per-controller detail lives in docs/ExcelParity/manifest.json under controllers[] (match by "controller").',
  'Hard rules: never git commit/push/merge/checkout <branch>/stash/reset; never write docs/ExcelParity/Status.md; never print connection strings.',
  'Your final message is machine-read: return ONLY the structured result.',
].join('\n')
const J = (v) => JSON.stringify(v)

const BOOL = { type: 'boolean' }, STR = { type: 'string' }, STRS = { type: 'array', items: STR }
const RULES5 = { type: 'object', required: ['title', 'date', 'fromTo', 'columnsExact', 'footer'], properties: { title: BOOL, date: BOOL, fromTo: BOOL, columnsExact: BOOL, footer: BOOL } }
const SHARED = { type: 'array', items: { type: 'object', required: ['file', 'change', 'reason'], properties: { file: STR, change: STR, reason: STR } } }
const BATCH_REPORT = {
  type: 'object', required: ['batchId', 'reports', 'sharedEditRequests', 'concerns'],
  properties: {
    batchId: STR, concerns: STRS, sharedEditRequests: SHARED,
    reports: { type: 'array', items: { type: 'object', required: ['controller', 'status', 'rulesVerified', 'edits', 'notes'],
      properties: { controller: STR, status: { type: 'string', enum: ['ok', 'already-done', 'needs-shared-edit', 'blocked', 'reverted'] }, rulesVerified: RULES5, edits: STRS, notes: STR } } },
  },
}
const BATCH_VERDICT = {
  type: 'object', required: ['batchId', 'verdicts'],
  properties: { batchId: STR, verdicts: { type: 'array', items: { type: 'object', required: ['controller', 'refuted', 'refutedRules', 'evidence'],
    properties: { controller: STR, refuted: BOOL, refutedRules: STRS, evidence: STRS, suggestedFix: STR } } } },
}
const GATE = {
  type: 'object', required: ['ok', 'buildOk', 'dbAvailable', 'results', 'failures', 'summary'],
  properties: { ok: BOOL, buildOk: BOOL, dbAvailable: BOOL, summary: STR,
    results: { type: 'array', items: { type: 'object', required: ['controller', 'passed'], properties: { controller: STR, passed: BOOL, header: BOOL, columns: BOOL, footer: { type: 'string', enum: ['ok', 'mismatch', 'unverified-nodb', 'n/a'] }, error: STR } } },
    failures: { type: 'array', items: { type: 'object', required: ['file', 'error'], properties: { file: STR, error: STR, controller: STR } } } },
}
const REPORT = { type: 'object', required: ['controller', 'status', 'rulesVerified', 'edits', 'sharedEditRequests', 'concerns', 'notes'],
  properties: { controller: STR, status: { type: 'string', enum: ['ok', 'needs-shared-edit', 'blocked', 'reverted'] }, rulesVerified: RULES5, edits: STRS, concerns: STRS, notes: STR, sharedEditRequests: SHARED } }
const APPLIED = { type: 'object', required: ['applied', 'rejected', 'touchedFrontend'], properties: { applied: STRS, touchedFrontend: BOOL, rejected: { type: 'array', items: { type: 'object', required: ['request', 'reason'], properties: { request: STR, reason: STR } } } } }
const PAGE = { type: 'object', required: ['page', 'status', 'edits', 'concerns'], properties: { page: STR, status: { type: 'string', enum: ['ok', 'blocked'] }, edits: STRS, concerns: STRS } }
const CRITIC = { type: 'object', required: ['missing', 'notGreen'], properties: { missing: STRS, notGreen: { type: 'array', items: { type: 'object', required: ['controller', 'why'], properties: { controller: STR, why: STR } } } } }
const E2E = { type: 'object', required: ['ran', 'passed', 'failed', 'notes'], properties: { ran: BOOL, passed: STRS, failed: STRS, notes: STR } }
const WRITTEN = { type: 'object', required: ['path', 'green', 'total'], properties: { path: STR, green: { type: 'number' }, total: { type: 'number' } } }

const KNOWN = A.knownFailureMatches || []
const isKnownFailure = (f) => KNOWN.some((m) => `${f.file || ''} ${f.error || ''}`.includes(m))
const gateOk = (g) => {
  if (!g || !g.buildOk) return false
  if (!g.results.every((r) => r.passed)) return false
  const real = g.failures.filter((f) => !isKnownFailure(f))
  if (g.failures.length - real.length) log(`gate: waived ${g.failures.length - real.length} known pre-existing failure(s); ${real.length} real`)
  return real.length === 0
}
const filesOf = (b) => b.controllers.map((c) => `Backend/Controllers/Report/${c}.cs`)
  .concat(b.kind === 'composite' ? b.controllers.map((c) => `Backend.Tests/ExcelParity/${c}LayoutTests.cs`) : [])

const ALL = IMPL.flatMap((b) => b.controllers)
const rows = new Map(ALL.map((c) => [c, { impl: null, gate: null, skeptic: null, repairs: 0, batch: null }]))

// ---------------- Phase 1: every controller, batched, no builds ----------------
phase('Controllers')
log(`${ALL.length} controllers in ${IMPL.length} batches (${IMPL.filter((b) => b.kind === 'aggregate').length} aggregate, ${IMPL.filter((b) => b.kind === 'composite').length} composite, ${IMPL.filter((b) => b.kind === 'verify').length} verify); no gate until every batch is done`)
const implResults = await pipeline(IMPL, (_, b) => agent(
  `${RULES}\nRead ${P('implement-batch')} and ${P('footer-check')} in full, then work these controllers in order:\n${J(b)}\nYou may edit ONLY: ${filesOf(b).join(', ')}. Everything else goes into sharedEditRequests. Do NOT run dotnet or npm.`,
  { label: `impl ${b.id} (${b.controllers.length})`, phase: 'Controllers', schema: BATCH_REPORT }))
implResults.forEach((r, idx) => {
  const b = IMPL[idx]
  b.controllers.forEach((c) => { rows.get(c).batch = b.id })
  if (!r) { log(`batch ${b.id} died; its ${b.controllers.length} controller(s) are unverified`); return }
  r.reports.forEach((rep) => { if (rows.has(rep.controller)) rows.get(rep.controller).impl = rep })
})
const doneCount = ALL.filter((c) => rows.get(c).impl).length
log(`controllers reported: ${doneCount}/${ALL.length}`)
const shared = implResults.filter(Boolean).flatMap((r) => r.sharedEditRequests.map((s) => ({ batchId: r.batchId, ...s })))
if (shared.length) {
  const ap = await agent(`${RULES}\nRead ${P('shared-applier')}. Apply these shared-file edit requests one at a time; reject anything outside the allowlist or that conflicts:\n${J(shared)}`, { label: `shared edits (${shared.length})`, phase: 'Controllers', schema: APPLIED })
  if (ap) ap.rejected.forEach((r) => log(`rejected shared edit: ${r.reason}`))
}

// ---------------- Phase 2: one build gate, then targeted repairs ----------------
phase('Build gate')
let gate = null
for (let c = 0; c <= MAX_REPAIRS; c++) {
  gate = await agent(`${RULES}\nRead ${P('gate-final')} and ${P('_gate-common')}. Build gate cycle ${c}: build the solution and run the FULL Backend.Tests suite. Return one results entry per controller in docs/ExcelParity/manifest.json (160).`,
    { label: `build gate #${c}`, phase: 'Build gate', schema: GATE, effort: 'high' })
  if (!gate) { log('build gate agent died'); break }
  if (gateOk(gate)) break
  if (c === MAX_REPAIRS) { log(`build gate still red after ${MAX_REPAIRS} repair cycles`); break }
  const bad = ALL.filter((x) => gate.failures.some((f) => (f.controller || f.file || '').includes(x)) || gate.results.some((r) => r.controller === x && !r.passed))
  if (!bad.length) {
    await agent(`${RULES}\nRead ${P('core-repair')}. The gate is red outside any controller. Fix ONLY these:\n${J(gate.failures)}`, { label: `shared repair #${c}`, phase: 'Build gate', schema: REPORT, effort: 'high' })
  } else {
    log(`repair cycle ${c}: ${bad.length} controller(s)`)
    await pipeline(bad, (_, x) => { rows.get(x).repairs++; return agent(
      `${RULES}\nRead ${P('repair')}. Repair cycle ${c} for ${x}.\nGate evidence: ${J({ buildOk: gate.buildOk, failures: gate.failures.filter((f) => (f.controller || f.file || '').includes(x)), result: gate.results.find((r) => r.controller === x) || null })}\nYou may edit ONLY: Backend/Controllers/Report/${x}.cs. If you cannot make it compile, git checkout -- that file and return status "reverted".`,
      { label: `repair ${x} #${c}`, phase: 'Build gate', schema: REPORT }) })
  }
}
if (gate) ALL.forEach((x) => { const r = gate.results.find((y) => y.controller === x); if (r) rows.get(x).gate = r })
if (!gateOk(gate)) log('WARNING: proceeding to verification with a red build gate — Status.md will show it')

// ---------------- Phase 3: batched adversarial verification ----------------
phase('Verify')
const refuted = []
if (SKEPT.length) {
  log(`${SKEPT.reduce((n, b) => n + b.controllers.length, 0)} judgment-heavy reports in ${SKEPT.length} skeptic batches; the other ${ALL.length - SKEPT.reduce((n, b) => n + b.controllers.length, 0)} rely on the contract test alone (Contract.md §14)`)
  const verdicts = await pipeline(SKEPT, (_, b) => agent(`${RULES}\nRead ${P('skeptic-batch')} and ${P('skeptic')}, then judge these controllers:\n${J(b)}`, { label: `skeptic ${b.id} (${b.controllers.length})`, phase: 'Verify', schema: BATCH_VERDICT }))
  verdicts.filter(Boolean).forEach((v) => v.verdicts.forEach((x) => {
    if (!rows.has(x.controller)) return
    rows.get(x.controller).skeptic = x
    if (x.refuted) refuted.push(x.controller)
  }))
  log(`skeptics refuted ${refuted.length} report(s)`)
  if (refuted.length) {
    await pipeline(refuted, (_, x) => agent(`${RULES}\nRead ${P('repair')}. Skeptic-driven repair for ${x}.\nVerdict: ${J(rows.get(x).skeptic)}\nYou may edit ONLY: Backend/Controllers/Report/${x}.cs. If the skeptic is wrong, change nothing and say so in notes.`, { label: `skeptic repair ${x}`, phase: 'Verify', schema: REPORT })
      .then((r) => { if (r && r.status === 'ok') rows.get(x).skepticRepaired = true; return r }))
  }
}

// ---------------- Phase 4: bespoke frontend pages ----------------
phase('Bespoke pages')
let pageResults = [], feGate = null
if (PAGES.length) {
  pageResults = await pipeline(PAGES, (_, page) => agent(`${RULES}\nRead ${P('bespoke-page')}. Bring this page onto the queued Excel-spec flow. Edit ONLY Frontend/src/Report/Page/${page}.tsx, Frontend/src/Report/excel/bespoke/* (your module + your entry in index.ts) and, for MemberRegistrationReport only, its entry in Frontend/src/Report/config/reportConfigs.ts. Do NOT run npm.\nPage: ${page}`, { label: `page ${page}`, phase: 'Bespoke pages', schema: PAGE }))
  for (let c = 0; c <= MAX_REPAIRS; c++) {
    feGate = await agent(`${RULES}\nRead ${P('gate-frontend')} and ${P('_gate-common')}. Frontend gate cycle ${c}. Pages: ${J(PAGES)}`, { label: `frontend gate #${c}`, phase: 'Bespoke pages', schema: GATE })
    if (!feGate || gateOk(feGate) || c === MAX_REPAIRS) break
    const bad = PAGES.filter((p) => feGate.failures.some((f) => f.file.includes(p)))
    if (!bad.length) await agent(`${RULES}\nRead ${P('core-repair')}. Frontend gate red outside any page; fix ONLY these:\n${J(feGate.failures)}`, { label: `frontend shared repair #${c}`, phase: 'Bespoke pages', schema: REPORT })
    else await pipeline(bad, (_, p) => agent(`${RULES}\nRead ${P('repair')}. Frontend repair #${c}; edit ONLY Frontend/src/Report/Page/${p}.tsx and its bespoke module:\n${J(feGate.failures.filter((f) => f.file.includes(p)))}`, { label: `page repair ${p} #${c}`, phase: 'Bespoke pages', schema: PAGE }))
  }
  log(`bespoke pages: ${PAGES.length}; frontend gate ok=${gateOk(feGate)}`)
}

// ---------------- Phase 5: final gate, e2e, status ----------------
phase('Final gate + status')
let finalGate = null
for (let c = 0; c <= MAX_REPAIRS; c++) {
  finalGate = await agent(`${RULES}\nRead ${P('gate-final')} and ${P('_gate-common')}. FINAL gate cycle ${c}: full solution build, full Backend.Tests suite, frontend build/lint/vitest with fixture no-diff. One results entry per manifest controller.`, { label: `final gate #${c}`, phase: 'Final gate + status', schema: GATE, effort: 'high' })
  if (!finalGate || gateOk(finalGate) || c === MAX_REPAIRS) break
  const bad = ALL.filter((x) => finalGate.failures.some((f) => (f.controller || f.file || '').includes(x)) || finalGate.results.some((r) => r.controller === x && !r.passed))
  if (!bad.length) await agent(`${RULES}\nRead ${P('core-repair')}. Final gate red outside any controller; fix ONLY these:\n${J(finalGate.failures)}`, { label: `final shared repair #${c}`, phase: 'Final gate + status', schema: REPORT, effort: 'high' })
  else await pipeline(bad, (_, x) => agent(`${RULES}\nRead ${P('repair')}. Final repair #${c} for ${x}.\nEvidence: ${J(finalGate.failures.filter((f) => (f.controller || f.file || '').includes(x)))}\nEdit ONLY Backend/Controllers/Report/${x}.cs.`, { label: `final repair ${x} #${c}`, phase: 'Final gate + status', schema: REPORT }))
}
if (finalGate) ALL.forEach((x) => { const r = finalGate.results.find((y) => y.controller === x); if (r) rows.get(x).gate = r })
const e2e = A.e2e !== false && gateOk(finalGate)
  ? await agent(`${RULES}\nRead ${P('e2e-smoke')}. Generate real exports for ${J(A.e2eControllers || [])} against the dev DB (read TradeNetDBTest from Backend/appsettings.json into env, never print it; use 2025 date ranges), verify each .xlsx with openpyxl, and delete the jobs you created.`, { label: 'e2e smoke', phase: 'Final gate + status', schema: E2E, effort: 'high' })
  : (log('e2e smoke skipped (final gate not green or disabled)'), null)

const statusRows = ALL.map((x) => {
  const r = rows.get(x), g = r.gate, s = r.skeptic, i = r.impl
  const green = !!(g && g.passed) && (!s || !s.refuted || !!r.skepticRepaired) && (!i || (i.status !== 'blocked' && i.status !== 'reverted'))
  return { controller: x, batch: r.batch, implStatus: i ? i.status : 'no-report',
    headerOk: !!(g && g.header), columnsOk: !!(g && g.columns), footer: g ? g.footer : 'n/a', gatePassed: !!(g && g.passed),
    repairs: r.repairs, skepticScope: s ? 'run' : (SKEPT.some((b) => b.controllers.includes(x)) ? 'died' : 'not-required'),
    skepticRefuted: !!(s && s.refuted), skepticRules: s ? s.refutedRules : [], green,
    notes: [i && i.notes, s && s.refuted && s.evidence.join('; ')].filter(Boolean).join(' | ') }
})
const critic = await agent(`${RULES}\nRead ${P('critic')}. Which manifest controllers lack a green row and why? Which are missing from these rows entirely?\n${J(statusRows)}`, { label: 'completeness critic', phase: 'Final gate + status', schema: CRITIC })
const written = await agent(`${RULES}\nRead ${P('status-writer')}. You are the ONLY writer of docs/ExcelParity/Status.md. Run stamp: ${RUN_STAMP}. Write it from this data only:\n${J({ rows: statusRows, critic, e2e, finalGate: finalGate && { ok: finalGate.ok, dbAvailable: finalGate.dbAvailable, summary: finalGate.summary }, frontendGate: feGate && { ok: feGate.ok, summary: feGate.summary }, pages: pageResults.filter(Boolean) })}`, { label: 'status writer', phase: 'Final gate + status', schema: WRITTEN })
const green = statusRows.filter((r) => r.green).length
log(`${green}/${ALL.length} green`)
return { green, total: ALL.length, controllersReported: doneCount, notGreen: critic ? critic.notGreen : [],
  statusPath: written && written.path, buildGateOk: gateOk(gate), finalGateOk: gateOk(finalGate), e2e,
  mergeable: gateOk(finalGate) && (!PAGES.length || gateOk(feGate)) && (!e2e || !e2e.failed.length) && green === ALL.length }

export const meta = {
  name: 'excel-parity-verify-and-finish',
  description: 'Phase 1 (all 160 controllers) is done: gate it with the DB-skip filter, verify adversarially, finish the bespoke pages, write Status.md',
  phases: [
    { title: 'Build gate', detail: 'build + Backend.Tests with the mandatory DB-skip filter, then targeted repairs' },
    { title: 'Verify', detail: 'batched skeptics over the 98 judgment-heavy reports, then skeptic-driven repairs' },
    { title: 'Bespoke pages', detail: 'one agent per bespoke frontend page, then the frontend gate' },
    { title: 'Final gate + status', detail: 'final gate, completeness critic, Status.md with the MERGEABLE verdict' },
  ],
}

const A = args || {}
if (!A.repoRoot) throw new Error('args.repoRoot is required')
const PAGES = A.pages || []
const SKEPTIC_IDS = Array.from({ length: A.skepticBatches || 20 }, (_, i) => `skeptic-${String(i + 1).padStart(2, '0')}`)
const MAX_REPAIRS = A.maxRepairs || 3
const RUN_STAMP = A.runStamp || 'unstamped'
const P = (n) => `docs/ExcelParity/Prompts/${n}.md`
const RULES = [
  `Repo: ${A.repoRoot} (branch fix/account-summary). Prompt version ${A.promptVersion || 1}.`,
  'Read docs/ExcelParity/Prompts/_preamble.md first, then docs/ExcelParity/Contract.md.',
  'Per-controller detail lives in docs/ExcelParity/manifest.json under controllers[] (match by "controller").',
  'Phase 1 already changed all 160 controllers; docs/ExcelParity/impl-results.json records what each agent claimed.',
  'Hard rules: never git commit/push/merge/checkout <branch>/stash/reset; never write docs/ExcelParity/Status.md; never print connection strings.',
  'Your final message is machine-read: return ONLY the structured result.',
].join('\n')
const J = (v) => JSON.stringify(v)

const BOOL = { type: 'boolean' }, STR = { type: 'string' }, STRS = { type: 'array', items: STR }
const RULES5 = { type: 'object', required: ['title', 'date', 'fromTo', 'columnsExact', 'footer'], properties: { title: BOOL, date: BOOL, fromTo: BOOL, columnsExact: BOOL, footer: BOOL } }
const SHARED = { type: 'array', items: { type: 'object', required: ['file', 'change', 'reason'], properties: { file: STR, change: STR, reason: STR } } }
const GATE = {
  type: 'object', required: ['ok', 'buildOk', 'dbAvailable', 'results', 'failures', 'summary'],
  properties: { ok: BOOL, buildOk: BOOL, dbAvailable: BOOL, summary: STR,
    results: { type: 'array', items: { type: 'object', required: ['controller', 'passed'], properties: { controller: STR, passed: BOOL, header: BOOL, columns: BOOL, footer: { type: 'string', enum: ['ok', 'mismatch', 'unverified-nodb', 'n/a'] }, error: STR } } },
    failures: { type: 'array', items: { type: 'object', required: ['file', 'error'], properties: { file: STR, error: STR, controller: STR } } } },
}
const BATCH_VERDICT = {
  type: 'object', required: ['batchId', 'verdicts'],
  properties: { batchId: STR, verdicts: { type: 'array', items: { type: 'object', required: ['controller', 'refuted', 'refutedRules', 'evidence'],
    properties: { controller: STR, refuted: BOOL, refutedRules: STRS, evidence: STRS, suggestedFix: STR } } } },
}
const REPORT = { type: 'object', required: ['controller', 'status', 'rulesVerified', 'edits', 'sharedEditRequests', 'concerns', 'notes'],
  properties: { controller: STR, status: { type: 'string', enum: ['ok', 'needs-shared-edit', 'blocked', 'reverted'] }, rulesVerified: RULES5, edits: STRS, concerns: STRS, notes: STR, sharedEditRequests: SHARED } }
const PAGE = { type: 'object', required: ['page', 'status', 'edits', 'concerns'], properties: { page: STR, status: { type: 'string', enum: ['ok', 'blocked'] }, edits: STRS, concerns: STRS } }
const CRITIC = { type: 'object', required: ['missing', 'notGreen'], properties: { missing: STRS, notGreen: { type: 'array', items: { type: 'object', required: ['controller', 'why'], properties: { controller: STR, why: STR } } } } }
const WRITTEN = { type: 'object', required: ['path', 'green', 'total'], properties: { path: STR, green: { type: 'number' }, total: { type: 'number' } } }

const KNOWN = A.knownFailureMatches || []
const isKnown = (f) => KNOWN.some((m) => `${f.file || ''} ${f.error || ''}`.includes(m))
const gateOk = (g) => {
  if (!g || !g.buildOk) return false
  if (!g.results.length || !g.results.every((r) => r.passed)) return false
  const real = g.failures.filter((f) => !isKnown(f))
  if (g.failures.length - real.length) log(`gate: waived ${g.failures.length - real.length} known pre-existing failure(s); ${real.length} real`)
  return real.length === 0
}
const DBSKIP = 'Run Backend.Tests ONLY with the mandatory DB-skip filter from _gate-common.md section 3a — never the unfiltered suite (that costs 14 minutes and returns ~690 pre-login-timeout failures). Expect ~1538 tests in seconds and exactly the 8 pre-existing failures recorded in known-failures.json under "pre-existing-request-factory-and-filter-contract". Rebuild Backend.Tests after ANY fixture change or the contract theory reads stale copies out of bin/. Footer is unverified-nodb for every report with totals; say so in summary.'

// ---------------- Phase 1: gate what Phase 1 produced ----------------
phase('Build gate')
let gate = null
for (let c = 0; c <= MAX_REPAIRS; c++) {
  gate = await agent(`${RULES}\nRead ${P('gate-final')} and ${P('_gate-common')}. Build gate cycle ${c}. ${DBSKIP}\nReturn one results entry per controller in docs/ExcelParity/manifest.json (160).`,
    { label: `build gate #${c}`, phase: 'Build gate', schema: GATE, effort: 'high' })
  if (!gate) { log('build gate agent died'); break }
  if (gateOk(gate)) break
  if (c === MAX_REPAIRS) { log(`build gate still red after ${MAX_REPAIRS} repair cycles`); break }
  const bad = [...new Set(gate.results.filter((r) => !r.passed).map((r) => r.controller)
    .concat(gate.failures.filter((f) => !isKnown(f) && f.controller).map((f) => f.controller)))]
  if (!bad.length) {
    await agent(`${RULES}\nRead ${P('core-repair')}. The gate is red but no single controller owns the failure — likely a shared file. Fix ONLY these, no feature work:\n${J(gate.failures.filter((f) => !isKnown(f)))}`,
      { label: `shared repair #${c}`, phase: 'Build gate', schema: REPORT, effort: 'high' })
  } else {
    log(`build gate #${c} red for ${bad.length} controller(s)`)
    await pipeline(bad, (_, x) => agent(`${RULES}\nRead ${P('repair')}. Repair cycle ${c} for ${x}.\nGate evidence: ${J({ buildOk: gate.buildOk, failures: gate.failures.filter((f) => (f.controller || f.file || '').includes(x)), result: gate.results.find((r) => r.controller === x) || null })}\nYou may edit ONLY: Backend/Controllers/Report/${x}.cs. If you cannot make it compile, git checkout -- that file and return status "reverted".`,
      { label: `repair ${x} #${c}`, phase: 'Build gate', schema: REPORT }))
  }
}
const gateRows = new Map((gate ? gate.results : []).map((r) => [r.controller, r]))
log(`build gate ok=${gateOk(gate)}; ${gateRows.size} controller results; dbAvailable=${gate ? gate.dbAvailable : 'unknown'}`)

// ---------------- Phase 2: adversarial verification ----------------
phase('Verify')
const skeptics = new Map()
const refuted = []
log(`${SKEPTIC_IDS.length} skeptic batches over the 98 judgment-heavy reports (list: docs/ExcelParity/verify-batches.json); the other 62 rely on the contract test alone (Contract.md section 14)`)
const verdicts = await pipeline(SKEPTIC_IDS, (_, id) => agent(`${RULES}\nRead ${P('skeptic-batch')} and ${P('skeptic')}. Your batchId is "${id}" — read docs/ExcelParity/verify-batches.json and judge exactly the controllers of that entry.`,
  { label: `skeptic ${id}`, phase: 'Verify', schema: BATCH_VERDICT }))
verdicts.filter(Boolean).forEach((v) => v.verdicts.forEach((x) => {
  skeptics.set(x.controller, x)
  if (x.refuted) refuted.push(x.controller)
}))
log(`skeptics returned ${skeptics.size}/98 verdicts; refuted ${refuted.length}`)
const skepticRepairs = new Map()
if (refuted.length) {
  await pipeline([...new Set(refuted)], (_, x) => agent(`${RULES}\nRead ${P('repair')}. Skeptic-driven repair for ${x}.\nVerdict: ${J(skeptics.get(x))}\nYou may edit ONLY: Backend/Controllers/Report/${x}.cs. If the skeptic is wrong, change nothing and explain why in notes.`,
    { label: `skeptic repair ${x}`, phase: 'Verify', schema: REPORT }).then((r) => { if (r) skepticRepairs.set(x, r); return r }))
  log(`skeptic repairs applied: ${[...skepticRepairs.values()].filter((r) => r.status === 'ok').length}/${new Set(refuted).size}`)
}

// ---------------- Phase 3: bespoke frontend pages ----------------
phase('Bespoke pages')
let pageResults = [], feGate = null
if (PAGES.length) {
  pageResults = await pipeline(PAGES, (_, page) => agent(`${RULES}\nRead ${P('bespoke-page')}. Bring this page onto the queued Excel-spec flow. Edit ONLY Frontend/src/Report/Page/${page}.tsx, Frontend/src/Report/excel/bespoke/* (your module + your entry in index.ts) and, for MemberRegistrationReport only, its entry in Frontend/src/Report/config/reportConfigs.ts. Do NOT run npm.\nPage: ${page}`,
    { label: `page ${page}`, phase: 'Bespoke pages', schema: PAGE }))
  for (let c = 0; c <= MAX_REPAIRS; c++) {
    feGate = await agent(`${RULES}\nRead ${P('gate-frontend')} and ${P('_gate-common')}. Frontend gate cycle ${c}. Pages: ${J(PAGES)}\nAfter regenerating fixtures you MUST run dotnet build Backend.Tests/Backend.Tests.csproj before any --no-build test run. ${DBSKIP}`,
      { label: `frontend gate #${c}`, phase: 'Bespoke pages', schema: GATE })
    if (!feGate || gateOk(feGate) || c === MAX_REPAIRS) break
    const bad = PAGES.filter((p) => feGate.failures.some((f) => !isKnown(f) && (f.file || '').includes(p)))
    if (!bad.length) await agent(`${RULES}\nRead ${P('core-repair')}. Frontend gate red outside any bespoke page; fix ONLY these:\n${J(feGate.failures.filter((f) => !isKnown(f)))}`, { label: `frontend shared repair #${c}`, phase: 'Bespoke pages', schema: REPORT })
    else await pipeline(bad, (_, p) => agent(`${RULES}\nRead ${P('repair')}. Frontend repair #${c}; edit ONLY Frontend/src/Report/Page/${p}.tsx and its bespoke module:\n${J(feGate.failures.filter((f) => (f.file || '').includes(p)))}`, { label: `page repair ${p} #${c}`, phase: 'Bespoke pages', schema: PAGE }))
  }
  log(`bespoke pages: ${PAGES.length}; frontend gate ok=${gateOk(feGate)}`)
} else log('no bespoke pages in args')

// ---------------- Phase 4: final gate, critic, status ----------------
phase('Final gate + status')
let finalGate = null
for (let c = 0; c <= MAX_REPAIRS; c++) {
  finalGate = await agent(`${RULES}\nRead ${P('gate-final')} and ${P('_gate-common')}. FINAL gate cycle ${c}: full solution build, Backend.Tests, frontend build/lint/vitest with fixture no-diff. ${DBSKIP}\nOne results entry per manifest controller (160).`,
    { label: `final gate #${c}`, phase: 'Final gate + status', schema: GATE, effort: 'high' })
  if (!finalGate || gateOk(finalGate) || c === MAX_REPAIRS) break
  const bad = [...new Set(finalGate.results.filter((r) => !r.passed).map((r) => r.controller)
    .concat(finalGate.failures.filter((f) => !isKnown(f) && f.controller).map((f) => f.controller)))]
  if (!bad.length) await agent(`${RULES}\nRead ${P('core-repair')}. Final gate red outside any controller; fix ONLY these:\n${J(finalGate.failures.filter((f) => !isKnown(f)))}`, { label: `final shared repair #${c}`, phase: 'Final gate + status', schema: REPORT, effort: 'high' })
  else await pipeline(bad, (_, x) => agent(`${RULES}\nRead ${P('repair')}. Final repair #${c} for ${x}.\nEvidence: ${J(finalGate.failures.filter((f) => (f.controller || f.file || '').includes(x)))}\nEdit ONLY Backend/Controllers/Report/${x}.cs.`, { label: `final repair ${x} #${c}`, phase: 'Final gate + status', schema: REPORT }))
}
const finalRows = new Map((finalGate ? finalGate.results : []).map((r) => [r.controller, r]))
const CONTROLLERS = [...new Set([...finalRows.keys(), ...gateRows.keys()])].sort()
const statusRows = CONTROLLERS.map((x) => {
  const g = finalRows.get(x) || gateRows.get(x) || null
  const s = skeptics.get(x) || null
  const rep = skepticRepairs.get(x) || null
  return { controller: x, gatePassed: !!(g && g.passed), headerOk: !!(g && g.header), columnsOk: !!(g && g.columns),
    footer: g ? g.footer : 'n/a', gateError: g && g.error ? String(g.error).slice(0, 200) : '',
    skeptic: s ? (s.refuted ? (rep && rep.status === 'ok' ? `refuted-repaired(${s.refutedRules.join(',')})` : `refuted(${s.refutedRules.join(',')})`) : 'ok') : 'n/a',
    skepticEvidence: s && s.refuted ? s.evidence.slice(0, 3) : [],
    green: !!(g && g.passed) && (!s || !s.refuted || !!(rep && rep.status === 'ok')) }
})
const green = statusRows.filter((r) => r.green).length
log(`${green}/${statusRows.length} green`)
const critic = await agent(`${RULES}\nRead ${P('critic')}. Which manifest controllers lack a green row and why? Which are missing from these rows entirely? Note that footer is unverified-nodb for every report with totals on this machine, and that e2e was NOT run (no DB).\n${J(statusRows.filter((r) => !r.green || r.footer === 'unverified-nodb').slice(0, 120))}\nGreen count: ${green}/${statusRows.length}. Rows present: ${statusRows.length}.`,
  { label: 'completeness critic', phase: 'Final gate + status', schema: CRITIC })
const written = await agent(`${RULES}\nRead ${P('status-writer')}. You are the ONLY writer of docs/ExcelParity/Status.md. Run stamp: ${RUN_STAMP}. Read docs/ExcelParity/impl-results.json and docs/ExcelParity/verify-batches.json for the per-controller Phase-1 state and verification scope, and write the file from those plus this data:\n${J({ rows: statusRows, critic, e2e: null, e2eNote: 'e2e spreadsheet smoke NOT run: the report DB is unreachable from this machine. Still required before merge.', finalGate: finalGate && { ok: finalGate.ok, dbAvailable: finalGate.dbAvailable, summary: finalGate.summary }, buildGate: gate && { ok: gate.ok, summary: gate.summary }, frontendGate: feGate && { ok: feGate.ok, summary: feGate.summary }, pages: pageResults.filter(Boolean), green, total: statusRows.length })}`,
  { label: 'status writer', phase: 'Final gate + status', schema: WRITTEN, effort: 'high' })
return { green, total: statusRows.length, statusPath: written && written.path,
  buildGateOk: gateOk(gate), frontendGateOk: PAGES.length ? gateOk(feGate) : null, finalGateOk: gateOk(finalGate),
  skepticsRefuted: [...new Set(refuted)].length, notGreen: critic ? critic.notGreen.map((n) => n.controller) : [],
  mergeable: false, mergeableBlockers: ['footer parity unverified (no DB)', 'e2e spreadsheet check not run (no DB)'] }

export const meta = {
  name: 'excel-parity-all-reports',
  description: 'Excel export parity for all 160 streaming report controllers: core, gated per-report waves, bespoke pages, final gate + Status.md',
  phases: [
    { title: 'Core', detail: 'backend + frontend core agents in parallel, 4-lens adversarial review, build/test/fixture gate, bounded repair' },
    { title: 'Fan-out', detail: 'waves of controllers: group-specific implement (no builds) -> serialized shared edits -> one build+contract-test gate -> repair loop -> detached skeptics' },
    { title: 'Bespoke pages', detail: 'one agent per bespoke frontend page, then npm build/lint/vitest fixture-drift gate' },
    { title: 'Final gate + status', detail: 'skeptic-driven repairs, full dotnet+npm gate, e2e smoke (openpyxl), completeness critic, Status.md writer' },
  ],
}

const A = args || {}
if (!A.repoRoot || !Array.isArray(A.items)) throw new Error('args.repoRoot and args.items are required')
const ITEMS = A.items
  .map((t) => Array.isArray(t)
    ? { controller: t[0], group: t[1], hasColumnTotals: !!t[2], hasCurrencyTotals: !!t[3], bespokePage: t[4], excelPagePattern: t[5] }
    : t)
  .sort((x, y) => (x.controller < y.controller ? -1 : x.controller > y.controller ? 1 : 0))
const WAVE = A.waveSize || 20
const MAX_REPAIRS = A.maxRepairs || 3
const SYSTEMIC = A.systemicFailRatio || 0.3
const RUN_STAMP = A.runStamp || 'unstamped'
const PV = A.promptVersion || 1
const P = (n) => `docs/ExcelParity/Prompts/${n}.md`
const RULES = [
  `Repo: ${A.repoRoot} (branch fix/account-summary). Prompt version ${PV}.`,
  `Read docs/ExcelParity/Prompts/_preamble.md first, then docs/ExcelParity/Contract.md.`,
  `Your full manifest item is in docs/ExcelParity/manifest.json under controllers[] (match by "controller"); the summary below is only an index.`,
  'Hard rules: never git commit/push/merge/checkout <branch>/stash/reset; never write docs/ExcelParity/Status.md; never print connection strings.',
  'Your final message is machine-read: return ONLY the structured result.',
].join('\n')
const J = (v) => JSON.stringify(v)

const BOOL = { type: 'boolean' }
const STR = { type: 'string' }
const STRS = { type: 'array', items: STR }
const RULES5 = { type: 'object', required: ['title', 'date', 'fromTo', 'columnsExact', 'footer'], properties: { title: BOOL, date: BOOL, fromTo: BOOL, columnsExact: BOOL, footer: BOOL } }
const REPORT = {
  type: 'object', required: ['controller', 'status', 'rulesVerified', 'edits', 'sharedEditRequests', 'concerns', 'notes'],
  properties: {
    controller: STR, status: { type: 'string', enum: ['ok', 'needs-shared-edit', 'blocked', 'reverted'] }, rulesVerified: RULES5,
    edits: STRS, concerns: STRS, notes: STR,
    sharedEditRequests: { type: 'array', items: { type: 'object', required: ['file', 'change', 'reason'], properties: { file: STR, change: STR, reason: STR } } },
  },
}
const GATE = {
  type: 'object', required: ['ok', 'buildOk', 'dbAvailable', 'results', 'failures', 'summary'],
  properties: {
    ok: BOOL, buildOk: BOOL, dbAvailable: BOOL, summary: STR,
    results: { type: 'array', items: { type: 'object', required: ['controller', 'passed'], properties: { controller: STR, passed: BOOL, header: BOOL, columns: BOOL, footer: { type: 'string', enum: ['ok', 'mismatch', 'unverified-nodb', 'n/a'] }, error: STR } } },
    failures: { type: 'array', items: { type: 'object', required: ['file', 'error'], properties: { file: STR, error: STR, controller: STR } } },
  },
}
const VERDICT = { type: 'object', required: ['controller', 'refuted', 'refutedRules', 'evidence'], properties: { controller: STR, refuted: BOOL, refutedRules: STRS, evidence: STRS, suggestedFix: STR } }
const APPLIED = { type: 'object', required: ['applied', 'rejected', 'touchedFrontend'], properties: { applied: STRS, touchedFrontend: BOOL, rejected: { type: 'array', items: { type: 'object', required: ['request', 'reason'], properties: { request: STR, reason: STR } } } } }
const PAGE = { type: 'object', required: ['page', 'status', 'edits', 'concerns'], properties: { page: STR, status: { type: 'string', enum: ['ok', 'blocked'] }, edits: STRS, concerns: STRS } }
const CRITIC = { type: 'object', required: ['missing', 'notGreen'], properties: { missing: STRS, notGreen: { type: 'array', items: { type: 'object', required: ['controller', 'why'], properties: { controller: STR, why: STR } } } } }
const E2E = { type: 'object', required: ['ran', 'passed', 'failed', 'notes'], properties: { ran: BOOL, passed: STRS, failed: STRS, notes: STR } }
const WRITTEN = { type: 'object', required: ['path', 'green', 'total'], properties: { path: STR, green: { type: 'number' }, total: { type: 'number' } } }

const ownFiles = (it) => [`Backend/Controllers/Report/${it.controller}.cs`].concat(it.group === 'D' ? [`Backend.Tests/ExcelParity/${it.controller}LayoutTests.cs`] : [])
const implPromptFile = (g) => (g === 'C' ? 'implement-C' : g === 'D' ? 'implement-D' : 'implement-verify-only')
const implPrompt = (it) => `${RULES}\nRead ${P(implPromptFile(it.group))} and ${P('footer-check')} in full, then execute them for exactly one report:\n${J(it)}\nYou may edit ONLY: ${ownFiles(it).join(', ')}. Anything else (writer, builder, ReportQueryRequest, reportConfigs.ts, allowlist, shared tests) goes into sharedEditRequests. Do NOT run dotnet build/test or npm.`
const gatePrompt = (wave, tag, cycle, fe) => `${RULES}\nRead ${P('gate-wave')} and ${P('_gate-common')}. Gate ${tag} cycle ${cycle}. Controllers in this wave: ${J(wave.map((i) => i.controller))}. Frontend shared files were touched this wave: ${fe} (if true, regenerate fixtures first). Return one results entry per controller listed.`
const repairPrompt = (it, gate, cycle) => `${RULES}\nRead ${P('repair')}. Repair cycle ${cycle} for:\n${J(it)}\nGate evidence: ${J({ buildOk: gate.buildOk, failures: gate.failures.filter((f) => (f.controller || f.file || '').includes(it.controller)), result: gate.results.find((r) => r.controller === it.controller) || null })}\nYou may edit ONLY: ${ownFiles(it).join(', ')}. If you cannot make it compile, restore with git checkout -- <file> and return status "reverted".`
const skepticPrompt = (it, gate) => `${RULES}\nRead ${P('skeptic')}. You are a skeptic: try to REFUTE that this report's Excel export now matches the UI grid and the 5 rules. Default to refuted=true when uncertain.\n${J(it)}\nGate result for it: ${J((gate && gate.results.find((r) => r.controller === it.controller)) || null)}`
const failingIn = (list, gate) => list.filter((it) => gate.failures.some((f) => (f.controller || f.file || '').includes(it.controller)) || gate.results.some((r) => r.controller === it.controller && !r.passed))

phase('Core')
const core = await parallel([
  () => agent(`${RULES}\nRead ${P('core-backend')} and the Contract. Implement the backend core exactly as specified. Do not touch Frontend/. You may run dotnet build once at the end.`, { label: 'core backend', schema: REPORT, effort: 'high' }),
  () => agent(`${RULES}\nRead ${P('core-frontend')} and the Contract. Implement the frontend core + fixture generator. Do not touch Backend/ except Backend.Tests/Fixtures/ExcelSpecs/**. You may run npm run build, npm run lint and npx vitest run.`, { label: 'core frontend', schema: REPORT, effort: 'high' }),
])
if (core.some((c) => !c)) throw new Error('a core agent died; nothing to fan out')
const LENSES = ['header-and-dates', 'columns-and-render-rules', 'footer-totals-resolver', 'cache-version-and-enqueue']
const coreRefutations = (await parallel(LENSES.map((l) => () => agent(`${RULES}\nRead ${P('core-skeptic')} with lens "${l}". Try to REFUTE that the core implementation satisfies the Contract for that lens; cite file:line. Default to refuted=true when uncertain.`, { label: `core skeptic ${l}`, schema: VERDICT })))).filter(Boolean).filter((v) => v.refuted)
log(`core skeptics refuted ${coreRefutations.length}/${LENSES.length} lenses`)
let coreGate = null
for (let c = 0; c <= MAX_REPAIRS; c++) {
  if (c > 0 || coreRefutations.length) {
    await agent(`${RULES}\nRead ${P('core-repair')}. You are the ONLY agent editing shared files now. Fix everything listed, nothing else:\n${J({ gate: coreGate, skepticFindings: c === 0 ? coreRefutations : [] })}`, { label: `core repair #${c}`, schema: REPORT, effort: 'high' })
  }
  coreGate = await agent(`${RULES}\nRead ${P('gate-core')} and ${P('_gate-common')}. Core gate cycle ${c}. Expect ${A.expectedFixtures || 167} entries in Backend.Tests/Fixtures/ExcelSpecs/index.json.`, { label: `core gate #${c}`, schema: GATE })
  if (!coreGate) throw new Error('core gate agent died')
  if (coreGate.ok) break
}
if (!coreGate.ok) return { stoppedAt: 'core', mergeable: false, gate: coreGate }
log(`core green; dbAvailable=${coreGate.dbAvailable}`)

phase('Fan-out')
const rows = new Map(ITEMS.map((it) => [it.controller, { item: it, impl: null, gate: null, repairs: 0, skeptic: null, skepticRepair: null }]))
const waves = []
for (let i = 0; i < ITEMS.length; i += WAVE) waves.push(ITEMS.slice(i, i + WAVE))
log(`${ITEMS.length} controllers -> ${waves.length} waves of ${WAVE}; groups ${J(ITEMS.reduce((m, it) => ((m[it.group] = (m[it.group] || 0) + 1), m), {}))}`)
const skepticRuns = []
const refuted = []
let stopReason = null
for (let w = 0; w < waves.length && !stopReason; w++) {
  const wave = waves[w]
  const tag = `W${w + 1}`
  if (budget.total && budget.remaining() < 200000) { stopReason = `budget: ${budget.remaining()} tokens left before ${tag}`; break }
  const impl = await pipeline(wave, (_, it) => agent(implPrompt(it), { label: `${tag} impl ${it.controller}`, phase: 'Fan-out', schema: REPORT }))
  wave.forEach((it, i) => { rows.get(it.controller).impl = impl[i]; if (!impl[i]) log(`${tag}: implement agent died for ${it.controller} (still gated)`) })
  const shared = impl.filter(Boolean).flatMap((r) => r.sharedEditRequests.map((s) => ({ controller: r.controller, ...s })))
  let touchedFrontend = false
  if (shared.length) {
    const ap = await agent(`${RULES}\nRead ${P('shared-applier')}. Apply these shared-file edit requests one at a time (you are the only agent touching shared files now); reject anything outside the allowlist or that conflicts with another request:\n${J(shared)}`, { label: `${tag} shared edits (${shared.length})`, phase: 'Fan-out', schema: APPLIED })
    touchedFrontend = !!(ap && ap.touchedFrontend)
    if (ap) ap.rejected.forEach((r) => log(`${tag} rejected shared edit: ${r.reason}`))
  }
  let gate = null
  for (let c = 0; c <= MAX_REPAIRS; c++) {
    gate = await agent(gatePrompt(wave, tag, c, touchedFrontend), { label: `${tag} gate #${c}`, phase: 'Fan-out', schema: GATE })
    if (!gate) { stopReason = `${tag}: gate agent died`; break }
    if (gate.ok) break
    const failing = failingIn(wave, gate)
    if (c === 0 && failing.length > SYSTEMIC * wave.length) { stopReason = `${tag}: ${failing.length}/${wave.length} failed the first gate (systemic)`; break }
    if (c === MAX_REPAIRS) { log(`${tag}: still red after ${MAX_REPAIRS} repairs: ${J(failing.map((i) => i.controller))}`); break }
    if (!gate.buildOk && failing.length === 0) {
      await agent(`${RULES}\nRead ${P('core-repair')}. The build is broken outside any wave controller (likely a shared-file edit). Fix ONLY these errors, no feature work:\n${J(gate.failures)}`, { label: `${tag} shared repair #${c}`, phase: 'Fan-out', schema: REPORT })
    } else {
      failing.forEach((it) => rows.get(it.controller).repairs++)
      await pipeline(failing, (_, it) => agent(repairPrompt(it, gate, c), { label: `${tag} repair ${it.controller} #${c}`, phase: 'Fan-out', schema: REPORT }))
    }
  }
  wave.forEach((it) => { rows.get(it.controller).gate = (gate && gate.results.find((r) => r.controller === it.controller)) || null })
  if (stopReason) break
  skepticRuns.push(pipeline(wave, (_, it) => agent(skepticPrompt(it, gate), { label: `${tag} skeptic ${it.controller}`, phase: 'Fan-out', schema: VERDICT })
    .then((v) => { rows.get(it.controller).skeptic = v; if (v && v.refuted) refuted.push(it); return v })))
}
if (stopReason) log(`FAN-OUT STOPPED: ${stopReason}`)

phase('Bespoke pages')
const pages = [...new Set(ITEMS.filter((it) => it.bespokePage && it.excelPagePattern !== 'none').map((it) => it.bespokePage))].sort()
let pageResults = []
let feGate = null
if (!stopReason && pages.length) {
  pageResults = await pipeline(pages, (_, page) => agent(`${RULES}\nRead ${P('bespoke-page')}. Bring this bespoke page onto the queued Excel-spec flow. Edit ONLY Frontend/src/Report/Page/${page}.tsx, Frontend/src/Report/excel/bespoke/* (your own module + your entry in index.ts) and, for MemberRegistrationReport only, its entry in Frontend/src/Report/config/reportConfigs.ts. Do NOT run npm.\nPage: ${page}. Controllers behind it: ${J(ITEMS.filter((it) => it.bespokePage === page))}`, { label: `page ${page}`, phase: 'Bespoke pages', schema: PAGE }))
  for (let c = 0; c <= MAX_REPAIRS; c++) {
    feGate = await agent(`${RULES}\nRead ${P('gate-frontend')} and ${P('_gate-common')}. Frontend gate cycle ${c}. Pages: ${J(pages)}`, { label: `frontend gate #${c}`, phase: 'Bespoke pages', schema: GATE })
    if (!feGate || feGate.ok || c === MAX_REPAIRS) break
    const bad = pages.filter((p) => feGate.failures.some((f) => f.file.includes(p)))
    if (!bad.length) await agent(`${RULES}\nRead ${P('core-repair')}. Frontend gate red outside any bespoke page; fix ONLY these:\n${J(feGate.failures)}`, { label: `frontend shared repair #${c}`, phase: 'Bespoke pages', schema: REPORT })
    else await pipeline(bad, (_, page) => agent(`${RULES}\nRead ${P('repair')}. Frontend repair #${c}; edit ONLY Frontend/src/Report/Page/${page}.tsx and its bespoke spec module:\n${J(feGate.failures.filter((f) => f.file.includes(page)))}`, { label: `page repair ${page} #${c}`, phase: 'Bespoke pages', schema: PAGE }))
  }
  log(`bespoke pages: ${pages.length}; frontend gate ok=${!!(feGate && feGate.ok)}`)
} else log(`bespoke pages skipped (${stopReason ? 'fan-out stopped' : 'none'})`)

phase('Final gate + status')
await Promise.all(skepticRuns)
const refutedItems = [...new Map(refuted.map((it) => [it.controller, it])).values()].sort((x, y) => (x.controller < y.controller ? -1 : 1))
log(`skeptics refuted ${refutedItems.length}/${ITEMS.length}; ${ITEMS.filter((it) => !rows.get(it.controller).skeptic).length} skeptic verdicts missing`)
let finalGate = null
if (!stopReason) {
  if (refutedItems.length) {
    await pipeline(refutedItems, (_, it) => agent(`${RULES}\nRead ${P('repair')}. Skeptic-driven repair for:\n${J(it)}\nSkeptic verdict: ${J(rows.get(it.controller).skeptic)}\nYou may edit ONLY: ${ownFiles(it).join(', ')}. If the skeptic is wrong, change nothing and explain in notes.`, { label: `skeptic repair ${it.controller}`, phase: 'Final gate + status', schema: REPORT })
      .then((r) => { rows.get(it.controller).skepticRepair = r; return r }))
  }
  for (let c = 0; c <= MAX_REPAIRS; c++) {
    finalGate = await agent(`${RULES}\nRead ${P('gate-final')} and ${P('_gate-common')}. Final gate cycle ${c}. Return one results entry per controller in docs/ExcelParity/manifest.json.`, { label: `final gate #${c}`, phase: 'Final gate + status', schema: GATE, effort: 'high' })
    if (!finalGate || finalGate.ok || c === MAX_REPAIRS) break
    const bad = failingIn(ITEMS, finalGate)
    if (!bad.length) await agent(`${RULES}\nRead ${P('core-repair')}. Final gate red outside any controller; fix ONLY these:\n${J(finalGate.failures)}`, { label: `final shared repair #${c}`, phase: 'Final gate + status', schema: REPORT })
    else await pipeline(bad, (_, it) => agent(repairPrompt(it, finalGate, c), { label: `final repair ${it.controller} #${c}`, phase: 'Final gate + status', schema: REPORT }))
  }
  ITEMS.forEach((it) => { const r = finalGate && finalGate.results.find((x) => x.controller === it.controller); if (r) rows.get(it.controller).gate = r })
}
const reps = A.e2eControllers || ['A', 'B', 'C', 'D', 'E', 'F'].map((g) => (ITEMS.find((it) => it.group === g) || {}).controller).filter(Boolean)
const e2e = !stopReason && A.e2e !== false && finalGate && finalGate.ok
  ? await agent(`${RULES}\nRead ${P('e2e-smoke')}. Generate one real export per group for ${J(reps)} against the dev DB (read TradeNetDBTest from Backend/appsettings.json into env, never print it; use 2025 date ranges), verify each .xlsx with openpyxl per the prompt, and delete the jobs you created.`, { label: 'e2e smoke', phase: 'Final gate + status', schema: E2E, effort: 'high' })
  : (log('e2e smoke skipped'), null)
const statusRows = ITEMS.map((it) => {
  const r = rows.get(it.controller)
  const g = r.gate
  const s = r.skeptic
  const i = r.impl
  const green = !!(g && g.passed) && (!s || !s.refuted || !!r.skepticRepair) && (!i || (i.status !== 'blocked' && i.status !== 'reverted'))
  return {
    controller: it.controller, group: it.group, hasColumnTotals: it.hasColumnTotals, hasCurrencyTotals: it.hasCurrencyTotals,
    headerOk: !!(g && g.header), columnsOk: !!(g && g.columns), footer: g ? g.footer : 'n/a', gatePassed: !!(g && g.passed), repairs: r.repairs,
    skepticRefuted: !!(s && s.refuted), skepticRules: s ? s.refutedRules : [], implStatus: i ? i.status : 'agent-died', green,
    notes: [i && i.notes, s && s.refuted && s.evidence.join('; '), r.skepticRepair && r.skepticRepair.notes].filter(Boolean).join(' | '),
  }
})
const critic = await agent(`${RULES}\nRead ${P('critic')}. Completeness critic: which manifest controllers lack a green row and why? Also list controllers in the manifest missing from these rows.\n${J(statusRows)}`, { label: 'completeness critic', phase: 'Final gate + status', schema: CRITIC })
const written = await agent(`${RULES}\nRead ${P('status-writer')}. You are the ONLY writer of docs/ExcelParity/Status.md. Run stamp: ${RUN_STAMP}. Stop reason: ${stopReason || 'none'}. Write the file from this data only (do not re-verify, edit nothing else):\n${J({ rows: statusRows, critic, e2e, finalGate: finalGate && { ok: finalGate.ok, dbAvailable: finalGate.dbAvailable, summary: finalGate.summary }, frontendGate: feGate && { ok: feGate.ok, summary: feGate.summary }, pages: pageResults.filter(Boolean) })}`, { label: 'status writer', phase: 'Final gate + status', schema: WRITTEN })
const green = statusRows.filter((r) => r.green).length
log(`${green}/${ITEMS.length} green; not green: ${J((critic ? critic.notGreen : []).map((n) => n.controller))}`)
return {
  stopReason, green, total: ITEMS.length, notGreen: critic ? critic.notGreen : [], statusPath: written && written.path,
  finalGateOk: !!(finalGate && finalGate.ok), e2e,
  mergeable: !stopReason && !!(finalGate && finalGate.ok && (!pages.length || (feGate && feGate.ok)) && (!e2e || !e2e.failed.length)) && green === ITEMS.length,
}

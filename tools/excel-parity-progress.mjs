#!/usr/bin/env node
// Live progress for the Excel-export parity workflow.
//
//   node tools/excel-parity-progress.mjs           once
//   node tools/excel-parity-progress.mjs --watch    refresh every 20s
//
// Reads only aggregates from the workflow transcript (never dumps agent text),
// plus git history and docs/ExcelParity/Status.md when it exists.
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const WATCH = process.argv.includes('--watch');
const PHASES = ['Core', 'Fan-out', 'Bespoke pages', 'Final gate + status'];

const git = (...a) => { try { return execFileSync('git', a, { cwd: repoRoot, encoding: 'utf8' }).trim(); } catch { return ''; } };
const readJson = (f) => { try { return JSON.parse(fs.readFileSync(f, 'utf8')); } catch { return null; } };
const ago = (ms) => { const s = Math.round(ms / 1000); return s < 90 ? `${s}s` : s < 5400 ? `${Math.round(s / 60)}m` : `${(s / 3600).toFixed(1)}h`; };
const bar = (done, total, width = 28) => {
  const filled = total ? Math.round((done / total) * width) : 0;
  return `[${'#'.repeat(filled)}${'.'.repeat(width - filled)}] ${done}/${total}`;
};

// Newest workflow run directory for this project.
function findRunDir() {
  const base = path.join(os.homedir(), '.claude', 'projects');
  if (!fs.existsSync(base)) return null;
  const out = [];
  for (const proj of fs.readdirSync(base)) {
    if (!proj.includes('tradenet-admin-report')) continue;
    const sessions = path.join(base, proj);
    for (const sess of fs.readdirSync(sessions)) {
      const wf = path.join(sessions, sess, 'subagents', 'workflows');
      if (!fs.existsSync(wf)) continue;
      for (const run of fs.readdirSync(wf)) {
        const dir = path.join(wf, run);
        const journal = path.join(dir, 'journal.jsonl');
        if (fs.existsSync(journal)) out.push({ dir, run, mtime: fs.statSync(journal).mtimeMs });
      }
    }
  }
  out.sort((a, b) => b.mtime - a.mtime);
  return out[0] ?? null;
}

// One line per agent: label (from its prompt), phase, size, last write.
function agents(dir) {
  const rows = [];
  for (const name of fs.readdirSync(dir)) {
    if (!name.startsWith('agent-') || !name.endsWith('.jsonl')) continue;
    const file = path.join(dir, name);
    const st = fs.statSync(file);
    // Decide "finished" from the file clock BEFORE parsing: an agent killed mid-write
    // leaves a truncated transcript whose parse throws, and it must not be reported active.
    let done = Date.now() - st.mtimeMs > 120000;
    let label = '?', phase = '?';
    try {
      const head = fs.readFileSync(file, 'utf8', { flag: 'r' }).slice(0, 4000);
      const first = JSON.parse(head.slice(0, head.indexOf('\n')));
      const prompt = String(first?.message?.content ?? '');
      // Skip the shared _preamble/_gate-common mentions: the role is the first
      // prompt file that is not an underscore-prefixed include.
      const names = [...prompt.matchAll(/Prompts\/([\w-]+)\.md/g)].map((x) => x[1]);
      const role = names.find((n) => !n.startsWith('_')) ?? names[0] ?? 'agent';
      const ctrl = /"controller":"(\w+)"/.exec(prompt) ?? /Page: (\w+)/.exec(prompt);
      label = `${role}${ctrl ? ` ${ctrl[1].replace(/Controller$/, '')}` : ''}`;
      phase = /implement-|footer-check/.test(label) ? 'Fan-out'
        : /core-/.test(label) ? 'Core'
        : /bespoke/.test(label) ? 'Bespoke pages'
        : /gate-final|critic|status-writer|e2e/.test(label) ? 'Final gate + status'
        : /gate-/.test(label) ? 'gate' : '?';
    } catch { label = 'unparsable (killed mid-write)'; }
    rows.push({ label, phase, kb: Math.round(st.size / 1024), idle: Date.now() - st.mtimeMs, done });
  }
  return rows.sort((a, b) => a.idle - b.idle);
}

function render() {
  const run = findRunDir();
  const manifest = readJson(path.join(repoRoot, 'docs', 'ExcelParity', 'manifest.json'));
  const total = manifest?.counts?.total ?? 160;
  const lines = [];
  lines.push(`Excel parity progress — ${new Date().toISOString().replace('T', ' ').slice(0, 19)}Z`);
  lines.push('');

  if (!run) {
    lines.push('No workflow run found yet.');
  } else {
    const ags = agents(run.dir);
    const live = ags.filter((a) => !a.done);
    const started = fs.statSync(path.join(run.dir, 'journal.jsonl')).birthtimeMs;
    lines.push(`Run ${run.run}   elapsed ${ago(Date.now() - started)}   agents ${ags.length} (${live.length} active)`);
    const seen = new Set(ags.map((a) => a.phase));
    const phase = PHASES.filter((p) => seen.has(p)).pop() ?? 'Core';
    lines.push(`Phase: ${phase}  (${PHASES.map((p) => (p === phase ? `>${p}<` : p)).join(' -> ')})`);
    lines.push('');
    lines.push('Active agents:');
    if (!live.length) lines.push('  (none — between steps, or the run finished)');
    for (const a of live.slice(0, 16)) lines.push(`  ${a.label.padEnd(38)} ${String(a.kb).padStart(5)} KB  last write ${ago(a.idle)} ago`);
  }

  // Controller files edited since the parity work began (the fan-out's real output).
  const baseline = git('rev-list', '-1', '--grep=Excel export parity workflow and manifest', 'HEAD') || 'HEAD';
  const touched = git('diff', '--name-only', `${baseline}..HEAD`, '--', 'Backend/Controllers/Report')
    .split('\n').filter(Boolean);
  const dirty = git('status', '--porcelain', '--', 'Backend/Controllers/Report').split('\n').filter(Boolean).length;
  lines.push('');
  lines.push(`Report controllers changed: ${bar(touched.length, total)}${dirty ? `  (+${dirty} uncommitted)` : ''}`);

  const core = [
    ['spec DTO', 'Backend/Model/ExcelExport/ExcelPresentationSpec.cs'],
    ['layout builder', 'Backend/Service/ExcelExport/ExcelLayoutBuilder.cs'],
    ['footer builder', 'Backend/Service/ExcelExport/ExcelFooterBuilder.cs'],
    ['totals resolver', 'Backend/Service/ExcelExport/ExcelFooterTotalsResolver.cs'],
    ['spec filter', 'Backend/Service/ExcelExport/RequireExcelPresentationSpecFilter.cs'],
    ['contract test', 'Backend.Tests/ExcelSpecContractTests.cs'],
    ['fe spec builder', 'Frontend/src/Report/excel/buildExcelPresentation.ts'],
    ['fe enqueue', 'Frontend/src/Report/excel/excelEnqueue.ts'],
    ['fe shared helpers', 'Frontend/src/Report/reportPresentation.ts'],
  ];
  lines.push('');
  lines.push('Core pieces:');
  for (const [name, rel] of core) {
    lines.push(`  ${fs.existsSync(path.join(repoRoot, rel)) ? '[x]' : '[ ]'} ${name}`);
  }
  const fixtures = path.join(repoRoot, 'Backend.Tests', 'Fixtures', 'ExcelSpecs');
  const nFix = fs.existsSync(fixtures) ? fs.readdirSync(fixtures).filter((f) => f.endsWith('.json')).length : 0;
  lines.push(`  ${nFix ? '[x]' : '[ ]'} fixtures generated: ${nFix} json files (expect ~193 incl. index + allowlist)`);

  const statusPath = path.join(repoRoot, 'docs', 'ExcelParity', 'Status.md');
  lines.push('');
  if (fs.existsSync(statusPath)) {
    const txt = fs.readFileSync(statusPath, 'utf8');
    const mergeable = /^MERGEABLE:\s*(\w+)/m.exec(txt);
    lines.push(`Status.md present — MERGEABLE: ${mergeable ? mergeable[1] : '?'}`);
    for (const l of txt.split('\n').filter((l) => /^(Green|Not green|Final gate|Frontend gate|e2e)/i.test(l)).slice(0, 6)) lines.push(`  ${l}`);
  } else {
    lines.push('Status.md not written yet (final phase writes it; it carries the MERGEABLE verdict).');
  }
  lines.push('');
  lines.push(`Last commits: ${git('log', '--oneline', '-3').split('\n').join(' | ')}`);
  return lines.join('\n');
}

if (WATCH) {
  const tick = () => { process.stdout.write('\x1b[2J\x1b[H' + render() + '\n\nwatching — Ctrl+C to stop\n'); };
  tick();
  setInterval(tick, 20000);
} else {
  console.log(render());
}

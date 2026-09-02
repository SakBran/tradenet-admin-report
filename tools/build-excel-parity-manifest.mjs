// Regenerates docs/ExcelParity/manifest.json (Excel parity plan, Phase 1).
//
//   node tools/build-excel-parity-manifest.mjs
//
// Thin wrapper: runs Frontend/scripts/excelParityManifest.ts through the
// Frontend's own vite-node (the configs must be imported at runtime, see the
// script header) and prints the manifest's summary counts.
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const frontendRoot = path.join(repoRoot, 'Frontend');
const manifestPath = path.join(repoRoot, 'docs', 'ExcelParity', 'manifest.json');
const viteNode = path.join(
  frontendRoot,
  'node_modules',
  '.bin',
  process.platform === 'win32' ? 'vite-node.cmd' : 'vite-node'
);

if (!fs.existsSync(viteNode)) {
  console.error(`vite-node not found at ${viteNode} — run "npm install" in Frontend first.`);
  process.exit(1);
}

const result = spawnSync(viteNode, ['scripts/excelParityManifest.ts'], {
  cwd: frontendRoot,
  stdio: 'inherit',
  shell: process.platform === 'win32',
});

if (result.error) {
  console.error(result.error.message);
  process.exit(1);
}
if (result.status !== 0) {
  process.exit(result.status ?? 1);
}

const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
console.log(`HEAD ${manifest.generatedFrom.head}`);
console.log(JSON.stringify(manifest.counts, null, 2));
if (manifest.unknown.length) {
  console.log(`UNKNOWN group: ${manifest.unknown.join(', ')}`);
}
if (manifest.controllersWithoutConfig.length) {
  console.log(`Controllers without config: ${manifest.controllersWithoutConfig.join(', ')}`);
}
if (manifest.configsWithoutController.length) {
  console.log(`Configs without streaming controller: ${manifest.configsWithoutController.join(', ')}`);
}
console.log(`Excluded: ${manifest.excluded.map((e) => e.controller).join(', ')}`);

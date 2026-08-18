import fs from 'node:fs/promises';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import { auditGlb } from './audit-glb.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectDir = path.resolve(__dirname, '..');
const distDir = path.join(projectDir, 'dist');
const transformCli = path.join(projectDir, 'node_modules/.bin/gltf-transform');

const files = {
  raw: path.join(distDir, 'PoliceStation_Mobile.raw.glb'),
  step1: path.join(distDir, 'PoliceStation_Mobile.step1.glb'),
  step2: path.join(distDir, 'PoliceStation_Mobile.step2.glb'),
  step3: path.join(distDir, 'PoliceStation_Mobile.step3.glb'),
  unity: path.join(distDir, 'PoliceStation_Mobile_Unity.glb'),
  web: path.join(distDir, 'PoliceStation_Mobile_Web_Meshopt.glb'),
};

function run(executable, args) {
  const result = spawnSync(executable, args, {
    cwd: projectDir,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  if (result.stdout.trim()) process.stdout.write(result.stdout);
  if (result.stderr.trim()) process.stderr.write(result.stderr);
  if (result.status !== 0) {
    throw new Error(`Command failed (${result.status}): ${executable} ${args.join(' ')}`);
  }
}

await fs.mkdir(distDir, { recursive: true });
run(process.execPath, [path.join(__dirname, 'generate-police-station.mjs')]);
run(transformCli, ['dedup', files.raw, files.step1]);
run(transformCli, ['prune', files.step1, files.step2, '--keep-leaves', 'true']);
run(transformCli, ['weld', files.step2, files.step3]);
run(transformCli, ['reorder', files.step3, files.unity, '--target', 'performance']);
run(transformCli, [
  'meshopt', files.unity, files.web,
  '--level', 'high',
  '--quantize-position', '16',
  '--quantize-normal', '10',
  '--quantize-color', '8',
]);
run(transformCli, ['validate', files.unity]);

const unityAudit = await auditGlb(files.unity, { unitySafe: true });
const webAudit = await auditGlb(files.web, { unitySafe: false });
const generation = JSON.parse(await fs.readFile(path.join(distDir, 'generation-report.json'), 'utf8'));
const report = {
  generatedAt: new Date().toISOString(),
  generation,
  unity: unityAudit,
  web: webAudit,
};
await fs.writeFile(path.join(distDir, 'build-report.json'), `${JSON.stringify(report, null, 2)}\n`);

await Promise.all([files.raw, files.step1, files.step2, files.step3].map((file) => fs.rm(file, { force: true })));

console.log(JSON.stringify({
  unity: { file: files.unity, bytes: unityAudit.bytes, sha256: unityAudit.sha256 },
  web: { file: files.web, bytes: webAudit.bytes, sha256: webAudit.sha256 },
  triangles: unityAudit.triangleCount,
  primitives: unityAudit.primitiveCount,
  materials: unityAudit.materialCount,
}, null, 2));


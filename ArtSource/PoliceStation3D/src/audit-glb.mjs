import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

function parseGlb(buffer) {
  if (buffer.length < 20 || buffer.toString('utf8', 0, 4) !== 'glTF') {
    throw new Error('Not a valid binary glTF (GLB) file.');
  }
  const version = buffer.readUInt32LE(4);
  const declaredLength = buffer.readUInt32LE(8);
  if (version !== 2) throw new Error(`Expected GLB version 2, received ${version}.`);
  if (declaredLength !== buffer.length) {
    throw new Error(`GLB length mismatch: header=${declaredLength}, file=${buffer.length}.`);
  }
  const jsonLength = buffer.readUInt32LE(12);
  const jsonType = buffer.toString('utf8', 16, 20);
  if (jsonType !== 'JSON') throw new Error(`Expected first GLB chunk to be JSON, received ${jsonType}.`);
  return JSON.parse(buffer.toString('utf8', 20, 20 + jsonLength));
}

export async function auditGlb(inputPath, { unitySafe = true } = {}) {
  const bytes = await fs.readFile(inputPath);
  const gltf = parseGlb(bytes);
  const primitives = (gltf.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const triangles = primitives.reduce((sum, primitive) => {
    if (primitive.mode != null && primitive.mode !== 4) return sum;
    if (primitive.indices == null) {
      const accessorIndex = primitive.attributes?.POSITION;
      return sum + Math.floor((gltf.accessors?.[accessorIndex]?.count ?? 0) / 3);
    }
    return sum + Math.floor((gltf.accessors?.[primitive.indices]?.count ?? 0) / 3);
  }, 0);

  const materialNames = (gltf.materials ?? []).map((material) => material.name ?? '(unnamed)');
  const alphaModes = (gltf.materials ?? []).map((material) => material.alphaMode ?? 'OPAQUE');
  const requiredExtensions = gltf.extensionsRequired ?? [];
  const errors = [];

  if (unitySafe && requiredExtensions.length) {
    errors.push(`Unity-safe profile must not require extensions: ${requiredExtensions.join(', ')}`);
  }
  if (unitySafe && alphaModes.some((mode) => mode !== 'OPAQUE')) {
    errors.push(`Unity-safe profile uses non-opaque alpha modes: ${[...new Set(alphaModes)].join(', ')}`);
  }
  if (primitives.length > 5) errors.push(`Expected at most 5 render primitives; found ${primitives.length}.`);
  if ((gltf.materials?.length ?? 0) > 5) errors.push(`Expected at most 5 materials; found ${gltf.materials.length}.`);
  if (triangles > 13000) errors.push(`Triangle budget exceeded: ${triangles} > 13000.`);
  if ((gltf.textures?.length ?? 0) > 0) errors.push('Texture-free mobile profile unexpectedly contains textures.');

  const report = {
    file: path.resolve(inputPath),
    sha256: crypto.createHash('sha256').update(bytes).digest('hex'),
    bytes: bytes.length,
    generator: gltf.asset?.generator ?? null,
    sceneCount: gltf.scenes?.length ?? 0,
    nodeCount: gltf.nodes?.length ?? 0,
    meshCount: gltf.meshes?.length ?? 0,
    primitiveCount: primitives.length,
    materialCount: gltf.materials?.length ?? 0,
    materials: materialNames,
    alphaModes: [...new Set(alphaModes)],
    textureCount: gltf.textures?.length ?? 0,
    triangleCount: triangles,
    extensionsUsed: gltf.extensionsUsed ?? [],
    extensionsRequired: requiredExtensions,
    unitySafe,
    passed: errors.length === 0,
    errors,
  };

  if (!report.passed) throw new Error(JSON.stringify(report, null, 2));
  return report;
}

const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  const inputPath = process.argv[2];
  if (!inputPath) throw new Error('Usage: node src/audit-glb.mjs <file.glb> [--web]');
  const report = await auditGlb(inputPath, { unitySafe: !process.argv.includes('--web') });
  console.log(JSON.stringify(report, null, 2));
}


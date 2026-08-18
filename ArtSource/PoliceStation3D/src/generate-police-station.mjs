import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import * as THREE from 'three';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import { FontLoader } from 'three/addons/loaders/FontLoader.js';
import { TextGeometry } from 'three/addons/geometries/TextGeometry.js';
import {
  mergeGeometries,
  mergeVertices,
} from 'three/addons/utils/BufferGeometryUtils.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectDir = path.resolve(__dirname, '..');
const distDir = path.join(projectDir, 'dist');
const rawOutputPath = path.join(distDir, 'PoliceStation_Mobile.raw.glb');

// GLTFExporter uses FileReader in browsers. This small geometry-only polyfill
// keeps the official Three.js exporter usable in Node without a DOM/canvas.
if (typeof globalThis.FileReader === 'undefined') {
  globalThis.FileReader = class FileReaderPolyfill {
    constructor() {
      this.result = null;
      this.onloadend = null;
      this.onerror = null;
    }

    async readAsArrayBuffer(blob) {
      try {
        this.result = await blob.arrayBuffer();
        this.onloadend?.({ target: this });
      } catch (error) {
        this.onerror?.(error);
      }
    }

    async readAsDataURL(blob) {
      try {
        const bytes = Buffer.from(await blob.arrayBuffer());
        this.result = `data:${blob.type || 'application/octet-stream'};base64,${bytes.toString('base64')}`;
        this.onloadend?.({ target: this });
      } catch (error) {
        this.onerror?.(error);
      }
    }
  };
}

const C = {
  concrete: 0xb8b6ae,
  concreteLight: 0xd7d4ca,
  concreteDark: 0x777a7b,
  asphalt: 0x30363a,
  asphaltSoft: 0x3d4346,
  sidewalk: 0xa7a39a,
  curb: 0xcdc8bc,
  navy: 0x132b42,
  policeBlue: 0x1e5f9b,
  blueBright: 0x2f83ca,
  red: 0xc63b35,
  redDark: 0x762a2d,
  white: 0xf1efe6,
  warmWhite: 0xffd58a,
  yellow: 0xe3aa34,
  black: 0x171b1e,
  metal: 0x495157,
  metalLight: 0x7c8588,
  glass: 0x24485f,
  glassLight: 0x3c6d84,
  hedge: 0x365943,
  hedgeLight: 0x4e7553,
  soil: 0x59483a,
};

const batches = {
  surface: [],
  metal: [],
  glass: [],
};

const stats = {
  inputParts: 0,
  features: new Map(),
  removedDegenerateTriangles: 0,
};

const v3 = (x, y, z) => new THREE.Vector3(x, y, z);
const e3 = (x = 0, y = 0, z = 0) => new THREE.Euler(x, y, z, 'XYZ');

function noteFeature(feature) {
  stats.inputParts += 1;
  stats.features.set(feature, (stats.features.get(feature) ?? 0) + 1);
}

function removeUnusedAttributes(geometry) {
  geometry.deleteAttribute('uv');
  geometry.deleteAttribute('uv1');
  geometry.deleteAttribute('tangent');
  return geometry;
}

function addVertexColor(geometry, hexColor) {
  const color = new THREE.Color(hexColor);
  const count = geometry.getAttribute('position').count;
  const values = new Uint8Array(count * 3);
  const r = Math.round(color.r * 255);
  const g = Math.round(color.g * 255);
  const b = Math.round(color.b * 255);
  for (let i = 0; i < count; i += 1) {
    values[i * 3] = r;
    values[i * 3 + 1] = g;
    values[i * 3 + 2] = b;
  }
  geometry.setAttribute('color', new THREE.Uint8BufferAttribute(values, 3, true));
  return geometry;
}

function transformed(geometry, {
  position = v3(0, 0, 0),
  rotation = e3(),
  scale = v3(1, 1, 1),
} = {}) {
  const matrix = new THREE.Matrix4();
  const quaternion = new THREE.Quaternion().setFromEuler(rotation);
  matrix.compose(position, quaternion, scale);
  geometry.applyMatrix4(matrix);
  return geometry;
}

function commit(geometry, {
  batch = 'surface',
  color = C.concrete,
  feature = 'misc',
  position,
  rotation,
  scale,
} = {}) {
  removeUnusedAttributes(geometry);
  transformed(geometry, { position, rotation, scale });
  if (!geometry.getAttribute('normal')) geometry.computeVertexNormals();
  addVertexColor(geometry, color);
  batches[batch].push(geometry);
  noteFeature(feature);
  return geometry;
}

function plane(width, height, options = {}) {
  return commit(new THREE.PlaneGeometry(width, height, 1, 1), options);
}

function frontPlane(width, height, x, y, z, color, feature, batch = 'surface') {
  return plane(width, height, {
    batch,
    color,
    feature,
    position: v3(x, y, z),
    rotation: e3(0, Math.PI, 0),
  });
}

function backPlane(width, height, x, y, z, color, feature, batch = 'surface') {
  return plane(width, height, {
    batch,
    color,
    feature,
    position: v3(x, y, z),
  });
}

function groundPlane(width, depth, x, y, z, color, feature, batch = 'surface', rotationY = 0) {
  return plane(width, depth, {
    batch,
    color,
    feature,
    position: v3(x, y, z),
    rotation: e3(-Math.PI / 2, rotationY, 0),
  });
}

function sidePlane(depth, height, x, y, z, outward, color, feature, batch = 'surface') {
  return plane(depth, height, {
    batch,
    color,
    feature,
    position: v3(x, y, z),
    rotation: e3(0, outward > 0 ? Math.PI / 2 : -Math.PI / 2, 0),
  });
}

// A visible shell made from independent planes. Hidden internal/bottom faces can
// be omitted per call, unlike BoxGeometry, keeping geometry intentionally sparse.
function planeShell({
  width,
  height,
  depth,
  center,
  color,
  feature,
  batch = 'surface',
  faces = { front: true, back: true, left: true, right: true, top: true, bottom: false },
}) {
  const x = center.x;
  const y = center.y;
  const z = center.z;
  if (faces.front) frontPlane(width, height, x, y, z - depth / 2, color, feature, batch);
  if (faces.back) backPlane(width, height, x, y, z + depth / 2, color, feature, batch);
  if (faces.left) sidePlane(depth, height, x - width / 2, y, z, -1, color, feature, batch);
  if (faces.right) sidePlane(depth, height, x + width / 2, y, z, 1, color, feature, batch);
  if (faces.top) groundPlane(width, depth, x, y + height / 2, z, color, feature, batch);
  if (faces.bottom) {
    plane(width, depth, {
      batch,
      color,
      feature,
      position: v3(x, y - height / 2, z),
      rotation: e3(Math.PI / 2, 0, 0),
    });
  }
}

function cylinder({
  radiusTop,
  radiusBottom = radiusTop,
  height,
  segments = 8,
  position,
  rotation = e3(),
  color,
  feature,
  batch = 'metal',
  openEnded = false,
}) {
  return commit(
    new THREE.CylinderGeometry(radiusTop, radiusBottom, height, segments, 1, openEnded),
    { batch, color, feature, position, rotation },
  );
}

function shapeGeometry(points, options = {}) {
  const shape = new THREE.Shape();
  shape.moveTo(points[0][0], points[0][1]);
  for (let i = 1; i < points.length; i += 1) shape.lineTo(points[i][0], points[i][1]);
  shape.closePath();
  return commit(new THREE.ShapeGeometry(shape, 1), options);
}

function addFrontWindow({ x, y, width, height, z = -0.026, mullions = 1, feature = 'front_windows' }) {
  frontPlane(width, height, x, y, z + 0.012, C.navy, `${feature}_recess`);
  frontPlane(width - 0.16, height - 0.16, x, y, z, C.glass, feature, 'glass');
  const frame = 0.09;
  frontPlane(width, frame, x, y + height / 2, z - 0.014, C.metal, `${feature}_frames`, 'metal');
  frontPlane(width, frame, x, y - height / 2, z - 0.014, C.metal, `${feature}_frames`, 'metal');
  frontPlane(frame, height, x - width / 2, y, z - 0.014, C.metal, `${feature}_frames`, 'metal');
  frontPlane(frame, height, x + width / 2, y, z - 0.014, C.metal, `${feature}_frames`, 'metal');
  for (let i = 1; i <= mullions; i += 1) {
    const mx = x - width / 2 + (width * i) / (mullions + 1);
    frontPlane(frame * 0.72, height, mx, y, z - 0.016, C.metal, `${feature}_mullions`, 'metal');
  }
}

function addSideWindow({ x, y, z, width, height, outward, feature = 'side_windows' }) {
  const offset = outward * 0.028;
  sidePlane(width, height, x + outward * 0.012, y, z, outward, C.navy, `${feature}_recess`);
  sidePlane(width - 0.16, height - 0.16, x + offset, y, z, outward, C.glass, feature, 'glass');
  const frame = 0.09;
  sidePlane(width, frame, x + outward * 0.036, y + height / 2, z, outward, C.metal, `${feature}_frames`, 'metal');
  sidePlane(width, frame, x + outward * 0.036, y - height / 2, z, outward, C.metal, `${feature}_frames`, 'metal');
  sidePlane(frame, height, x + outward * 0.036, y, z - width / 2, outward, C.metal, `${feature}_frames`, 'metal');
  sidePlane(frame, height, x + outward * 0.036, y, z + width / 2, outward, C.metal, `${feature}_frames`, 'metal');
}

function addBackWindow({ x, y, width, height, z = 16.026, mullions = 1, feature = 'rear_windows' }) {
  backPlane(width, height, x, y, z - 0.012, C.navy, `${feature}_recess`);
  backPlane(width - 0.16, height - 0.16, x, y, z, C.glass, feature, 'glass');
  const frame = 0.09;
  backPlane(width, frame, x, y + height / 2, z + 0.014, C.metal, `${feature}_frames`, 'metal');
  backPlane(width, frame, x, y - height / 2, z + 0.014, C.metal, `${feature}_frames`, 'metal');
  backPlane(frame, height, x - width / 2, y, z + 0.014, C.metal, `${feature}_frames`, 'metal');
  backPlane(frame, height, x + width / 2, y, z + 0.014, C.metal, `${feature}_frames`, 'metal');
  for (let i = 1; i <= mullions; i += 1) {
    const mx = x - width / 2 + (width * i) / (mullions + 1);
    backPlane(frame * 0.72, height, mx, y, z + 0.016, C.metal, `${feature}_mullions`, 'metal');
  }
}

function addGarageDoor(x, width = 5.4) {
  const y = 2.55;
  const height = 4.75;
  frontPlane(width, height, x, y, -0.052, C.black, 'garage_door_recess');
  frontPlane(width - 0.22, height - 0.22, x, y, -0.076, C.metal, 'garage_doors', 'metal');
  for (let row = 1; row < 9; row += 1) {
    frontPlane(width - 0.28, 0.035, x, 0.25 + (row * (height - 0.5)) / 9, -0.09, C.black, 'garage_door_ribs');
  }
  frontPlane(0.12, height, x - width / 2, y, -0.095, C.yellow, 'garage_hazard_markings');
  frontPlane(0.12, height, x + width / 2, y, -0.095, C.yellow, 'garage_hazard_markings');
}

function addParkingBay(x, z, width = 2.7, depth = 5.6, accessible = false) {
  const line = 0.07;
  groundPlane(line, depth, x - width / 2, 0.026, z, C.white, 'parking_lines');
  groundPlane(line, depth, x + width / 2, 0.026, z, C.white, 'parking_lines');
  groundPlane(width, line, x, 0.026, z + depth / 2, C.white, 'parking_lines');
  if (accessible) {
    groundPlane(width - 0.25, depth - 0.25, x, 0.021, z, C.policeBlue, 'accessible_bay');
    groundPlane(0.18, 1.45, x - 0.15, 0.033, z, C.white, 'accessible_symbol');
    groundPlane(0.95, 0.18, x + 0.22, 0.034, z - 0.56, C.white, 'accessible_symbol', 'surface', Math.PI / 5);
    cylinder({
      radiusTop: 0.42,
      height: 0.08,
      segments: 16,
      position: v3(x, 0.035, z + 0.5),
      rotation: e3(0, 0, Math.PI / 2),
      color: C.white,
      feature: 'accessible_symbol',
      batch: 'surface',
    });
  }
}

function addArrow(x, z, rotationY = 0) {
  const points = [
    [-0.28, -1.35], [0.28, -1.35], [0.28, 0.25],
    [0.75, 0.25], [0, 1.35], [-0.75, 0.25], [-0.28, 0.25],
  ];
  shapeGeometry(points, {
    color: C.white,
    feature: 'drive_arrows',
    position: v3(x, 0.032, z),
    rotation: e3(-Math.PI / 2, 0, -rotationY),
  });
}

function addBollard(x, z) {
  cylinder({
    radiusTop: 0.11,
    height: 0.9,
    segments: 8,
    position: v3(x, 0.45, z),
    color: C.yellow,
    feature: 'bollards',
    batch: 'metal',
  });
  cylinder({
    radiusTop: 0.16,
    height: 0.07,
    segments: 8,
    position: v3(x, 0.035, z),
    color: C.black,
    feature: 'bollard_bases',
    batch: 'metal',
  });
}

function addLampPost(x, z, height = 5.6) {
  cylinder({
    radiusTop: 0.075,
    radiusBottom: 0.13,
    height,
    segments: 8,
    position: v3(x, height / 2, z),
    color: C.metal,
    feature: 'lamp_posts',
    batch: 'metal',
  });
  cylinder({
    radiusTop: 0.2,
    height: 0.08,
    segments: 10,
    position: v3(x, 0.04, z),
    color: C.black,
    feature: 'lamp_bases',
    batch: 'metal',
  });
  planeShell({
    width: 0.72,
    height: 0.16,
    depth: 0.34,
    center: v3(x + 0.26, height - 0.06, z),
    color: C.metal,
    feature: 'lamp_heads',
    batch: 'metal',
  });
  groundPlane(0.5, 0.2, x + 0.26, height - 0.16, z, C.warmWhite, 'lamp_lenses');
}

function addPlanter(x, z, width = 3.0, depth = 0.9) {
  planeShell({
    width,
    height: 0.48,
    depth,
    center: v3(x, 0.24, z),
    color: C.concreteLight,
    feature: 'planters',
  });
  groundPlane(width - 0.25, depth - 0.25, x, 0.495, z, C.soil, 'planter_soil');
  const clumps = Math.max(2, Math.round(width / 0.75));
  for (let i = 0; i < clumps; i += 1) {
    const px = x - width / 2 + 0.38 + (i * (width - 0.76)) / Math.max(1, clumps - 1);
    plane(0.85, 0.7, {
      color: i % 2 ? C.hedgeLight : C.hedge,
      feature: 'planter_foliage_cards',
      position: v3(px, 0.83, z),
      rotation: e3(0, i % 2 ? Math.PI / 4 : -Math.PI / 4, 0),
    });
    plane(0.85, 0.7, {
      color: i % 2 ? C.hedge : C.hedgeLight,
      feature: 'planter_foliage_cards',
      position: v3(px, 0.83, z),
      rotation: e3(0, i % 2 ? -Math.PI / 4 : Math.PI / 4, 0),
    });
  }
}

function addBench(x, z, rotationY = 0) {
  const transformPoint = (localX, localZ) => {
    const c = Math.cos(rotationY);
    const s = Math.sin(rotationY);
    return v3(x + localX * c + localZ * s, 0, z - localX * s + localZ * c);
  };
  const seat = transformPoint(0, 0);
  planeShell({
    width: 2.4,
    height: 0.12,
    depth: 0.52,
    center: v3(seat.x, 0.52, seat.z),
    color: C.metalLight,
    feature: 'benches',
    batch: 'metal',
  });
  const back = transformPoint(0, 0.24);
  plane(2.4, 0.62, {
    batch: 'metal',
    color: C.metalLight,
    feature: 'benches',
    position: v3(back.x, 0.92, back.z),
    rotation: e3(0, Math.PI + rotationY, 0),
  });
  for (const lx of [-0.85, 0.85]) {
    const leg = transformPoint(lx, 0);
    cylinder({
      radiusTop: 0.065,
      height: 0.5,
      segments: 6,
      position: v3(leg.x, 0.25, leg.z),
      color: C.black,
      feature: 'bench_legs',
      batch: 'metal',
    });
  }
}

function addFenceRunX(xStart, xEnd, z, height = 2.2, spacing = 1.45) {
  const length = Math.abs(xEnd - xStart);
  const count = Math.max(1, Math.ceil(length / spacing));
  for (let i = 0; i <= count; i += 1) {
    const x = THREE.MathUtils.lerp(xStart, xEnd, i / count);
    cylinder({
      radiusTop: 0.045,
      height,
      segments: 6,
      position: v3(x, height / 2, z),
      color: C.metal,
      feature: 'security_fence',
      batch: 'metal',
    });
  }
  for (const y of [0.25, height - 0.18]) {
    cylinder({
      radiusTop: 0.04,
      height: length,
      segments: 6,
      position: v3((xStart + xEnd) / 2, y, z),
      rotation: e3(0, 0, Math.PI / 2),
      color: C.metal,
      feature: 'security_fence_rails',
      batch: 'metal',
    });
  }
  // Two broad diagonal cards imply chain mesh without an alpha texture.
  const panels = Math.max(1, Math.round(length / 3));
  for (let i = 0; i < panels; i += 1) {
    const x = THREE.MathUtils.lerp(xStart, xEnd, (i + 0.5) / panels);
    frontPlane(length / panels - 0.08, 0.028, x, height / 2, z - 0.012, C.metalLight, 'security_fence_mesh', 'metal');
  }
}

function addFenceRunZ(zStart, zEnd, x, height = 2.2, spacing = 1.45) {
  const length = Math.abs(zEnd - zStart);
  const count = Math.max(1, Math.ceil(length / spacing));
  for (let i = 0; i <= count; i += 1) {
    const z = THREE.MathUtils.lerp(zStart, zEnd, i / count);
    cylinder({
      radiusTop: 0.045,
      height,
      segments: 6,
      position: v3(x, height / 2, z),
      color: C.metal,
      feature: 'security_fence',
      batch: 'metal',
    });
  }
  for (const y of [0.25, height - 0.18]) {
    cylinder({
      radiusTop: 0.04,
      height: length,
      segments: 6,
      position: v3(x, y, (zStart + zEnd) / 2),
      rotation: e3(Math.PI / 2, 0, 0),
      color: C.metal,
      feature: 'security_fence_rails',
      batch: 'metal',
    });
  }
}

function addHVAC(x, z, scale = 1) {
  planeShell({
    width: 2.1 * scale,
    height: 1.15 * scale,
    depth: 1.55 * scale,
    center: v3(x, 8.15 + 0.575 * scale, z),
    color: C.metalLight,
    feature: 'roof_hvac',
    batch: 'metal',
  });
  for (const offset of [-0.48, 0, 0.48]) {
    sidePlane(1.0 * scale, 0.055 * scale, x - 1.05 * scale - 0.02, 8.15 + 0.55 * scale + offset * scale, z, -1, C.black, 'roof_hvac_vents');
  }
}

function addCamera(x, y, z, rotationY = 0) {
  cylinder({
    radiusTop: 0.045,
    height: 0.45,
    segments: 6,
    position: v3(x, y, z),
    rotation: e3(Math.PI / 2, rotationY, 0),
    color: C.metal,
    feature: 'security_cameras',
    batch: 'metal',
  });
  cylinder({
    radiusTop: 0.1,
    radiusBottom: 0.16,
    height: 0.48,
    segments: 8,
    position: v3(x, y - 0.14, z - 0.24),
    rotation: e3(Math.PI / 2.5, rotationY, 0),
    color: C.white,
    feature: 'security_cameras',
    batch: 'metal',
  });
}

async function loadFont() {
  const fontPath = path.join(projectDir, 'node_modules/three-font-assets/examples/fonts/helvetiker_bold.typeface.json');
  const json = JSON.parse(await fs.readFile(fontPath, 'utf8'));
  return new FontLoader().parse(json);
}

function addFrontText(font, text, {
  x,
  y,
  z,
  size,
  depth = 0.035,
  maxWidth,
  color = C.white,
  feature = 'sign_text',
}) {
  const geometry = new TextGeometry(text, {
    font,
    size,
    depth,
    curveSegments: 1,
    bevelEnabled: false,
  });
  geometry.computeBoundingBox();
  const naturalWidth = geometry.boundingBox.max.x - geometry.boundingBox.min.x;
  if (maxWidth && naturalWidth > maxWidth) {
    const factor = maxWidth / naturalWidth;
    geometry.scale(factor, factor, factor);
  }
  geometry.center();
  commit(geometry, {
    color,
    feature,
    position: v3(x, y, z),
    rotation: e3(0, Math.PI, 0),
  });
}

function addShield(x, y, z, scale = 1) {
  const outer = [
    [-0.72, 0.92], [0.72, 0.92], [0.62, -0.15],
    [0.34, -0.66], [0, -0.96], [-0.34, -0.66], [-0.62, -0.15],
  ];
  shapeGeometry(outer, {
    color: C.white,
    feature: 'shield_badge',
    position: v3(x, y, z),
    rotation: e3(0, Math.PI, 0),
    scale: v3(scale, scale, scale),
  });
  const star = [];
  for (let i = 0; i < 10; i += 1) {
    const angle = Math.PI / 2 + (i * Math.PI) / 5;
    const radius = i % 2 === 0 ? 0.42 : 0.18;
    star.push([Math.cos(angle) * radius, Math.sin(angle) * radius]);
  }
  shapeGeometry(star, {
    color: C.policeBlue,
    feature: 'shield_star',
    position: v3(x, y, z - 0.018),
    rotation: e3(0, Math.PI, 0),
    scale: v3(scale, scale, scale),
  });
}

function addSocket(parent, name, position, rotation = e3(), extras = {}) {
  const socket = new THREE.Object3D();
  socket.name = name;
  socket.position.copy(position);
  socket.rotation.copy(rotation);
  socket.userData = { socket: true, ...extras };
  parent.add(socket);
}

function addColliderHint(parent, name, position, size, extras = {}) {
  const hint = new THREE.Object3D();
  hint.name = name;
  hint.position.copy(position);
  hint.scale.copy(size);
  hint.userData = {
    colliderHint: true,
    type: 'box',
    sizeMeters: [size.x, size.y, size.z],
    ...extras,
  };
  parent.add(hint);
}

function mergeBatch(name, geometries) {
  if (!geometries.length) throw new Error(`Batch ${name} is empty.`);
  const compatible = geometries.map((geometry) => (geometry.index ? geometry.toNonIndexed() : geometry));
  let merged = mergeGeometries(compatible, false);
  merged = mergeVertices(merged, 1e-5);
  stats.removedDegenerateTriangles += removeDegenerateTriangles(merged);
  merged.computeBoundingBox();
  merged.computeBoundingSphere();
  merged.name = `${name}_Geometry`;
  return merged;
}

function removeDegenerateTriangles(geometry, epsilonSquared = 1e-14) {
  const position = geometry.getAttribute('position');
  const index = geometry.index;
  if (!index) return 0;
  const kept = [];
  let removed = 0;
  for (let i = 0; i < index.count; i += 3) {
    const ia = index.getX(i);
    const ib = index.getX(i + 1);
    const ic = index.getX(i + 2);
    const abx = position.getX(ib) - position.getX(ia);
    const aby = position.getY(ib) - position.getY(ia);
    const abz = position.getZ(ib) - position.getZ(ia);
    const acx = position.getX(ic) - position.getX(ia);
    const acy = position.getY(ic) - position.getY(ia);
    const acz = position.getZ(ic) - position.getZ(ia);
    const crossX = aby * acz - abz * acy;
    const crossY = abz * acx - abx * acz;
    const crossZ = abx * acy - aby * acx;
    const areaSquared = crossX * crossX + crossY * crossY + crossZ * crossZ;
    if (ia === ib || ib === ic || ia === ic || areaSquared <= epsilonSquared) {
      removed += 1;
      continue;
    }
    kept.push(ia, ib, ic);
  }
  geometry.setIndex(kept);
  return removed;
}

function triangleCount(geometry) {
  return geometry.index
    ? geometry.index.count / 3
    : geometry.getAttribute('position').count / 3;
}

function buildSite() {
  // Site and pavement: one-sided planes plus only silhouette-relevant curb faces.
  groundPlane(68, 56, 0, -0.035, -2, C.asphalt, 'site_asphalt');
  groundPlane(68, 6.2, 0, -0.018, -27.0, C.asphaltSoft, 'street_apron');
  groundPlane(62, 2.5, 0, 0.012, -22.2, C.sidewalk, 'front_sidewalk');
  frontPlane(62, 0.19, 0, 0.095, -23.45, C.curb, 'front_curb');
  groundPlane(2.4, 43, -29.3, 0.012, 0, C.sidewalk, 'side_sidewalks');
  groundPlane(2.4, 43, 29.3, 0.012, 0, C.sidewalk, 'side_sidewalks');

  // Main administration shell and lower navy plinth.
  planeShell({
    width: 36,
    height: 8.2,
    depth: 16,
    center: v3(-10, 4.1, 8),
    color: C.concrete,
    feature: 'administration_shell',
  });
  frontPlane(36, 1.05, -10, 0.525, -0.018, C.navy, 'facade_plinth');
  sidePlane(16, 1.05, -28.018, 0.525, 8, -1, C.navy, 'facade_plinth');

  // Garage/operations wing; its left face is hidden by the main block.
  planeShell({
    width: 20,
    height: 6.35,
    depth: 18,
    center: v3(18, 3.175, 9),
    color: C.concreteDark,
    feature: 'garage_shell',
    faces: { front: true, back: true, left: false, right: true, top: true, bottom: false },
  });
  frontPlane(20, 0.62, 18, 5.78, -0.022, C.policeBlue, 'garage_blue_band');
  sidePlane(18, 0.62, 28.022, 5.78, 9, 1, C.policeBlue, 'garage_blue_band');

  // Raised command block and roof parapets give the civic silhouette.
  planeShell({
    width: 17,
    height: 2.8,
    depth: 11.5,
    center: v3(-8, 9.6, 8.2),
    color: C.concreteLight,
    feature: 'command_block',
  });
  frontPlane(17, 2.1, -8, 9.55, 2.425, C.navy, 'main_sign_panel');

  for (const x of [-27.8, 7.8]) {
    sidePlane(16, 0.55, x, 8.45, 8, x > 0 ? 1 : -1, C.concreteLight, 'roof_parapet');
  }
  frontPlane(36, 0.55, -10, 8.45, -0.02, C.concreteLight, 'roof_parapet');
  backPlane(36, 0.55, -10, 8.45, 16.02, C.concreteLight, 'roof_parapet');

  // Horizontal accent bands from the reference visual language.
  frontPlane(36, 0.28, -10, 6.55, -0.035, C.policeBlue, 'facade_blue_band');
  frontPlane(36, 0.085, -10, 6.22, -0.041, C.red, 'facade_red_pinstripe');
  sidePlane(16, 0.28, -28.035, 6.55, 8, -1, C.policeBlue, 'facade_blue_band');

  // Lobby recess, opaque-glass cards, mullions and canopy.
  frontPlane(10.2, 4.1, -5.6, 2.55, -0.05, C.navy, 'lobby_recess');
  for (let i = 0; i < 4; i += 1) {
    addFrontWindow({
      x: -9.45 + i * 2.58,
      y: 2.52,
      width: 2.42,
      height: 3.55,
      z: -0.076,
      mullions: 0,
      feature: 'lobby_glazing',
    });
  }
  frontPlane(2.8, 0.17, -5.6, 2.45, -0.125, C.metal, 'entrance_door_mullion', 'metal');
  frontPlane(0.12, 3.5, -5.6, 2.52, -0.128, C.metal, 'entrance_door_mullion', 'metal');

  planeShell({
    width: 12.2,
    height: 0.32,
    depth: 3.2,
    center: v3(-5.6, 4.42, -1.55),
    color: C.navy,
    feature: 'entrance_canopy',
    faces: { front: true, back: false, left: true, right: true, top: true, bottom: true },
  });
  for (const x of [-10.5, -0.7]) {
    cylinder({
      radiusTop: 0.13,
      height: 4.05,
      segments: 8,
      position: v3(x, 2.025, -2.62),
      color: C.metal,
      feature: 'canopy_columns',
      batch: 'metal',
    });
  }
  groundPlane(11, 2.7, -5.6, 0.035, -1.5, C.sidewalk, 'entrance_pad');
  groundPlane(8.8, 2.5, -5.6, 0.05, -4.0, C.sidewalk, 'entrance_walk');
  for (const x of [-10.3, -8.4, -2.8, -0.9]) addBollard(x, -3.08);

  // Admin office windows: dark recess cards with minimal frame planes.
  for (const x of [-25.0, -21.3, -17.6, -13.9]) {
    addFrontWindow({ x, y: 2.7, width: 2.7, height: 2.15, mullions: 1, feature: 'admin_ground_windows' });
    addFrontWindow({ x, y: 5.35, width: 2.7, height: 1.45, mullions: 1, feature: 'admin_upper_windows' });
  }
  for (const x of [0.2, 3.25]) {
    addFrontWindow({ x, y: 5.25, width: 2.45, height: 1.5, mullions: 1, feature: 'lobby_upper_windows' });
  }
  for (const z of [3.0, 6.3, 9.6, 12.9]) {
    addSideWindow({ x: -28.026, y: 4.15, z, width: 2.35, height: 1.65, outward: -1 });
  }

  // Garage front and service details.
  addGarageDoor(11.75, 5.45);
  addGarageDoor(18.0, 5.45);
  addGarageDoor(24.25, 5.45);
  frontPlane(1.35, 2.65, 27.1, 1.325, -0.082, C.navy, 'garage_service_door');
  frontPlane(0.22, 0.72, 26.78, 1.35, -0.102, C.metalLight, 'garage_door_handle', 'metal');
  for (const x of [11.75, 18.0, 24.25]) {
    planeShell({
      width: 1.0,
      height: 0.18,
      depth: 0.42,
      center: v3(x, 5.28, -0.22),
      color: C.black,
      feature: 'garage_wall_lights',
      batch: 'metal',
    });
    groundPlane(0.72, 0.25, x, 5.17, -0.22, C.warmWhite, 'garage_wall_light_lenses');
  }

  // Rear service yard language: shutters, utility cabinets and dumpster.
  backPlane(36, 0.28, -10, 6.55, 16.035, C.policeBlue, 'rear_blue_band');
  backPlane(36, 0.085, -10, 6.22, 16.041, C.red, 'rear_red_pinstripe');
  for (const x of [-24.8, -20.6, -16.4, -12.2, -8.0, -3.8]) {
    addBackWindow({ x, y: 4.85, width: 2.75, height: 1.5, mullions: 1, feature: 'rear_admin_upper_windows' });
  }
  for (const x of [-24.4, -19.7, -15.0, -10.3]) {
    addBackWindow({ x, y: 2.15, width: 3.05, height: 1.75, mullions: 1, feature: 'rear_admin_ground_windows' });
  }
  backPlane(1.65, 2.75, 4.7, 1.375, 16.052, C.navy, 'rear_emergency_exit');
  backPlane(0.24, 0.08, 5.18, 1.34, 16.072, C.metalLight, 'rear_exit_handle', 'metal');
  for (const x of [-25.4, -13.0, -1.0]) {
    planeShell({
      width: 0.92,
      height: 0.18,
      depth: 0.4,
      center: v3(x, 6.88, 16.22),
      color: C.black,
      feature: 'rear_wall_lights',
      batch: 'metal',
    });
    plane(0.68, 0.22, {
      color: C.warmWhite,
      feature: 'rear_wall_light_lenses',
      position: v3(x, 6.76, 16.22),
      rotation: e3(Math.PI / 2, 0, 0),
    });
  }
  backPlane(5.8, 4.4, 19.5, 2.35, 18.025, C.black, 'rear_sally_port_recess');
  backPlane(5.5, 4.15, 19.5, 2.35, 18.052, C.metal, 'rear_sally_port', 'metal');
  for (let row = 0; row < 8; row += 1) {
    backPlane(5.2, 0.035, 19.5, 0.55 + row * 0.48, 18.065, C.black, 'rear_sally_port_ribs');
  }
  for (const [x, width, height] of [[10.3, 1.0, 1.65], [12.0, 1.3, 1.9], [14.0, 1.0, 1.45]]) {
    planeShell({
      width,
      height,
      depth: 0.42,
      center: v3(x, height / 2, 18.18),
      color: C.metalLight,
      feature: 'utility_cabinets',
      batch: 'metal',
      faces: { front: false, back: true, left: true, right: true, top: true, bottom: false },
    });
  }
  planeShell({
    width: 3.1,
    height: 1.35,
    depth: 1.65,
    center: v3(25.5, 0.675, 19.4),
    color: C.hedge,
    feature: 'dumpster',
    batch: 'metal',
  });
  groundPlane(3.2, 1.7, 25.5, 1.39, 19.4, C.black, 'dumpster_lid', 'metal');

  // Site parking and traffic graphics, all plane-based.
  const parkingZ = -14.8;
  const bayXs = [-23.8, -20.8, -17.8, -14.8, 8.8, 11.8, 14.8, 17.8, 20.8, 23.8];
  for (let i = 0; i < bayXs.length; i += 1) addParkingBay(bayXs[i], parkingZ, 2.7, 5.6, i === 3);
  groundPlane(21, 0.09, -1.2, 0.027, -8.2, C.yellow, 'traffic_centerline');
  for (let i = 0; i < 7; i += 1) {
    groundPlane(0.55, 3.3, -11.0 + i * 0.9, 0.032, -5.9, C.white, 'crosswalk');
  }
  addArrow(3.2, -9.4, 0);
  addArrow(27.0, -7.0, Math.PI / 2);

  // Planters, benches and lights keep the forecourt readable at game camera scale.
  addPlanter(-20.7, -3.2, 4.4, 1.0);
  addPlanter(3.2, -3.2, 4.1, 1.0);
  addPlanter(-27.0, 18.8, 1.0, 5.0);
  addBench(-16.0, -3.7, 0);
  addBench(4.2, -3.7, Math.PI);
  for (const [x, z] of [[-27, -19], [27, -19], [-27, 20], [27, 22]]) addLampPost(x, z);

  // Fence wraps the secure rear yard but leaves the public forecourt open.
  addFenceRunX(8.3, 28.5, 24.2);
  addFenceRunZ(18.5, 24.2, 28.5);
  addFenceRunZ(18.5, 24.2, 8.3);

  // Rooftop helipad: dark painted plane, ring, and block H.
  groundPlane(13.6, 13.6, -19.6, 8.225, 8.0, C.asphaltSoft, 'helipad_surface');
  const ring = new THREE.RingGeometry(5.0, 5.35, 40, 1);
  commit(ring, {
    color: C.white,
    feature: 'helipad_ring',
    position: v3(-19.6, 8.245, 8.0),
    rotation: e3(-Math.PI / 2, 0, 0),
  });
  groundPlane(0.65, 5.1, -21.15, 8.255, 8.0, C.white, 'helipad_h');
  groundPlane(0.65, 5.1, -18.05, 8.255, 8.0, C.white, 'helipad_h');
  groundPlane(3.1, 0.65, -19.6, 8.258, 8.0, C.white, 'helipad_h');

  addHVAC(1.5, 10.8, 0.85);
  addHVAC(3.9, 10.8, 0.85);
  addHVAC(13.0, 9.8, 0.75);
  addHVAC(22.5, 9.8, 0.75);

  // Antenna mast, red/blue marker lenses and roof rail.
  cylinder({
    radiusTop: 0.09,
    radiusBottom: 0.13,
    height: 6.5,
    segments: 8,
    position: v3(-8, 14.25, 11.4),
    color: C.metal,
    feature: 'antenna_mast',
    batch: 'metal',
  });
  for (const y of [12.2, 14.0, 16.8]) {
    cylinder({
      radiusTop: 0.28,
      height: 0.12,
      segments: 10,
      position: v3(-8, y, 11.4),
      color: y === 14.0 ? C.blueBright : C.red,
      feature: 'antenna_markers',
      batch: 'metal',
    });
  }
  for (const x of [-15.8, -0.2]) {
    cylinder({
      radiusTop: 0.035,
      height: 2.9,
      segments: 6,
      position: v3(x, 12.05, 11.4),
      rotation: e3(0, 0, x < -8 ? -0.92 : 0.92),
      color: C.metal,
      feature: 'antenna_braces',
      batch: 'metal',
    });
  }

  addCamera(-0.4, 4.15, -0.55, 0);
  addCamera(-27.55, 6.9, 0.4, -Math.PI / 2);
  addCamera(27.4, 5.6, 0.3, Math.PI / 2);
  addCamera(27.4, 5.2, 17.4, Math.PI / 2);

  // Flagpole and a two-plane fictional civic flag.
  cylinder({
    radiusTop: 0.055,
    radiusBottom: 0.1,
    height: 7.6,
    segments: 8,
    position: v3(6.0, 3.8, -4.8),
    color: C.metalLight,
    feature: 'flagpole',
    batch: 'metal',
  });
  frontPlane(2.2, 0.75, 7.1, 6.85, -4.8, C.policeBlue, 'civic_flag');
  frontPlane(2.2, 0.75, 7.1, 6.1, -4.8, C.white, 'civic_flag');
  frontPlane(2.2, 0.12, 7.1, 6.48, -4.815, C.red, 'civic_flag');
}

async function buildScene() {
  const font = await loadFont();
  buildSite();

  addShield(-14.3, 9.57, 2.382, 0.88);
  addFrontText(font, 'METRO POLICE', {
    x: -6.6,
    y: 9.87,
    z: 2.365,
    size: 0.93,
    maxWidth: 12.2,
    color: C.white,
    feature: 'main_sign_text',
  });
  addFrontText(font, 'PRECINCT 07  |  PUBLIC SAFETY', {
    x: -6.6,
    y: 9.05,
    z: 2.355,
    size: 0.33,
    maxWidth: 12.1,
    color: C.warmWhite,
    feature: 'main_sign_subtext',
  });
  addFrontText(font, 'VEHICLE SERVICES', {
    x: 18,
    y: 5.8,
    z: -0.052,
    size: 0.38,
    maxWidth: 10.8,
    color: C.white,
    feature: 'garage_sign_text',
  });

  const root = new THREE.Group();
  root.name = 'PoliceStation_Root';
  root.userData = {
    asset: 'Fictional Metro Police Station',
    authoring: 'Procedural Three.js plane/custom-geometry generator',
    units: 'meters',
    upAxis: '+Y',
    frontDirection: '-Z',
    intendedUse: 'Unity URP mobile / Three.js WebGL',
    materialStrategy: 'Three shared opaque glTF PBR materials with normalized u8 vertex colors',
    sourceStyle: 'Original GTA-inspired sunny civic architecture; no real-world insignia',
  };

  const renderGroup = new THREE.Group();
  renderGroup.name = 'LOD0_RenderBatches';
  root.add(renderGroup);

  const materials = {
    surface: new THREE.MeshStandardMaterial({
      name: 'M_Surface_VertexColor',
      color: 0xffffff,
      vertexColors: true,
      roughness: 0.82,
      metalness: 0.02,
      side: THREE.FrontSide,
    }),
    metal: new THREE.MeshStandardMaterial({
      name: 'M_Metal_VertexColor',
      color: 0xffffff,
      vertexColors: true,
      roughness: 0.42,
      metalness: 0.64,
      side: THREE.FrontSide,
    }),
    glass: new THREE.MeshStandardMaterial({
      name: 'M_Glass_Opaque_VertexColor',
      color: 0xffffff,
      vertexColors: true,
      roughness: 0.2,
      metalness: 0.12,
      transparent: false,
      side: THREE.FrontSide,
    }),
  };

  const merged = {};
  for (const batchName of Object.keys(batches)) {
    const geometry = mergeBatch(`BATCH_${batchName}`, batches[batchName]);
    merged[batchName] = geometry;
    const mesh = new THREE.Mesh(geometry, materials[batchName]);
    mesh.name = `BATCH_${batchName[0].toUpperCase()}${batchName.slice(1)}_LOD0`;
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    mesh.userData = {
      staticBatch: true,
      drawCallGroup: batchName,
      sourcePartCount: batches[batchName].length,
    };
    renderGroup.add(mesh);
  }

  const sockets = new THREE.Group();
  sockets.name = 'SOCKETS';
  addSocket(sockets, 'SOCKET_MainEntrance', v3(-5.6, 0, -4.9), e3(0, Math.PI, 0));
  addSocket(sockets, 'SOCKET_GarageBay_01', v3(11.75, 0, -4.8), e3(0, Math.PI, 0));
  addSocket(sockets, 'SOCKET_GarageBay_02', v3(18.0, 0, -4.8), e3(0, Math.PI, 0));
  addSocket(sockets, 'SOCKET_GarageBay_03', v3(24.25, 0, -4.8), e3(0, Math.PI, 0));
  addSocket(sockets, 'SOCKET_RoofSpawn', v3(-19.6, 8.32, 8.0));
  addSocket(sockets, 'SOCKET_SallyPort', v3(19.5, 0, 22.0));
  addSocket(sockets, 'SOCKET_Flag', v3(6.0, 7.6, -4.8));
  root.add(sockets);

  const colliderHints = new THREE.Group();
  colliderHints.name = 'COLLIDER_HINTS';
  colliderHints.userData = { renderersDisabled: true, createUnityCollidersFromChildren: true };
  addColliderHint(colliderHints, 'COL_Admin', v3(-10, 4.1, 8), v3(36, 8.2, 16));
  addColliderHint(colliderHints, 'COL_Garage', v3(18, 3.175, 9), v3(20, 6.35, 18));
  addColliderHint(colliderHints, 'COL_CommandBlock', v3(-8, 9.6, 8.2), v3(17, 2.8, 11.5));
  addColliderHint(colliderHints, 'COL_Canopy', v3(-5.6, 4.42, -1.55), v3(12.2, 0.32, 3.2));
  root.add(colliderHints);

  const scene = new THREE.Scene();
  scene.name = 'PoliceStation_Mobile_Scene';
  scene.add(root);

  const report = {
    generatedAt: new Date().toISOString(),
    output: rawOutputPath,
    sourceParts: stats.inputParts,
    removedDegenerateTriangles: stats.removedDegenerateTriangles,
    features: Object.fromEntries([...stats.features.entries()].sort(([a], [b]) => a.localeCompare(b))),
    batches: Object.fromEntries(Object.entries(merged).map(([name, geometry]) => [name, {
      vertices: geometry.getAttribute('position').count,
      triangles: triangleCount(geometry),
      indexed: Boolean(geometry.index),
      indexComponentBits: geometry.index?.array.BYTES_PER_ELEMENT * 8 ?? null,
    }])),
    totals: {
      meshes: renderGroup.children.length,
      materials: Object.keys(materials).length,
      vertices: Object.values(merged).reduce((sum, geometry) => sum + geometry.getAttribute('position').count, 0),
      triangles: Object.values(merged).reduce((sum, geometry) => sum + triangleCount(geometry), 0),
    },
  };

  return { scene, report };
}

async function exportBinary(scene) {
  const exporter = new GLTFExporter();
  return new Promise((resolve, reject) => {
    exporter.parse(
      scene,
      (result) => {
        if (!(result instanceof ArrayBuffer)) {
          reject(new Error('Expected GLTFExporter to return an ArrayBuffer for binary output.'));
          return;
        }
        resolve(result);
      },
      reject,
      {
        binary: true,
        trs: false,
        onlyVisible: true,
        truncateDrawRange: true,
        includeCustomExtensions: false,
      },
    );
  });
}

await fs.mkdir(distDir, { recursive: true });
const { scene, report } = await buildScene();
const glb = await exportBinary(scene);
await fs.writeFile(rawOutputPath, Buffer.from(glb));
await fs.writeFile(path.join(distDir, 'generation-report.json'), `${JSON.stringify(report, null, 2)}\n`);

console.log(JSON.stringify({
  output: rawOutputPath,
  bytes: glb.byteLength,
  ...report.totals,
}, null, 2));

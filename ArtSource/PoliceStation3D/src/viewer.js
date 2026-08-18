import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x91c3df);
scene.fog = new THREE.Fog(0x91c3df, 90, 155);

const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.75));
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFShadowMap;
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.08;
document.body.prepend(renderer.domElement);

const camera = new THREE.PerspectiveCamera(42, window.innerWidth / window.innerHeight, 0.1, 260);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.065;
controls.target.set(-3, 4.8, 0);
controls.maxPolarAngle = Math.PI * 0.49;
controls.minDistance = 16;
controls.maxDistance = 125;

scene.add(new THREE.HemisphereLight(0xd8eeff, 0x4b4337, 2.05));
const sun = new THREE.DirectionalLight(0xfff1d3, 3.25);
sun.position.set(-42, 60, -32);
sun.castShadow = true;
sun.shadow.mapSize.set(2048, 2048);
sun.shadow.camera.left = -55;
sun.shadow.camera.right = 55;
sun.shadow.camera.top = 55;
sun.shadow.camera.bottom = -55;
sun.shadow.camera.near = 5;
sun.shadow.camera.far = 140;
sun.shadow.bias = -0.00025;
scene.add(sun);

const fill = new THREE.DirectionalLight(0x8ac7ff, 0.75);
fill.position.set(35, 18, 32);
scene.add(fill);

const views = {
  hero: { position: [55, 39, -63], target: [-3, 4.6, 1] },
  front: { position: [0, 12, -78], target: [-3, 4.1, 1] },
  rear: { position: [3, 18, 72], target: [0, 4.0, 8] },
  aerial: { position: [58, 55, -55], target: [-2, 2.8, 1] },
  top: { position: [0.01, 96, 0.01], target: [0, 0, 0] },
};

function setView(name) {
  const view = views[name];
  camera.position.fromArray(view.position);
  controls.target.fromArray(view.target);
  controls.update();
}
setView('hero');

document.querySelectorAll('[data-view]').forEach((button) => {
  button.addEventListener('click', () => setView(button.dataset.view));
});

const status = document.querySelector('#status');
const loader = new GLTFLoader();
loader.setMeshoptDecoder(MeshoptDecoder);
const profile = new URLSearchParams(window.location.search).get('profile') === 'unity' ? 'Unity' : 'Web Meshopt';
const modelUrl = profile === 'Unity'
  ? '/dist/PoliceStation_Mobile_Unity.glb'
  : '/dist/PoliceStation_Mobile_Web_Meshopt.glb';
status.textContent = `Đang nạp ${profile} GLB…`;
loader.load(
  modelUrl,
  (gltf) => {
    gltf.scene.traverse((object) => {
      if (object.isMesh) {
        object.castShadow = true;
        object.receiveShadow = true;
      }
    });
    scene.add(gltf.scene);
    status.textContent = `${profile} GLB đã nạp · kéo để xoay · cuộn để zoom`;
  },
  (event) => {
    if (event.total) status.textContent = `Đang nạp ${Math.round((event.loaded / event.total) * 100)}%`;
  },
  (error) => {
    console.error(error);
    status.textContent = 'Không thể nạp GLB';
  },
);

function animate() {
  controls.update();
  renderer.render(scene, camera);
  requestAnimationFrame(animate);
}
animate();

window.addEventListener('resize', () => {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
});

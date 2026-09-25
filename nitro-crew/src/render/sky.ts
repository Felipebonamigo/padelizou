// Céu: domo com gradiente de 3 cores, sol/lua (disco + halo) no próprio shader, estrelas à
// noite, nuvens low-poly de dia, luzes (direcional com sombras + hemisférica), névoa e o env
// map (PMREM do próprio domo) para o reflexo nos carros. O grupo do céu gira pelo rumo
// absoluto do carro: é a única coisa "fixa no mundo".
import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import type { TimeOfDay } from '../core/types';
import type { Quality } from '../game/contracts';
import { hash2 } from './noise';
import type { Palette } from './palette';

const SKY_RADIUS = 1400;

const SKY_VERT = /* glsl */ `
varying vec3 vDir;
void main() {
  vDir = position;
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}`;

const SKY_FRAG = /* glsl */ `
uniform vec3 uTop; uniform vec3 uMid; uniform vec3 uHorizon;
uniform vec3 uSunDir; uniform vec3 uSunColor;
uniform float uSunSize; uniform float uHalo; uniform float uGlow; uniform float uDisc;
varying vec3 vDir;
void main() {
  vec3 d = normalize(vDir);
  float h = d.y;
  vec3 col = mix(uHorizon, uMid, smoothstep(0.0, 0.22, h));
  col = mix(col, uTop, smoothstep(0.18, 0.7, h));
  col = mix(col, uHorizon * 0.9, smoothstep(0.0, -0.25, h));
  float ang = acos(clamp(dot(d, uSunDir), -1.0, 1.0));
  float disc = 1.0 - smoothstep(uSunSize, uSunSize * 1.3, ang);
  float halo = exp(-ang * uHalo);
  float glow = exp(-ang * 1.6) * uGlow * clamp(1.0 - h * 2.5, 0.0, 1.0);
  col += uSunColor * (disc * uDisc + halo * 0.8 + glow);
  gl_FragColor = vec4(col, 1.0);
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
}`;

interface SunSetup { dir: THREE.Vector3; color: string; intensity: number; exposure: number; size: number; halo: number; glow: number; disc: number }

/** Posição do sol/lua no mundo absoluto (x direita, y cima, −z frente na largada). */
function sunSetup(time: TimeOfDay, p: Palette): SunSetup {
  const fromAngles = (elevDeg: number, azDeg: number) => {
    const el = elevDeg * Math.PI / 180; const az = azDeg * Math.PI / 180;
    return new THREE.Vector3(Math.sin(az) * Math.cos(el), Math.sin(el), -Math.cos(az) * Math.cos(el)).normalize();
  };
  if (time === 'day') return { dir: fromAngles(48, -55), color: p.sun ?? '#fff3a0', intensity: 4.2, exposure: 1.0, size: 0.03, halo: 9, glow: 0.25, disc: 6 };
  if (time === 'dusk') return { dir: fromAngles(12, 28), color: p.sun ?? '#ffb347', intensity: 3.8, exposure: 1.05, size: 0.045, halo: 5, glow: 0.9, disc: 5 };
  return { dir: fromAngles(42, -120), color: p.moon ?? '#f4f1d8', intensity: 1.1, exposure: 1.0, size: 0.02, halo: 14, glow: 0.05, disc: 2.2 };
}

function buildStars(): THREE.Points {
  const n = 1100;
  const pos = new Float32Array(n * 3);
  const sizes = new Float32Array(n);
  for (let i = 0; i < n; i++) {
    const u = hash2(7, i); const v = hash2(13, i);
    const theta = u * Math.PI * 2;
    const y = 0.04 + v * 0.96;
    const r = Math.sqrt(1 - y * y);
    const R = SKY_RADIUS * 0.96;
    pos[i * 3] = Math.cos(theta) * r * R; pos[i * 3 + 1] = y * R; pos[i * 3 + 2] = Math.sin(theta) * r * R;
    sizes[i] = 1;
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  const m = new THREE.PointsMaterial({ color: '#ffffff', size: 2.4, sizeAttenuation: false, transparent: true, opacity: 0.85, fog: false, depthWrite: false });
  const pts = new THREE.Points(g, m);
  pts.frustumCulled = false;
  return pts;
}

function buildCloud(seed: number): THREE.BufferGeometry {
  const parts: THREE.BufferGeometry[] = [];
  const blobs = 4 + Math.floor(hash2(seed, 1) * 4);
  for (let k = 0; k < blobs; k++) {
    const r = 9 + hash2(seed, 10 + k) * 12;
    const g = new THREE.IcosahedronGeometry(r, 0);
    const m = new THREE.Matrix4().makeScale(1.7, 0.75, 1.15).setPosition((hash2(seed, 20 + k) - 0.5) * 60, (hash2(seed, 30 + k) - 0.5) * 6, (hash2(seed, 40 + k) - 0.5) * 24);
    g.applyMatrix4(m);
    parts.push(g);
  }
  const merged = mergeGeometries(parts, false);
  if (!merged) throw new Error('nuvem vazia');
  for (const p of parts) p.dispose();
  return merged;
}

export class Sky {
  /** Domo, estrelas e nuvens: gira pelo rumo absoluto. */
  readonly group = new THREE.Group();
  readonly sun: THREE.DirectionalLight;
  readonly hemi: THREE.HemisphereLight;
  readonly fog = new THREE.FogExp2('#ffffff', 0.002);
  /** Direção do sol no referencial local (atualizada por quadro). */
  readonly sunDirLocal = new THREE.Vector3(0, 1, 0);
  private readonly sunDirAbs = new THREE.Vector3(0, 1, 0);
  private readonly dome: THREE.Mesh<THREE.SphereGeometry, THREE.ShaderMaterial>;
  private readonly stars: THREE.Points;
  private readonly clouds = new THREE.Group();
  private readonly cloudMaterial: THREE.MeshStandardMaterial;
  private readonly pmrem: THREE.PMREMGenerator;
  private envTarget: THREE.WebGLRenderTarget | null = null;
  private paletteKey = '';
  private sunSetup: SunSetup | null = null;
  exposure = 1;
  private readonly target = new THREE.Object3D();

  constructor(renderer: THREE.WebGLRenderer, private readonly scene: THREE.Scene) {
    const mat = new THREE.ShaderMaterial({
      vertexShader: SKY_VERT, fragmentShader: SKY_FRAG, side: THREE.BackSide, depthWrite: false, fog: false,
      uniforms: {
        uTop: { value: new THREE.Color('#1b6fd6') }, uMid: { value: new THREE.Color('#4fa6ef') }, uHorizon: { value: new THREE.Color('#bfe6ff') },
        uSunDir: { value: new THREE.Vector3(0, 1, 0) }, uSunColor: { value: new THREE.Color('#fff3a0') },
        uSunSize: { value: 0.03 }, uHalo: { value: 9 }, uGlow: { value: 0.3 }, uDisc: { value: 6 },
      },
    });
    this.dome = new THREE.Mesh(new THREE.SphereGeometry(SKY_RADIUS, 40, 20), mat);
    this.dome.renderOrder = -10;
    this.dome.frustumCulled = false;
    this.group.add(this.dome);
    this.stars = buildStars();
    this.group.add(this.stars);
    this.cloudMaterial = new THREE.MeshStandardMaterial({ color: '#ffffff', flatShading: true, roughness: 1, emissive: '#ffffff', emissiveIntensity: 0.12 });
    for (let k = 0; k < 7; k++) {
      const mesh = new THREE.Mesh(buildCloud(k * 17 + 3), this.cloudMaterial);
      const a = (k / 7) * Math.PI * 2 + hash2(k, 99) * 0.6;
      const r = 420 + hash2(k, 5) * 320;
      mesh.position.set(Math.cos(a) * r, 130 + hash2(k, 6) * 110, Math.sin(a) * r);
      mesh.rotation.y = hash2(k, 7) * Math.PI;
      const s = 1.1 + hash2(k, 8) * 1.2;
      mesh.scale.set(s, s, s);
      mesh.frustumCulled = false;
      this.clouds.add(mesh);
    }
    this.group.add(this.clouds);
    scene.add(this.group);

    this.sun = new THREE.DirectionalLight('#ffffff', 3);
    this.sun.castShadow = true;
    const cam = this.sun.shadow.camera;
    cam.left = -80; cam.right = 80; cam.top = 80; cam.bottom = -80; cam.near = 1; cam.far = 600;
    cam.updateProjectionMatrix(); // o three não recalcula sozinho depois de mudar os limites
    this.sun.shadow.bias = -0.00025;
    this.sun.shadow.normalBias = 0.12;
    this.sun.shadow.radius = 2;
    this.target.position.set(0, 0, -30);
    scene.add(this.target);
    this.sun.target = this.target;
    scene.add(this.sun);
    this.hemi = new THREE.HemisphereLight('#bfe6ff', '#3a6a2a', 0.9);
    scene.add(this.hemi);
    scene.fog = this.fog;
    this.pmrem = new THREE.PMREMGenerator(renderer);
  }

  /** Tamanho do mapa de sombra por qualidade (0 = sem sombra). */
  setQuality(q: Quality): void {
    const size = q === 'high' ? 2048 : q === 'medium' ? 1024 : 0;
    this.sun.castShadow = size > 0;
    if (size > 0 && this.sun.shadow.mapSize.x !== size) {
      this.sun.shadow.mapSize.set(size, size);
      if (this.sun.shadow.map) { this.sun.shadow.map.dispose(); this.sun.shadow.map = null; }
    }
    this.fog.density = q === 'low' ? 0.0032 : q === 'medium' ? 0.0024 : 0.0019;
    if (this.paletteKey.endsWith(':night')) this.fog.density *= 1.35;
  }

  setPalette(p: Palette, time: TimeOfDay, key: string): void {
    if (key === this.paletteKey) return;
    this.paletteKey = key;
    const s = sunSetup(time, p);
    this.sunSetup = s;
    this.sunDirAbs.copy(s.dir);
    const u = this.dome.material.uniforms;
    (u.uTop.value as THREE.Color).set(p.sky[0]);
    (u.uMid.value as THREE.Color).set(p.sky[1]);
    (u.uHorizon.value as THREE.Color).set(p.sky[2]);
    (u.uSunDir.value as THREE.Vector3).copy(s.dir);
    (u.uSunColor.value as THREE.Color).set(s.color);
    u.uSunSize.value = s.size; u.uHalo.value = s.halo; u.uGlow.value = s.glow; u.uDisc.value = s.disc;
    this.stars.visible = p.stars;
    this.clouds.visible = time !== 'night';
    this.cloudMaterial.color.set(time === 'dusk' ? '#ffd2b0' : '#ffffff');
    this.cloudMaterial.emissive.set(time === 'dusk' ? '#ff9a6a' : '#ffffff');
    this.cloudMaterial.emissiveIntensity = time === 'dusk' ? 0.25 : 0.12;
    this.sun.color.set(s.color);
    this.sun.intensity = s.intensity;
    this.hemi.color.set(time === 'night' ? '#6f86c8' : p.sky[1]);
    this.hemi.groundColor.set(p.grassDark);
    // Ambiente fraco de propósito: é o contraste com o sol que faz as sombras lerem.
    this.hemi.intensity = time === 'night' ? 1.35 : time === 'dusk' ? 0.42 : 0.72;
    this.fog.color.set(p.fog);
    this.exposure = s.exposure;
    this.scene.environmentIntensity = time === 'night' ? 0.9 : time === 'dusk' ? 0.3 : 0.48;
    this.rebuildEnvironment();
  }

  private rebuildEnvironment(): void {
    if (this.envTarget) { this.envTarget.dispose(); this.envTarget = null; }
    const envScene = new THREE.Scene();
    const dome = new THREE.Mesh(this.dome.geometry, this.dome.material);
    envScene.add(dome);
    this.envTarget = this.pmrem.fromScene(envScene, 0.02, 1, SKY_RADIUS * 1.5);
    this.scene.environment = this.envTarget.texture;
  }

  /** Gira o céu e a luz pelo rumo absoluto; anima nuvens e a sombra que segue o carro. */
  update(absHeading: number, time: number): void {
    this.group.rotation.y = absHeading;
    this.clouds.rotation.y = time * 0.004;
    const d = this.sunDirAbs;
    const c = Math.cos(absHeading); const s = Math.sin(absHeading);
    this.sunDirLocal.set(d.x * c + d.z * s, d.y, -d.x * s + d.z * c);
    this.sun.position.copy(this.sunDirLocal).multiplyScalar(260).add(this.target.position);
  }

  get isNight(): boolean { return this.sunSetup !== null && this.sunSetup.intensity < 1; }

  dispose(): void {
    this.dome.geometry.dispose(); this.dome.material.dispose();
    this.stars.geometry.dispose(); (this.stars.material as THREE.Material).dispose();
    for (const c of this.clouds.children) if (c instanceof THREE.Mesh) c.geometry.dispose();
    this.cloudMaterial.dispose();
    if (this.envTarget) this.envTarget.dispose();
    this.pmrem.dispose();
    if (this.sun.shadow.map) this.sun.shadow.map.dispose();
  }
}

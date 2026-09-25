// Carros: esportivo low-poly por "loft" de seções (capô baixo e longo, para-brisa inclinado,
// teto curto, traseira alta com aerofólio), para-lamas largos com as rodas visíveis nos
// cantos, vidro escuro com reflexo, faróis/lanternas emissivas (freio mais forte), chama de
// nitro em billboard animado, sombra de contato e etiqueta de nome. Tudo instanciado: um draw
// call por peça para os 20 carros. Pose por viewport via locateOnFrame; a animação (giro de
// roda, rolagem, mergulho) é por carro, uma vez por quadro.
import * as THREE from 'three';
import { carDef } from '../core/data/cars';
import { NITRO_DURATION_TICKS } from '../core/constants';
import type { RaceState, Track } from '../core/types';
import type { RenderFrame } from '../game/contracts';
import { hash2 } from './noise';
import { locateOnFrame, type FramePoint, type RoadFrame } from './roadframe';
import { blobTexture, canvas2d, labelTexture } from './textures';
import { zToMeters } from './units';

const MAX_CARS = 20;
const WHEEL_RADIUS = 0.33;
const WHEEL_X = 0.86;
const WHEEL_Z = 1.42;
const BODY_DARK = '#141518';
const WHITE = '#ffffff';

/** Seção da carroceria em z: meia largura, altura do topo e do fundo (carro de 4,4 × 1,9 × 1,2 m). */
type Station = [z: number, hw: number, top: number, bottom: number];

// Cunha: nariz baixo (0,44 m), capô subindo até o cowl (0,72), deck alto (0,98). Nas estações dos
// eixos o fundo sobe (0,46) e a lateral alarga: é o arco da roda — a roda fica visível por baixo.
const STATIONS: Station[] = [
  [-2.20, 0.62, 0.44, 0.28], [-2.00, 0.80, 0.50, 0.20], [-1.72, 0.86, 0.56, 0.34], [-1.42, 0.90, 0.60, 0.46],
  [-1.10, 0.86, 0.64, 0.30], [-0.60, 0.84, 0.72, 0.16], [0.20, 0.84, 0.76, 0.16], [0.95, 0.86, 0.80, 0.30],
  [1.42, 0.90, 0.88, 0.46], [1.78, 0.86, 0.96, 0.32], [2.08, 0.80, 0.98, 0.26], [2.20, 0.70, 0.92, 0.34],
];

/** Anel de 16 pontos (sentido horário visto de frente), com saia, ombro e topo levemente abaulado. */
function ring(hw: number, top: number, bottom: number): Array<[number, number]> {
  const h = top - bottom;
  const y1 = bottom + h * 0.15; const y2 = bottom + h * 0.45; const y3 = top - h * 0.25; const y4 = top - h * 0.06;
  const right: Array<[number, number]> = [[hw, bottom + 0.02], [hw + 0.02, y1], [hw, y2], [hw * 0.99, y3], [hw * 0.9, y4], [hw * 0.62, top], [0, top + 0.015]];
  const left = right.slice(0, 6).reverse().map(([x, y]) => [-x, y] as [number, number]);
  return [...right, ...left, [-hw * 0.5, bottom - 0.02], [0, bottom - 0.02], [hw * 0.5, bottom - 0.02]];
}

/** Loft entre anéis: faces planas, cor por vértice (escuro abaixo de `darkBelow`). */
function loft(rings: Array<Array<[number, number]>>, zs: number[], darkBelow: number, capFront: boolean, capBack: boolean): THREE.BufferGeometry {
  const pos: number[] = []; const col: number[] = [];
  const white = new THREE.Color(WHITE); const dark = new THREE.Color(BODY_DARK);
  const push = (x: number, y: number, z: number) => { pos.push(x, y, z); const c = y < darkBelow ? dark : white; col.push(c.r, c.g, c.b); };
  const K = rings[0].length;
  for (let i = 0; i < rings.length - 1; i++) {
    const a = rings[i]; const b = rings[i + 1];
    for (let k = 0; k < K; k++) {
      const k1 = (k + 1) % K;
      push(a[k][0], a[k][1], zs[i]); push(a[k1][0], a[k1][1], zs[i]); push(b[k][0], b[k][1], zs[i + 1]);
      push(a[k1][0], a[k1][1], zs[i]); push(b[k1][0], b[k1][1], zs[i + 1]); push(b[k][0], b[k][1], zs[i + 1]);
    }
  }
  const cap = (r: Array<Array<number>>, z: number, flip: boolean) => {
    let cx = 0; let cy = 0;
    for (const [x, y] of r) { cx += x; cy += y; }
    cx /= r.length; cy /= r.length;
    for (let k = 0; k < r.length; k++) {
      const k1 = (k + 1) % r.length;
      if (flip) { push(cx, cy, z); push(r[k1][0], r[k1][1], z); push(r[k][0], r[k][1], z); }
      else { push(cx, cy, z); push(r[k][0], r[k][1], z); push(r[k1][0], r[k1][1], z); }
    }
  };
  if (capFront) cap(rings[0], zs[0], false);
  if (capBack) cap(rings[rings.length - 1], zs[zs.length - 1], true);
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
  g.computeVertexNormals();
  return g;
}

function paintGeo(g: THREE.BufferGeometry, color: string): THREE.BufferGeometry {
  const c = new THREE.Color(color); const n = g.attributes.position.count; const arr = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) { arr[i * 3] = c.r; arr[i * 3 + 1] = c.g; arr[i * 3 + 2] = c.b; }
  g.setAttribute('color', new THREE.BufferAttribute(arr, 3));
  g.deleteAttribute('uv');
  return g;
}

function coloredBox(w: number, h: number, d: number, x: number, y: number, z: number, color: string): THREE.BufferGeometry {
  return paintGeo(new THREE.BoxGeometry(w, h, d).toNonIndexed().translate(x, y, z), color);
}

function mergeColored(parts: THREE.BufferGeometry[]): THREE.BufferGeometry {
  let total = 0;
  for (const p of parts) total += p.attributes.position.count;
  const pos = new Float32Array(total * 3); const col = new Float32Array(total * 3);
  let o = 0;
  for (const p of parts) {
    pos.set(p.attributes.position.array as Float32Array, o * 3);
    col.set(p.attributes.color.array as Float32Array, o * 3);
    o += p.attributes.position.count;
    p.dispose();
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  g.setAttribute('color', new THREE.BufferAttribute(col, 3));
  g.computeVertexNormals();
  return g;
}

/** Carroceria (cor da instância) + peças fixas: para-lamas, teto, aerofólio, difusor, splitter, retrovisores, escapes. */
function buildBody(): THREE.BufferGeometry {
  const rings = STATIONS.map(([, hw, top, bottom]) => ring(hw, top, bottom));
  const zs = STATIONS.map(([z]) => z);
  const parts = [loft(rings, zs, 0.21, true, true)];
  parts.push(coloredBox(1.26, 0.05, 0.7, 0, 1.18, 0.42, WHITE)); // teto
  parts.push(coloredBox(1.64, 0.05, 0.30, 0, 1.12, 1.98, WHITE)); // aerofólio
  parts.push(coloredBox(0.06, 0.18, 0.16, -0.52, 1.03, 1.98, BODY_DARK), coloredBox(0.06, 0.18, 0.16, 0.52, 1.03, 1.98, BODY_DARK));
  parts.push(coloredBox(1.44, 0.16, 0.22, 0, 0.22, 2.16, BODY_DARK)); // difusor
  parts.push(coloredBox(1.5, 0.06, 0.3, 0, 0.19, -2.1, BODY_DARK)); // splitter
  parts.push(coloredBox(0.14, 0.08, 0.18, -0.92, 0.84, -0.4, WHITE), coloredBox(0.14, 0.08, 0.18, 0.92, 0.84, -0.4, WHITE)); // retrovisores
  parts.push(coloredBox(0.18, 0.1, 0.14, -0.45, 0.3, 2.24, '#9a9ea6'), coloredBox(0.18, 0.1, 0.14, 0.45, 0.3, 2.24, '#9a9ea6')); // escapes
  return mergeColored(parts);
}

/** Cabine de vidro: para-brisa inclinado, teto curto, vidro traseiro e janelas laterais. */
function buildGlass(): THREE.BufferGeometry {
  const st: Array<[number, number, number, number, number]> = [
    [-0.58, 0.78, 0.73, 0.76, 0.75], [0.10, 0.74, 0.77, 0.62, 1.16], [0.75, 0.74, 0.79, 0.62, 1.16], [1.50, 0.78, 0.89, 0.72, 0.91],
  ];
  const rings = st.map(([, hb, yb, ht, yt]) => [[hb, yb], [ht, yt], [-ht, yt], [-hb, yb]] as Array<[number, number]>);
  return loft(rings, st.map(([z]) => z), -1, false, false);
}

/** Roda: pneu escuro, aro claro com cinco vãos escuros (mostra o giro). */
function buildWheel(): THREE.BufferGeometry {
  const parts = [paintGeo(new THREE.CylinderGeometry(WHEEL_RADIUS, WHEEL_RADIUS, 0.26, 12).toNonIndexed().rotateZ(Math.PI / 2), '#141416')];
  parts.push(paintGeo(new THREE.CylinderGeometry(0.22, 0.22, 0.27, 12).toNonIndexed().rotateZ(Math.PI / 2), '#d8dce2'));
  for (let k = 0; k < 5; k++) {
    parts.push(paintGeo(new THREE.BoxGeometry(0.29, 0.13, 0.05).toNonIndexed().translate(0, 0.13, 0).rotateX((k / 5) * Math.PI * 2), '#2a2c30'));
  }
  return mergeColored(parts);
}

/** Atlas de 4 quadros da chama (base embaixo de cada quadro, ponta em cima): gaussiana que afina, núcleo branco → azul. */
function flameTexture(): THREE.CanvasTexture {
  const W = 64; const H = 512; const F = 128;
  const [c, ctx] = canvas2d(W, H);
  const img = ctx.createImageData(W, H);
  const d = img.data;
  for (let f = 0; f < 4; f++) {
    const len = 0.78 + hash2(f, 1) * 0.2;
    const sig0 = 0.21 + hash2(f, 2) * 0.05;
    for (let row = 0; row < F; row++) {
      const v = (row + 0.5) / F; // 0 = base (embaixo do quadro)
      const t = v / len;
      const y = H - 1 - (f * F + row); // linha do canvas (v=0 no fundo, flipY)
      for (let px = 0; px < W; px++) {
        const k = (y * W + px) * 4;
        if (t >= 1) { d[k + 3] = 0; continue; }
        const u = (px + 0.5) / W - 0.5 + Math.sin(t * 11 + f * 2.1) * 0.03 * t;
        const sigma = sig0 * (1 - 0.7 * t);
        const g = Math.exp(-(u * u) / (2 * sigma * sigma));
        const profile = t < 0.08 ? t / 0.08 : Math.pow(1 - (t - 0.08) / 0.92, 0.9);
        const a = Math.min(1, g * profile * 1.15);
        const core = g * g * (1 - t * 0.85);
        d[k] = Math.round(90 + (255 - 90) * core); d[k + 1] = Math.round(175 + (255 - 175) * core); d[k + 2] = 255; d[k + 3] = Math.round(a * 255);
      }
    }
  }
  ctx.putImageData(img, 0, 0);
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

/** Billboard axial: o plano gira em torno do eixo da chama (+Z da instância) para encarar a câmera. */
const FLAME_VERT = /* glsl */ `
varying vec2 vUv; varying float vFacing;
void main() {
  mat4 m = modelMatrix * instanceMatrix;
  vec3 origin = (m * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
  vec3 axisRaw = mat3(m) * vec3(0.0, 0.0, 1.0);
  float len = length(axisRaw);
  vec3 axis = axisRaw / max(len, 1e-4);
  float wid = length(mat3(m) * vec3(1.0, 0.0, 0.0));
  vec3 toCam = normalize(cameraPosition - origin);
  vec3 crossv = cross(axis, toCam);
  float cl = length(crossv);
  vec3 side = cl > 1e-3 ? crossv / cl : normalize(cross(vec3(0.0, 1.0, 0.0), toCam));
  // De frente/de trás o plano axial colapsa numa linha: mistura com um billboard esférico curto.
  float facing = abs(dot(axis, toCam));
  float k = facing * facing;
  vec3 upv = normalize(mix(axis, normalize(cross(toCam, side)), k));
  // Visto de trás (câmera de perseguição) o brilho é curto, para não engolir as lanternas.
  float lenEff = mix(len, wid * 1.15, k);
  vec3 p = origin + upv * ((uv.y - 0.5 * k) * lenEff) + side * ((uv.x - 0.5) * wid * (1.0 + 0.25 * k));
  vUv = uv; vFacing = k;
  gl_Position = projectionMatrix * viewMatrix * vec4(p, 1.0);
}`;

const FLAME_FRAG = /* glsl */ `
uniform sampler2D uMap; uniform float uTime;
varying vec2 vUv; varying float vFacing;
void main() {
  float frame = floor(mod(uTime * 22.0, 4.0));
  vec4 c = texture2D(uMap, vec2(vUv.x, (vUv.y + frame) * 0.25));
  float gain = mix(4.0, 1.6, vFacing); // de trás as duas chamas se somam (aditivo): menos ganho
  gl_FragColor = vec4(c.rgb * gain * c.a, c.a);
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
}`;

interface CarAnim {
  spin: number; yaw: number; roll: number; pitch: number; bob: number;
  brake: boolean; prevSpeed: number; nitro: number;
}

export class Cars {
  readonly group = new THREE.Group();
  private readonly body: THREE.InstancedMesh;
  private readonly glass: THREE.InstancedMesh;
  private readonly wheels: THREE.InstancedMesh;
  private readonly heads: THREE.InstancedMesh;
  private readonly tail: THREE.InstancedMesh;
  private readonly tailBrake: THREE.InstancedMesh;
  private readonly blob: THREE.InstancedMesh;
  private readonly flames: THREE.InstancedMesh;
  private readonly flameMaterial: THREE.ShaderMaterial;
  private readonly bodyMaterial: THREE.MeshStandardMaterial;
  private readonly selfLight = { value: 0.22 };
  private readonly headMaterial: THREE.MeshStandardMaterial;
  /** Etiqueta por assento (0..3); denso de propósito — um array esparso quebra o `for…of`. */
  private readonly labels: Array<THREE.Sprite | null> = [null, null, null, null];
  private readonly labelKeys = ['', '', '', ''];
  private readonly spots: THREE.SpotLight[] = [];
  private readonly anims: CarAnim[] = [];
  private readonly colors: THREE.Color[] = [];
  private readonly dummy = new THREE.Object3D();
  private readonly wheelDummy = new THREE.Object3D();
  private readonly m = new THREE.Matrix4();
  private readonly mw = new THREE.Matrix4();
  private readonly pt: FramePoint = { x: 0, y: 0, z: 0, heading: 0 };
  private lastTime = -1;

  private get instanced(): THREE.InstancedMesh[] {
    return [this.body, this.glass, this.wheels, this.heads, this.tail, this.tailBrake, this.blob, this.flames];
  }

  constructor(scene: THREE.Scene) {
    // Pintura: a cor base domina (pouco metal, env map moderado); o clareamento vem da luz.
    this.bodyMaterial = new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 0.45, metalness: 0.1, envMapIntensity: 0.35 });
    // Uma fração da própria cor como emissivo: o lado na sombra (o que a câmera de perseguição vê)
    // continua lendo "branco"/"vermelho" em vez do azul do céu. É estilização, não física.
    this.bodyMaterial.onBeforeCompile = (shader) => {
      shader.uniforms.uSelfLight = this.selfLight;
      shader.fragmentShader = shader.fragmentShader
        .replace('#include <common>', '#include <common>\nuniform float uSelfLight;')
        .replace('#include <emissivemap_fragment>', '#include <emissivemap_fragment>\ntotalEmissiveRadiance += diffuseColor.rgb * uSelfLight;');
    };
    this.body = new THREE.InstancedMesh(buildBody(), this.bodyMaterial, MAX_CARS);
    this.body.castShadow = true; this.body.receiveShadow = true;
    const glassMat = new THREE.MeshStandardMaterial({ color: '#223448', flatShading: true, metalness: 0.75, roughness: 0.18, envMapIntensity: 1.4 });
    this.glass = new THREE.InstancedMesh(buildGlass(), glassMat, MAX_CARS);
    this.glass.castShadow = true;
    const wheelMat = new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, metalness: 0.2, roughness: 0.65 });
    this.wheels = new THREE.InstancedMesh(buildWheel(), wheelMat, MAX_CARS * 4);
    this.wheels.castShadow = true;
    this.headMaterial = new THREE.MeshStandardMaterial({ color: '#fff6d8', emissive: '#fff1c4', emissiveIntensity: 0.4, roughness: 0.3 });
    const headGeo = mergeColored([coloredBox(0.4, 0.11, 0.08, -0.44, 0.4, -2.21, WHITE), coloredBox(0.4, 0.11, 0.08, 0.44, 0.4, -2.21, WHITE)]);
    headGeo.deleteAttribute('color');
    this.heads = new THREE.InstancedMesh(headGeo, this.headMaterial, MAX_CARS);
    const tailGeo = mergeColored([coloredBox(0.46, 0.1, 0.06, -0.46, 0.86, 2.21, WHITE), coloredBox(0.46, 0.1, 0.06, 0.46, 0.86, 2.21, WHITE)]);
    tailGeo.deleteAttribute('color');
    this.tail = new THREE.InstancedMesh(tailGeo, new THREE.MeshStandardMaterial({ color: '#c81e1e', emissive: '#ff1a1a', emissiveIntensity: 0.9, roughness: 0.3 }), MAX_CARS);
    this.tailBrake = new THREE.InstancedMesh(tailGeo, new THREE.MeshStandardMaterial({ color: '#ff2a2a', emissive: '#ff2020', emissiveIntensity: 3.2, roughness: 0.3 }), MAX_CARS);
    this.blob = new THREE.InstancedMesh(new THREE.PlaneGeometry(2.7, 5.0).rotateX(-Math.PI / 2).translate(0, 0.02, 0), new THREE.MeshBasicMaterial({ map: blobTexture(), transparent: true, depthWrite: false }), MAX_CARS);
    this.blob.renderOrder = 1;
    this.flameMaterial = new THREE.ShaderMaterial({
      vertexShader: FLAME_VERT, fragmentShader: FLAME_FRAG, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
      uniforms: { uMap: { value: flameTexture() }, uTime: { value: 0 } },
    });
    this.flames = new THREE.InstancedMesh(new THREE.PlaneGeometry(1, 1), this.flameMaterial, MAX_CARS * 2);
    this.flames.renderOrder = 4;
    for (const im of this.instanced) {
      im.frustumCulled = false;
      im.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      im.count = 0;
      this.group.add(im);
    }
    for (let i = 0; i < MAX_CARS; i++) {
      this.anims.push({ spin: 0, yaw: 0, roll: 0, pitch: 0, bob: 0, brake: false, prevSpeed: 0, nitro: 0 });
      this.colors.push(new THREE.Color(WHITE));
    }
    // Faróis do carro do viewport (sempre na origem, olhando −Z): dois SpotLights fixos.
    for (const sx of [-0.55, 0.55]) {
      const spot = new THREE.SpotLight('#fff3d0', 230, 110, 0.48, 0.6, 1.3);
      spot.position.set(sx, 0.6, -2.0);
      spot.target.position.set(sx * 2.2, -0.6, -45);
      spot.visible = false;
      scene.add(spot, spot.target);
      this.spots.push(spot);
    }
    scene.add(this.group);
  }

  /** `light` = palette.light (1 dia, 0,7 entardecer, 0,32 noite). */
  setLight(light: number): void {
    const night = light < 0.5;
    this.selfLight.value = 0.05 + 0.17 * light;
    this.headMaterial.emissiveIntensity = night ? 2.6 : light < 0.8 ? 0.9 : 0.4;
    for (const s of this.spots) { s.visible = light < 0.8; s.intensity = night ? 230 : 110; }
  }

  /** Animação por carro (uma vez por quadro, independente do viewport). */
  update(frame: RenderFrame): void {
    const dt = this.lastTime < 0 ? 1 / 60 : Math.min(0.1, Math.max(0, frame.time - this.lastTime));
    this.lastTime = frame.time;
    const cars = frame.state.cars;
    const segs = frame.track.segments;
    for (let i = 0; i < cars.length && i < MAX_CARS; i++) {
      const c = cars[i];
      const a = this.anims[i];
      const def = carDef(c.carId);
      const sf = Math.min(1.2, c.speed / c.stats.topSpeed);
      a.spin += zToMeters(c.speed) * dt / WHEEL_RADIUS;
      const decel = (a.prevSpeed - c.speed) / dt;
      a.brake = c.speed > 200 && decel > c.stats.brake * 0.55 && c.collisionCooldown === 0;
      a.prevSpeed = c.speed;
      const seg = segs[Math.floor((((c.z % frame.track.length) + frame.track.length) % frame.track.length) / 200) % segs.length];
      const k = 1 - Math.exp(-dt * 8);
      a.yaw += (c.steerPose * 0.08 * Math.min(1, sf + 0.3) - a.yaw) * k;
      const rollTarget = (seg.curve * 0.045 * sf * sf + c.steerPose * 0.03 * sf);
      a.roll += (rollTarget - a.roll) * k;
      const pitchTarget = a.brake ? -0.045 : (c.nitroTicks > 0 ? 0.03 : 0);
      a.pitch += (pitchTarget - a.pitch) * k;
      a.bob = c.skidTicks > 0 && c.speed > 200 ? 1 : 0;
      a.nitro = c.nitroTicks > 0 ? c.nitroTicks / NITRO_DURATION_TICKS : 0;
      this.colors[i].set(def.color);
    }
    this.flameMaterial.uniforms.uTime.value = frame.time;
  }

  /** Posiciona os carros no referencial do viewport (`ownIndex` = carro do viewport). */
  pose(rf: RoadFrame, state: RaceState, track: Track, ownIndex: number, viewportSeats: ReadonlyArray<{ seat: number; name: string; color: string; carIndex: number }>, time: number): void {
    const cars = state.cars;
    const d = this.dummy;
    const w = this.wheelDummy;
    let n = 0; let nWheels = 0; let nTail = 0; let nBrake = 0; let nFlames = 0;
    for (const s of this.labels) if (s) s.visible = false;
    for (let i = 0; i < cars.length && i < MAX_CARS; i++) {
      const c = cars[i];
      if (!locateOnFrame(rf, track, c.z, c.x, this.pt)) continue;
      // Carro atrás do carro do viewport ficaria entre ele e a câmera (a 8 m), enorme e
      // cortado: só aparece quando o centro dele passa 2,2 m atrás do centro do jogador.
      if (i !== ownIndex && this.pt.z > 2.2) continue;
      const a = this.anims[i];
      const bobY = a.bob ? Math.sin(time * 71 + i) * 0.025 : 0;
      const bobR = a.bob ? Math.sin(time * 57 + i * 2) * 0.02 : 0;
      d.position.set(this.pt.x, this.pt.y + bobY, this.pt.z);
      d.rotation.set(a.pitch, -(this.pt.heading + a.yaw), a.roll + bobR, 'YXZ');
      d.scale.set(1, 1, 1);
      d.updateMatrix();
      const idx = n++;
      this.m.copy(d.matrix);
      this.body.setMatrixAt(idx, this.m);
      this.body.setColorAt(idx, this.colors[i]);
      this.glass.setMatrixAt(idx, this.m);
      this.heads.setMatrixAt(idx, this.m);
      if (a.brake) this.tailBrake.setMatrixAt(nBrake++, this.m); else this.tail.setMatrixAt(nTail++, this.m);
      // Sombra de contato: no chão, sem rolagem/mergulho.
      d.rotation.set(0, -(this.pt.heading + a.yaw), 0, 'YXZ');
      d.position.y = this.pt.y;
      d.updateMatrix();
      this.blob.setMatrixAt(idx, d.matrix);
      // Rodas: matriz do carro × deslocamento × giro (dianteiras viram com o volante).
      for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
        w.position.set(sx * WHEEL_X, WHEEL_RADIUS, sz * WHEEL_Z);
        w.rotation.set(a.spin, sz < 0 ? c.steerPose * 0.32 : 0, 0, 'YXZ');
        w.scale.set(1, 1, 1);
        w.updateMatrix();
        this.mw.multiplyMatrices(this.m, w.matrix);
        this.wheels.setMatrixAt(nWheels++, this.mw);
      }
      // Chama do nitro: origem no escape, +Z para trás = comprimento, escala x = largura.
      if (a.nitro > 0) {
        const pulse = 0.85 + 0.3 * Math.sin(time * 41 + i * 1.7) + 0.25 * hash2(Math.floor(time * 28), i);
        for (const sx of [-0.45, 0.45]) {
          w.position.set(sx, 0.32, 2.28);
          w.rotation.set(0, 0, 0);
          w.scale.set(0.7, 1, 2.3 * pulse * (0.55 + 0.45 * a.nitro));
          w.updateMatrix();
          this.mw.multiplyMatrices(this.m, w.matrix);
          this.flames.setMatrixAt(nFlames++, this.mw);
        }
      }
      // Etiqueta de jogador local visto de outro viewport.
      if (c.seat >= 0 && c.seat < 4 && i !== ownIndex) {
        const vp = viewportSeats.find((v) => v.seat === c.seat);
        if (vp) {
          const label = this.label(c.seat, vp.name, vp.color);
          label.position.set(this.pt.x, this.pt.y + 2.1, this.pt.z);
          label.visible = true;
        }
      }
    }
    this.body.count = n; this.glass.count = n; this.heads.count = n; this.blob.count = n;
    this.wheels.count = nWheels; this.tail.count = nTail; this.tailBrake.count = nBrake; this.flames.count = nFlames;
    for (const im of this.instanced) {
      im.visible = im.count > 0;
      if (im.count > 0) im.instanceMatrix.needsUpdate = true;
    }
    if (this.body.instanceColor) this.body.instanceColor.needsUpdate = true;
  }

  /** Esconde todos os carros (fundo dos menus). */
  hide(): void {
    for (const im of this.instanced) { im.count = 0; im.visible = false; }
    for (const s of this.labels) if (s) s.visible = false;
  }

  private label(seat: number, name: string, color: string): THREE.Sprite {
    const key = `${name}|${color}`;
    let sprite = this.labels[seat];
    if (!sprite) {
      sprite = new THREE.Sprite(new THREE.SpriteMaterial({ transparent: true, depthTest: false, fog: false }));
      sprite.scale.set(3.2, 1.0, 1);
      sprite.renderOrder = 5;
      this.labels[seat] = sprite;
      this.group.add(sprite);
    }
    if (this.labelKeys[seat] !== key) {
      const mat = sprite.material;
      if (mat.map) mat.map.dispose();
      mat.map = labelTexture(name, color);
      mat.needsUpdate = true;
      this.labelKeys[seat] = key;
    }
    return sprite;
  }

  dispose(): void {
    for (const im of this.instanced) { im.geometry.dispose(); (im.material as THREE.Material).dispose(); im.dispose(); }
    (this.flameMaterial.uniforms.uMap.value as THREE.Texture).dispose();
    for (const s of this.labels) if (s) { if (s.material.map) s.material.map.dispose(); s.material.dispose(); }
    for (const s of this.spots) s.dispose();
  }
}

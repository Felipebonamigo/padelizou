// Carros: carroceria esportiva low-poly por "loft" de seções (não é uma caixa), vidro
// metálico refletindo o env map, rodas com raios que giram, aerofólio, faróis e lanternas
// emissivas (mais fortes no freio), chama de nitro, sombra de contato e etiqueta de nome.
// Tudo instanciado: um draw call por peça para os 20 carros. Pose por viewport via
// locateOnFrame; a animação (giro de roda, rolagem, mergulho) é por carro, uma vez por quadro.
import * as THREE from 'three';
import { carDef } from '../core/data/cars';
import { NITRO_DURATION_TICKS } from '../core/constants';
import type { RaceState, Track } from '../core/types';
import type { RenderFrame } from '../game/contracts';
import { hash2 } from './noise';
import { locateOnFrame, type FramePoint, type RoadFrame } from './roadframe';
import { blobTexture, labelTexture } from './textures';
import { zToMeters } from './units';

const MAX_CARS = 20;
const WHEEL_RADIUS = 0.34;
const WHEEL_X = 0.97;
const WHEEL_Z = 1.38;
const BODY_DARK = '#15161a';

/** Seção da carroceria em z: meia largura, altura do topo e do fundo. */
type Station = [z: number, hw: number, top: number, bottom: number];

const STATIONS: Station[] = [
  [-2.15, 0.60, 0.48, 0.30], [-1.95, 0.86, 0.56, 0.24], [-1.45, 0.94, 0.66, 0.22], [-0.85, 0.98, 0.76, 0.2],
  [-0.35, 1.0, 0.84, 0.2], [0.55, 1.0, 0.86, 0.2], [1.35, 0.98, 0.88, 0.2], [1.85, 0.94, 0.92, 0.24], [2.10, 0.86, 0.80, 0.34],
];

function ring(hw: number, top: number, bottom: number): Array<[number, number]> {
  return [
    [hw, bottom], [hw + 0.02, 0.36], [hw, 0.5], [hw * 0.985, top - 0.1], [hw * 0.72, top], [0, top + 0.03],
    [-hw * 0.72, top], [-hw * 0.985, top - 0.1], [-hw, 0.5], [-(hw + 0.02), 0.36], [-hw, bottom], [-hw * 0.5, bottom - 0.02],
    [0, bottom - 0.02], [hw * 0.5, bottom - 0.02],
  ];
}

/** Loft entre anéis: faces planas, cor por vértice (escuro abaixo de `darkBelow`). */
function loft(rings: Array<Array<[number, number]>>, zs: number[], darkBelow: number, capFront: boolean, capBack: boolean): THREE.BufferGeometry {
  const pos: number[] = []; const col: number[] = [];
  const white = new THREE.Color('#ffffff'); const dark = new THREE.Color(BODY_DARK);
  const push = (x: number, y: number, z: number) => { pos.push(x, y, z); const c = y < darkBelow ? dark : white; col.push(c.r, c.g, c.b); };
  const K = rings[0].length;
  for (let i = 0; i < rings.length - 1; i++) {
    const a = rings[i]; const b = rings[i + 1];
    for (let k = 0; k < K; k++) {
      const k1 = (k + 1) % K;
      // Anel percorrido no sentido horário visto de frente; z cresce para trás.
      push(a[k][0], a[k][1], zs[i]); push(a[k1][0], a[k1][1], zs[i]); push(b[k][0], b[k][1], zs[i + 1]);
      push(a[k1][0], a[k1][1], zs[i]); push(b[k1][0], b[k1][1], zs[i + 1]); push(b[k][0], b[k][1], zs[i + 1]);
    }
  }
  const cap = (r: Array<[number, number]>, z: number, flip: boolean) => {
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

function coloredBox(w: number, h: number, d: number, x: number, y: number, z: number, color: string): THREE.BufferGeometry {
  const g = new THREE.BoxGeometry(w, h, d).toNonIndexed().translate(x, y, z);
  const c = new THREE.Color(color);
  const n = g.attributes.position.count;
  const arr = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) { arr[i * 3] = c.r; arr[i * 3 + 1] = c.g; arr[i * 3 + 2] = c.b; }
  g.setAttribute('color', new THREE.BufferAttribute(arr, 3));
  g.deleteAttribute('uv');
  return g;
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

function buildBody(): THREE.BufferGeometry {
  const rings = STATIONS.map(([, hw, top, bottom]) => ring(hw, top, bottom));
  const zs = STATIONS.map(([z]) => z);
  const body = loft(rings, zs, 0.44, true, true);
  const parts = [body];
  // Para-lamas sobre as rodas, aerofólio e suportes, difusor.
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) parts.push(coloredBox(0.36, 0.42, 1.05, sx * (WHEEL_X + 0.03), 0.62, sz * WHEEL_Z, BODY_DARK));
  parts.push(coloredBox(1.8, 0.07, 0.4, 0, 1.16, 1.9, BODY_DARK));
  parts.push(coloredBox(0.09, 0.3, 0.22, -0.62, 1.0, 1.92, BODY_DARK), coloredBox(0.09, 0.3, 0.22, 0.62, 1.0, 1.92, BODY_DARK));
  parts.push(coloredBox(0.3, 0.1, 0.5, -0.4, 1.19, 1.9, '#ffffff'), coloredBox(0.3, 0.1, 0.5, 0.4, 1.19, 1.9, '#ffffff'));
  parts.push(coloredBox(1.4, 0.16, 0.3, 0, 0.26, 2.05, BODY_DARK));
  return mergeColored(parts);
}

function buildGlass(): THREE.BufferGeometry {
  const rings: Array<Array<[number, number]>> = [
    [[0.92, 0.84], [0.88, 0.86], [-0.88, 0.86], [-0.92, 0.84]],
    [[0.86, 0.87], [0.7, 1.28], [-0.7, 1.28], [-0.86, 0.87]],
    [[0.86, 0.89], [0.7, 1.28], [-0.7, 1.28], [-0.86, 0.89]],
    [[0.9, 0.91], [0.84, 0.93], [-0.84, 0.93], [-0.9, 0.91]],
  ];
  return loft(rings, [-0.35, 0.3, 0.95, 1.75], -1, false, false);
}

function buildWheel(): THREE.BufferGeometry {
  const tire = new THREE.CylinderGeometry(WHEEL_RADIUS, WHEEL_RADIUS, 0.28, 12).toNonIndexed().rotateZ(Math.PI / 2);
  const paintGeo = (g: THREE.BufferGeometry, color: string) => {
    const c = new THREE.Color(color); const n = g.attributes.position.count; const arr = new Float32Array(n * 3);
    for (let i = 0; i < n; i++) { arr[i * 3] = c.r; arr[i * 3 + 1] = c.g; arr[i * 3 + 2] = c.b; }
    g.setAttribute('color', new THREE.BufferAttribute(arr, 3)); g.deleteAttribute('uv'); return g;
  };
  const parts = [paintGeo(tire, '#1a1a1c')];
  parts.push(paintGeo(new THREE.CylinderGeometry(0.13, 0.13, 0.3, 8).toNonIndexed().rotateZ(Math.PI / 2), '#d8dbe0'));
  for (let k = 0; k < 5; k++) {
    const a = (k / 5) * Math.PI * 2;
    parts.push(paintGeo(new THREE.BoxGeometry(0.3, 0.07, 0.26).toNonIndexed().translate(0, 0.14, 0).rotateX(a), '#c9ccd2'));
  }
  return mergeColored(parts);
}

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
  private readonly bodyMaterial: THREE.MeshStandardMaterial;
  private readonly headMaterial: THREE.MeshStandardMaterial;
  private readonly labels: THREE.Sprite[] = [];
  private readonly labelKeys: string[] = [];
  private readonly spots: THREE.SpotLight[] = [];
  private readonly anims: CarAnim[] = [];
  private readonly colors: THREE.Color[] = [];
  private readonly dummy = new THREE.Object3D();
  private readonly wheelDummy = new THREE.Object3D();
  private readonly m = new THREE.Matrix4();
  private readonly mw = new THREE.Matrix4();
  private readonly pt: FramePoint = { x: 0, y: 0, z: 0, heading: 0 };
  private lastTime = -1;

  constructor(scene: THREE.Scene) {
    this.bodyMaterial = new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, metalness: 0.6, roughness: 0.35, envMapIntensity: 1.2 });
    this.body = new THREE.InstancedMesh(buildBody(), this.bodyMaterial, MAX_CARS);
    this.body.castShadow = true; this.body.receiveShadow = true;
    const glassMat = new THREE.MeshStandardMaterial({ vertexColors: false, color: '#0d151f', flatShading: true, metalness: 0.95, roughness: 0.12, envMapIntensity: 1.6 });
    this.glass = new THREE.InstancedMesh(buildGlass(), glassMat, MAX_CARS);
    this.glass.castShadow = true;
    const wheelMat = new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, metalness: 0.3, roughness: 0.6 });
    this.wheels = new THREE.InstancedMesh(buildWheel(), wheelMat, MAX_CARS * 4);
    this.wheels.castShadow = true;
    this.headMaterial = new THREE.MeshStandardMaterial({ color: '#fff6d8', emissive: '#fff1c4', emissiveIntensity: 0.5, roughness: 0.3 });
    const headGeo = new THREE.BufferGeometry();
    {
      const l = new THREE.BoxGeometry(0.4, 0.14, 0.1).translate(-0.6, 0.55, -2.1);
      const r = new THREE.BoxGeometry(0.4, 0.14, 0.1).translate(0.6, 0.55, -2.1);
      const li = l.toNonIndexed(); const ri = r.toNonIndexed();
      const pos = new Float32Array(li.attributes.position.count * 6);
      pos.set(li.attributes.position.array as Float32Array, 0); pos.set(ri.attributes.position.array as Float32Array, li.attributes.position.count * 3);
      headGeo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
      headGeo.computeVertexNormals();
    }
    this.heads = new THREE.InstancedMesh(headGeo, this.headMaterial, MAX_CARS);
    const tailGeo = new THREE.BoxGeometry(1.5, 0.1, 0.06).translate(0, 0.76, 2.12);
    this.tail = new THREE.InstancedMesh(tailGeo, new THREE.MeshStandardMaterial({ color: '#ff2a2a', emissive: '#ff1a1a', emissiveIntensity: 1.1, roughness: 0.3 }), MAX_CARS);
    this.tailBrake = new THREE.InstancedMesh(tailGeo, new THREE.MeshStandardMaterial({ color: '#ff2a2a', emissive: '#ff2020', emissiveIntensity: 6, roughness: 0.3 }), MAX_CARS);
    const blobTex = blobTexture();
    this.blob = new THREE.InstancedMesh(new THREE.PlaneGeometry(2.9, 5.0).rotateX(-Math.PI / 2).translate(0, 0.02, 0), new THREE.MeshBasicMaterial({ map: blobTex, transparent: true, depthWrite: false }), MAX_CARS);
    this.blob.renderOrder = 1;
    const flameGeo = new THREE.ConeGeometry(0.15, 1.0, 8).rotateX(Math.PI / 2).translate(0, 0, 0.5);
    this.flames = new THREE.InstancedMesh(flameGeo, new THREE.MeshBasicMaterial({ color: '#8fd8ff', transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }), MAX_CARS * 2);
    for (const im of [this.body, this.glass, this.wheels, this.heads, this.tail, this.tailBrake, this.blob, this.flames]) {
      im.frustumCulled = false;
      im.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      im.count = 0;
      this.group.add(im);
    }
    for (let i = 0; i < MAX_CARS; i++) {
      this.anims.push({ spin: 0, yaw: 0, roll: 0, pitch: 0, bob: 0, brake: false, prevSpeed: 0, nitro: 0 });
      this.colors.push(new THREE.Color('#ffffff'));
    }
    // Faróis do carro do viewport (sempre na origem, olhando −Z): dois SpotLights fixos.
    for (const sx of [-0.65, 0.65]) {
      const spot = new THREE.SpotLight('#fff3d0', 140, 90, 0.5, 0.6, 1.3);
      spot.position.set(sx, 0.7, -2.0);
      spot.target.position.set(sx * 2.2, -0.6, -45);
      spot.visible = false;
      scene.add(spot, spot.target);
      this.spots.push(spot);
    }
    scene.add(this.group);
  }

  setNight(night: boolean): void {
    this.headMaterial.emissiveIntensity = night ? 4 : 0.5;
    for (const s of this.spots) s.visible = night;
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
      const sf = Math.min(1.2, c.speed / def.topSpeed);
      a.spin += zToMeters(c.speed) * dt / WHEEL_RADIUS;
      const decel = (a.prevSpeed - c.speed) / dt;
      a.brake = c.speed > 200 && decel > def.brake * 0.55 && c.collisionCooldown === 0;
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
  }

  /** Posiciona os carros no referencial do viewport (`ownIndex` = carro do viewport). */
  pose(rf: RoadFrame, state: RaceState, track: Track, ownIndex: number, viewportSeats: ReadonlyArray<{ seat: number; name: string; color: string; carIndex: number }>, time: number): void {
    const cars = state.cars;
    const d = this.dummy;
    const w = this.wheelDummy;
    let n = 0; let nWheels = 0; let nTail = 0; let nBrake = 0; let nFlames = 0;
    for (const s of this.labels) s.visible = false;
    for (let i = 0; i < cars.length && i < MAX_CARS; i++) {
      const c = cars[i];
      if (!locateOnFrame(rf, track, c.z, c.x, this.pt)) continue;
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
        w.updateMatrix();
        this.mw.multiplyMatrices(this.m, w.matrix);
        this.wheels.setMatrixAt(nWheels++, this.mw);
      }
      if (a.nitro > 0) {
        const pulse = 0.9 + 0.35 * Math.sin(time * 40 + i) + 0.2 * hash2(Math.floor(time * 30), i);
        for (const sx of [-0.42, 0.42]) {
          w.position.set(sx, 0.36, 2.1);
          w.rotation.set(0, 0, 0);
          w.scale.set(1.1, 1.1, 1.6 * pulse * (0.6 + 0.4 * a.nitro));
          w.updateMatrix();
          this.mw.multiplyMatrices(this.m, w.matrix);
          this.flames.setMatrixAt(nFlames++, this.mw);
        }
        w.scale.set(1, 1, 1);
      }
      // Etiqueta de jogador local visto de outro viewport.
      if (c.seat >= 0 && i !== ownIndex) {
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
    for (const im of [this.body, this.glass, this.wheels, this.heads, this.tail, this.tailBrake, this.blob, this.flames]) {
      im.visible = im.count > 0;
      if (im.count > 0) im.instanceMatrix.needsUpdate = true;
    }
    if (this.body.instanceColor) this.body.instanceColor.needsUpdate = true;
  }

  /** Esconde todos os carros (fundo dos menus). */
  hide(): void {
    for (const im of [this.body, this.glass, this.wheels, this.heads, this.tail, this.tailBrake, this.blob, this.flames]) { im.count = 0; im.visible = false; }
    for (const s of this.labels) s.visible = false;
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
    for (const im of [this.body, this.glass, this.wheels, this.heads, this.tail, this.tailBrake, this.blob, this.flames]) {
      im.geometry.dispose(); (im.material as THREE.Material).dispose(); im.dispose();
    }
    for (const s of this.labels) { if (s.material.map) s.material.map.dispose(); s.material.dispose(); }
    for (const s of this.spots) s.dispose();
  }
}

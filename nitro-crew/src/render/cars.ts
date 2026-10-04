// Carros: um modelo procedural por estilo de carroceria (cars/styles/*.ts, 13 estilos), agrupados por
// estilo — cada estilo presente é UMA chamada de desenho para todos os carros dele (InstancedMesh), e
// cada desenho de roda presente é outra. Carroceria, cabine, vidro, cromo, lentes e peças vivem na
// mesma malha e no mesmo material (cars/material.ts): pintura e acento por instância, faixas por
// camada, farol/lanterna emissivos (freio por instância), verniz com o reflexo do céu na qualidade alta.
// Chama de nitro em billboard animado, sombra de contato e etiqueta de nome. Pose por viewport via
// locateOnFrame; a animação (giro de roda, rolagem, mergulho) é por carro, uma vez por quadro.
import * as THREE from 'three';
import { CARS, carDef } from '../core/data/cars';
import { NITRO_DURATION_TICKS } from '../core/constants';
import type { CarBody, RaceState, Track } from '../core/types';
import type { GhostFrame, Quality, RenderFrame } from '../game/contracts';
import { hash2 } from './noise';
import { locateOnFrame, type FramePoint, type RoadFrame } from './roadframe';
import { blobTexture, labelTexture } from './textures';
import { VIP_COLOR, VIP_NAME } from '../core/modes';
import { zToMeters } from './units';
import { createCarMaterial, setCarQuality, type CarUniforms } from './cars/material';
import type { CarModel } from './cars/model';
import { buildModel } from './cars/models';
import { carPaint } from './cars/paints';
import { buildSimpleWheel, buildWheel, spinStep, type WheelDesign, type WheelModel } from './cars/wheels';
import { FLAME_FRAG, FLAME_VERT, flameTexture } from './cars/flame';
import { CAR_BODIES } from '../core/data/cars';

const MAX_CARS = 20;
/** Posição da etiqueta do VIP na lista de etiquetas (depois dos 4 assentos). */
const VIP_LABEL = 4;
/** Bit de freio em `instExtra.w` (os de pintura vêm de cars/paints.ts). */
const BRAKE_BIT = 1;
const SIDES = [-1, 1] as const;
/** Carro mais que isto à frente do carro do viewport (a câmera fica 7,8 m atrás) usa a roda simples. */
const FAR_WHEELS_M = 30;

interface Batch {
  readonly mesh: THREE.InstancedMesh;
  readonly extra: THREE.InstancedBufferAttribute;
  count: number;
}

interface StyleBatch extends Batch { readonly model: CarModel; readonly wheels: WheelBatch }
interface WheelBatch extends Batch { readonly model: WheelModel }

interface CarLook { style: StyleBatch; color: THREE.Color; accent: THREE.Color; livery: number }

interface CarAnim {
  spinF: number; spinR: number; yaw: number; roll: number; pitch: number; bob: number;
  brake: boolean; prevSpeed: number; nitro: number;
}

export class Cars {
  readonly group = new THREE.Group();
  private readonly material: THREE.MeshPhysicalMaterial;
  private readonly uniforms: CarUniforms;
  /** Estilos e desenhos de roda, na ordem de CAR_BODIES (lista, não Map: ordem de desenho estável). */
  private readonly styles: StyleBatch[] = [];
  private readonly wheelBatches: WheelBatch[] = [];
  /** Estilos + rodas numa lista só (fechamento de cada pose sem alocar). */
  private readonly batches: Batch[] = [];
  /** Roda única da qualidade baixa e dos carros distantes (todas numa chamada). */
  private readonly simpleWheels: WheelBatch;
  private lowQuality = false;
  private readonly blob: THREE.InstancedMesh;
  private readonly flames: THREE.InstancedMesh;
  private readonly flameMaterial: THREE.ShaderMaterial;
  /** Aparência por carro (estilo, cores, pintura), recalculada só quando o carId muda. */
  private readonly looks: Array<CarLook | null> = [];
  private readonly lookIds: string[] = [];
  /** Etiquetas por assento (0..3) e, no índice VIP_LABEL, a do VIP da escolta; denso de propósito. */
  private readonly labels: Array<THREE.Sprite | null> = [null, null, null, null, null];
  private readonly labelKeys = ['', '', '', '', ''];
  private readonly spots: THREE.SpotLight[] = [];
  private readonly anims: CarAnim[] = [];
  private readonly dummy = new THREE.Object3D();
  private readonly wheelDummy = new THREE.Object3D();
  private readonly m = new THREE.Matrix4();
  private readonly mw = new THREE.Matrix4();
  private readonly pt: FramePoint = { x: 0, y: 0, z: 0, heading: 0 };
  private lastTime = -1;
  /** Fantasma do contra-relógio: a malha do estilo dele, translúcida e clara, fora das instâncias (só visual). */
  private readonly ghostMaterial = new THREE.MeshStandardMaterial({
    color: '#cfeaff', emissive: '#6fb4ff', emissiveIntensity: 0.5, transparent: true, opacity: 0.38, depthWrite: false, roughness: 0.6,
  });
  private readonly ghost: THREE.Mesh;

  private get instanced(): THREE.InstancedMesh[] {
    return [...this.styles.map((s) => s.mesh), ...this.wheelBatches.map((w) => w.mesh), this.blob, this.flames];
  }

  constructor(scene: THREE.Scene) {
    const { material, uniforms } = createCarMaterial();
    this.material = material; this.uniforms = uniforms;
    const wheelByDesign = new Map<WheelDesign, WheelBatch>();
    for (const body of CAR_BODIES) {
      const model = buildModel(body);
      let wheels = wheelByDesign.get(model.wheel);
      if (!wheels) {
        // Rodas não fazem sombra: ficam dentro da sombra da carroceria e da de contato.
        const wm = buildWheel(model.wheel);
        wheels = { ...this.batch(wm.geometry, MAX_CARS * 4, false), model: wm };
        wheelByDesign.set(model.wheel, wheels);
        this.wheelBatches.push(wheels);
      }
      const style: StyleBatch = { ...this.batch(model.shell, MAX_CARS, true), model, wheels };
      style.mesh.receiveShadow = true;
      this.styles.push(style);
    }
    const simple = buildSimpleWheel();
    this.simpleWheels = { ...this.batch(simple.geometry, MAX_CARS * 4, false), model: simple };
    this.wheelBatches.push(this.simpleWheels);
    this.batches.push(...this.styles, ...this.wheelBatches);
    this.blob = new THREE.InstancedMesh(new THREE.PlaneGeometry(2.5, 4.9).rotateX(-Math.PI / 2).translate(0, 0.02, 0), new THREE.MeshBasicMaterial({ map: blobTexture(), transparent: true, depthWrite: false }), MAX_CARS);
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
      im.visible = false;
      this.group.add(im);
    }
    this.ghost = new THREE.Mesh(this.styles[0].model.shell, this.ghostMaterial);
    this.ghost.matrixAutoUpdate = false; this.ghost.frustumCulled = false; this.ghost.visible = false; this.ghost.renderOrder = 3;
    this.group.add(this.ghost);
    for (let i = 0; i < MAX_CARS; i++) {
      this.anims.push({ spinF: 0, spinR: 0, yaw: 0, roll: 0, pitch: 0, bob: 0, brake: false, prevSpeed: 0, nitro: 0 });
      this.looks.push(null); this.lookIds.push('');
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

  /** InstancedMesh do material dos carros com a cor (pintura) e o extra (acento + bits) por instância. */
  private batch(geometry: THREE.BufferGeometry, capacity: number, castShadow: boolean): Batch {
    const extra = new THREE.InstancedBufferAttribute(new Float32Array(capacity * 4), 4);
    extra.setUsage(THREE.DynamicDrawUsage);
    geometry.setAttribute('instExtra', extra);
    const mesh = new THREE.InstancedMesh(geometry, this.material, capacity);
    mesh.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(capacity * 3), 3);
    mesh.instanceColor.setUsage(THREE.DynamicDrawUsage);
    mesh.castShadow = castShadow;
    return { mesh, extra, count: 0 };
  }

  /** `light` = palette.light (1 dia, 0,7 entardecer, 0,32 noite). */
  setLight(light: number): void {
    const night = light < 0.5;
    this.uniforms.uSelfLight.value = 0.05 + 0.15 * light;
    this.uniforms.uHeadGlow.value = night ? 2.6 : light < 0.8 ? 0.9 : 0.35;
    this.uniforms.uTailGlow.value = night ? 1.3 : 0.9;
    this.uniforms.uPopup.value = light < 0.8 ? 1 : 0;
    for (const s of this.spots) { s.visible = light < 0.8; s.intensity = night ? 230 : 110; }
  }

  /** Verniz (clearcoat) só na alta; média e baixa ficam no programa sem a segunda camada. */
  setQuality(q: Quality): void {
    setCarQuality(this.material, q === 'high');
    this.lowQuality = q === 'low';
  }

  private look(i: number, carId: string): CarLook {
    const cached = this.looks[i];
    if (cached && this.lookIds[i] === carId) return cached;
    const def = carDef(carId);
    const style = this.styleOf(def.body);
    const paint = carPaint(def, CARS, style.model.liveries);
    const look: CarLook = { style, color: new THREE.Color(paint.color), accent: new THREE.Color(paint.accent), livery: paint.livery };
    this.looks[i] = look; this.lookIds[i] = carId;
    return look;
  }

  private styleOf(body: CarBody): StyleBatch {
    const s = this.styles.find((x) => x.model.body === body);
    if (!s) throw new Error(`Estilo de carroceria sem modelo: ${body}`);
    return s;
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
      const look = this.look(i, c.carId);
      const [fa, ra] = look.style.model.axles;
      const sym = look.style.wheels.model.symmetry;
      const sf = Math.min(1.2, c.speed / c.stats.topSpeed);
      const dist = zToMeters(c.speed) * dt;
      a.spinF += spinStep(dist / fa.r, sym);
      a.spinR += spinStep(dist / ra.r, sym);
      const decel = (a.prevSpeed - c.speed) / Math.max(1e-3, dt);
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
    }
    this.flameMaterial.uniforms.uTime.value = frame.time;
  }

  private put(b: Batch, m: THREE.Matrix4, look: CarLook, flags: number): void {
    const idx = b.count++;
    b.mesh.setMatrixAt(idx, m);
    b.mesh.setColorAt(idx, look.color);
    b.extra.setXYZW(idx, look.accent.r, look.accent.g, look.accent.b, flags);
  }

  /** Posiciona os carros no referencial do viewport (`ownIndex` = carro do viewport). */
  pose(rf: RoadFrame, state: RaceState, track: Track, ownIndex: number, viewportSeats: ReadonlyArray<{ seat: number; name: string; color: string; carIndex: number }>, time: number): void {
    const cars = state.cars;
    const d = this.dummy;
    const w = this.wheelDummy;
    let nBlob = 0; let nFlames = 0;
    for (const s of this.styles) s.count = 0;
    for (const wb of this.wheelBatches) wb.count = 0;
    for (const s of this.labels) if (s) s.visible = false;
    for (let i = 0; i < cars.length && i < MAX_CARS; i++) {
      const c = cars[i];
      if (!locateOnFrame(rf, track, c.z, c.x, this.pt)) continue;
      // Carro atrás do carro do viewport ficaria entre ele e a câmera (a 8 m), enorme e
      // cortado: só aparece quando o centro dele passa 2,2 m atrás do centro do jogador.
      if (i !== ownIndex && this.pt.z > 2.2) continue;
      const a = this.anims[i];
      const look = this.look(i, c.carId);
      const model = look.style.model;
      const bobY = a.bob ? Math.sin(time * 71 + i) * 0.025 : 0;
      const bobR = a.bob ? Math.sin(time * 57 + i * 2) * 0.02 : 0;
      d.position.set(this.pt.x, this.pt.y + bobY, this.pt.z);
      d.rotation.set(a.pitch, -(this.pt.heading + a.yaw), a.roll + bobR, 'YXZ');
      d.scale.set(1, 1, 1);
      d.updateMatrix();
      this.m.copy(d.matrix);
      const flags = look.livery | (a.brake ? BRAKE_BIT : 0);
      this.put(look.style, this.m, look, flags);
      // Sombra de contato: no chão, sem rolagem/mergulho.
      d.rotation.set(0, -(this.pt.heading + a.yaw), 0, 'YXZ');
      d.position.y = this.pt.y;
      d.updateMatrix();
      this.blob.setMatrixAt(nBlob++, d.matrix);
      // Rodas: matriz do carro × deslocamento × giro (dianteiras viram com o volante). O desenho
      // é montado com a face de fora em +x: as da esquerda giram meia volta em y (e o giro inverte).
      // Longe (roda com ~10 px na tela) o desenho do aro não aparece: vai a roda simples, com as dos
      // outros carros distantes, numa chamada só.
      const wheels = this.lowQuality || this.pt.z < -FAR_WHEELS_M ? this.simpleWheels : look.style.wheels;
      for (let ax = 0; ax < 2; ax++) {
        const axle = model.axles[ax];
        const spin = ax === 0 ? a.spinF : a.spinR;
        const steer = ax === 0 ? c.steerPose * 0.32 : 0;
        for (const sx of SIDES) {
          w.position.set(sx * axle.x, axle.r, axle.z);
          if (sx > 0) w.rotation.set(-spin, -steer, 0, 'YXZ');
          else w.rotation.set(spin, Math.PI - steer, 0, 'YXZ');
          w.scale.set(axle.w, axle.r, axle.r);
          w.updateMatrix();
          this.mw.multiplyMatrices(this.m, w.matrix);
          this.put(wheels, this.mw, look, look.livery);
        }
      }
      // Chama do nitro: origem no escape, +Z para trás = comprimento, escala x = largura.
      if (a.nitro > 0) {
        const pulse = 0.85 + 0.3 * Math.sin(time * 41 + i * 1.7) + 0.25 * hash2(Math.floor(time * 28), i);
        for (const e of model.exhausts) {
          if (nFlames >= MAX_CARS * 2) break;
          w.position.set(e[0], e[1], e[2]);
          w.rotation.set(0, 0, 0);
          w.scale.set(0.7, 1, 2.3 * pulse * (0.55 + 0.45 * a.nitro));
          w.updateMatrix();
          this.mw.multiplyMatrices(this.m, w.matrix);
          this.flames.setMatrixAt(nFlames++, this.mw);
        }
      }
      const labelY = this.pt.y + model.height + 0.9;
      // Escolta: o VIP leva etiqueta própria para a equipe não perdê-lo de vista (docs/MODOS.md).
      if (state.party && c.id === state.party.vipId) {
        const label = this.label(VIP_LABEL, VIP_NAME, VIP_COLOR);
        label.position.set(this.pt.x, labelY, this.pt.z);
        label.visible = true;
      }
      // Etiqueta de jogador local visto de outro viewport.
      if (c.seat >= 0 && c.seat < 4 && i !== ownIndex) {
        const vp = viewportSeats.find((v) => v.seat === c.seat);
        if (vp) {
          const label = this.label(c.seat, vp.name, vp.color);
          label.position.set(this.pt.x, labelY, this.pt.z);
          label.visible = true;
        }
      }
    }
    for (const b of this.batches) {
      b.mesh.count = b.count;
      b.mesh.visible = b.count > 0;
      if (b.count > 0) {
        b.mesh.instanceMatrix.needsUpdate = true;
        if (b.mesh.instanceColor) b.mesh.instanceColor.needsUpdate = true;
        b.extra.needsUpdate = true;
      }
    }
    this.blob.count = nBlob; this.flames.count = nFlames;
    this.blob.visible = nBlob > 0; if (nBlob > 0) this.blob.instanceMatrix.needsUpdate = true;
    this.flames.visible = nFlames > 0; if (nFlames > 0) this.flames.instanceMatrix.needsUpdate = true;
  }

  /**
   * Fantasma no referencial do viewport (chamar depois de `pose`). Some quando não há pose, quando
   * está fora da janela da pista ou atrás do carro do viewport (como os outros carros).
   */
  poseGhost(rf: RoadFrame, track: Track, ghost: GhostFrame | undefined): void {
    const p = ghost?.pose;
    const z = p ? ((p.z % track.length) + track.length) % track.length : 0;
    const on = !!p && locateOnFrame(rf, track, z, p.x, this.pt) && this.pt.z <= 2.2;
    this.ghost.visible = on;
    if (!on || !p || !ghost) return;
    this.ghost.geometry = this.ghostShell(ghost.carId);
    const d = this.dummy;
    d.position.set(this.pt.x, this.pt.y, this.pt.z);
    d.rotation.set(0, -(this.pt.heading + p.steerPose * 0.08), 0, 'YXZ');
    d.scale.set(1, 1, 1);
    d.updateMatrix();
    this.ghost.matrix.copy(d.matrix);
    this.ghost.matrixWorldNeedsUpdate = true;
  }

  /** Malha do estilo do carro do fantasma (carro que não existe mais cai no primeiro estilo). */
  private ghostShell(carId: string): THREE.BufferGeometry {
    const def = CARS.find((c) => c.id === carId);
    return def ? this.styleOf(def.body).model.shell : this.styles[0].model.shell;
  }

  /** Esconde todos os carros (fundo dos menus). */
  hide(): void {
    this.ghost.visible = false;
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

  /** Números para o harness e o relatório de desempenho: triângulos por estilo e por roda. */
  stats(): { styles: Record<string, number>; wheels: Record<string, number> } {
    const styles: Record<string, number> = {}; const wheels: Record<string, number> = {};
    for (const s of this.styles) styles[s.model.body] = s.model.triangles;
    for (const w of this.wheelBatches) wheels[w.model.design] = w.model.triangles;
    return { styles, wheels };
  }

  dispose(): void {
    for (const im of this.instanced) { im.geometry.dispose(); im.dispose(); }
    this.material.dispose();
    (this.blob.material as THREE.Material).dispose();
    (this.blob.material as THREE.MeshBasicMaterial).map?.dispose();
    this.flameMaterial.dispose();
    this.ghostMaterial.dispose(); // a geometria é a de um estilo, já liberada acima
    (this.flameMaterial.uniforms.uMap.value as THREE.Texture).dispose();
    for (const s of this.labels) if (s) { if (s.material.map) s.material.map.dispose(); s.material.dispose(); }
    for (const s of this.spots) s.dispose();
  }
}

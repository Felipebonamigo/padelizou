// Efeitos: partículas em coordenadas de pista (z ao longo, x lateral, h altura) — assim a
// mesma partícula aparece certa em todos os viewports, cada um a localizando pelo seu
// RoadFrame. Pools com buffers reutilizados; poeira na grama, faíscas na colisão, rastro do
// nitro, fumaça de freio. Também o chacoalho de câmera por assento, com decaimento.
import * as THREE from 'three';
import { COLLISION_COOLDOWN_TICKS } from '../core/constants';
import { carDef } from '../core/data/cars';
import type { RaceState, Track } from '../core/types';
import type { RenderFrame } from '../game/contracts';
import { hash2 } from './noise';
import type { Palette } from './palette';
import { locateOnFrame, type FramePoint, type RoadFrame } from './roadframe';

const VERT = /* glsl */ `
attribute float aSize; attribute float aAlpha; attribute vec3 aColor;
uniform float uScale;
varying float vAlpha; varying vec3 vColor;
void main() {
  vec4 mv = modelViewMatrix * vec4(position, 1.0);
  gl_PointSize = aSize * uScale / max(0.5, -mv.z);
  gl_Position = projectionMatrix * mv;
  vAlpha = aAlpha; vColor = aColor;
}`;

const FRAG = /* glsl */ `
varying float vAlpha; varying vec3 vColor;
void main() {
  float d = length(gl_PointCoord - 0.5);
  float a = smoothstep(0.5, 0.12, d) * vAlpha;
  if (a < 0.01) discard;
  gl_FragColor = vec4(vColor, a);
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
}`;

/** Pool de partículas em coordenadas de pista. */
class Pool {
  readonly points: THREE.Points;
  private readonly z: Float32Array; private readonly x: Float32Array; private readonly h: Float32Array;
  private readonly vz: Float32Array; private readonly vx: Float32Array; private readonly vh: Float32Array;
  private readonly life: Float32Array; private readonly maxLife: Float32Array; private readonly size: Float32Array;
  private readonly grow: Float32Array; private readonly gravity: Float32Array;
  private readonly r: Float32Array; private readonly g: Float32Array; private readonly b: Float32Array;
  private readonly position: THREE.BufferAttribute;
  private readonly aSize: THREE.BufferAttribute;
  private readonly aAlpha: THREE.BufferAttribute;
  private readonly aColor: THREE.BufferAttribute;
  private readonly material: THREE.ShaderMaterial;
  private next = 0;
  private readonly pt: FramePoint = { x: 0, y: 0, z: 0, heading: 0 };

  constructor(readonly max: number, additive: boolean) {
    const f = () => new Float32Array(max);
    this.z = f(); this.x = f(); this.h = f(); this.vz = f(); this.vx = f(); this.vh = f();
    this.life = f(); this.maxLife = f(); this.size = f(); this.grow = f(); this.gravity = f(); this.r = f(); this.g = f(); this.b = f();
    const geo = new THREE.BufferGeometry();
    this.position = new THREE.BufferAttribute(new Float32Array(max * 3), 3); this.position.setUsage(THREE.DynamicDrawUsage);
    this.aSize = new THREE.BufferAttribute(new Float32Array(max), 1); this.aSize.setUsage(THREE.DynamicDrawUsage);
    this.aAlpha = new THREE.BufferAttribute(new Float32Array(max), 1); this.aAlpha.setUsage(THREE.DynamicDrawUsage);
    this.aColor = new THREE.BufferAttribute(new Float32Array(max * 3), 3); this.aColor.setUsage(THREE.DynamicDrawUsage);
    geo.setAttribute('position', this.position); geo.setAttribute('aSize', this.aSize); geo.setAttribute('aAlpha', this.aAlpha); geo.setAttribute('aColor', this.aColor);
    geo.setDrawRange(0, 0);
    this.material = new THREE.ShaderMaterial({
      vertexShader: VERT, fragmentShader: FRAG, transparent: true, depthWrite: false,
      blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending, uniforms: { uScale: { value: 400 } },
    });
    this.points = new THREE.Points(geo, this.material);
    this.points.frustumCulled = false;
    this.points.renderOrder = 3;
  }

  emit(z: number, x: number, h: number, vz: number, vx: number, vh: number, life: number, size: number, grow: number, gravity: number, r: number, g: number, b: number): void {
    const i = this.next; this.next = (this.next + 1) % this.max;
    this.z[i] = z; this.x[i] = x; this.h[i] = h; this.vz[i] = vz; this.vx[i] = vx; this.vh[i] = vh;
    this.life[i] = life; this.maxLife[i] = life; this.size[i] = size; this.grow[i] = grow; this.gravity[i] = gravity;
    this.r[i] = r; this.g[i] = g; this.b[i] = b;
  }

  step(dt: number): void {
    for (let i = 0; i < this.max; i++) {
      if (this.life[i] <= 0) continue;
      this.life[i] -= dt;
      this.z[i] += this.vz[i] * dt; this.x[i] += this.vx[i] * dt;
      this.vh[i] -= this.gravity[i] * dt;
      this.h[i] += this.vh[i] * dt;
      if (this.h[i] < 0.02) { this.h[i] = 0.02; this.vh[i] = Math.abs(this.vh[i]) * 0.3; }
      this.vz[i] *= 1 - 1.2 * dt; this.vx[i] *= 1 - 1.2 * dt;
    }
  }

  clear(): void { this.life.fill(0); this.points.geometry.setDrawRange(0, 0); }

  /** Escreve os buffers no referencial do viewport; `scale` = altura em px / (2·tan(fov/2)). */
  pose(rf: RoadFrame, track: Track, scale: number): void {
    let n = 0;
    for (let i = 0; i < this.max; i++) {
      if (this.life[i] <= 0) continue;
      if (!locateOnFrame(rf, track, this.z[i], this.x[i], this.pt)) continue;
      const t = this.life[i] / this.maxLife[i];
      this.position.setXYZ(n, this.pt.x, this.pt.y + this.h[i], this.pt.z);
      this.aSize.setX(n, this.size[i] + this.grow[i] * (1 - t));
      this.aAlpha.setX(n, Math.min(1, t * 2.5) * (0.25 + 0.55 * t));
      this.aColor.setXYZ(n, this.r[i], this.g[i], this.b[i]);
      n++;
    }
    this.points.geometry.setDrawRange(0, n);
    this.points.visible = n > 0;
    if (n > 0) { this.position.needsUpdate = true; this.aSize.needsUpdate = true; this.aAlpha.needsUpdate = true; this.aColor.needsUpdate = true; }
    this.material.uniforms.uScale.value = scale;
  }

  dispose(): void { this.points.geometry.dispose(); this.material.dispose(); }
}

export interface Shake { x: number; y: number; roll: number }

export class Effects {
  readonly group = new THREE.Group();
  private readonly dust = new Pool(900, false);
  private readonly sparks = new Pool(700, true);
  private readonly prevCooldown = new Int32Array(20);
  private readonly prevSpeed = new Float32Array(20);
  private readonly shakeAmp = new Float32Array(4);
  private readonly dustColor = new THREE.Color('#8a7a5a');
  private lastTime = -1;
  private seed = 0;

  constructor() { this.group.add(this.dust.points, this.sparks.points); }

  setPalette(p: Palette): void {
    this.dustColor.set(p.grassDark).lerp(new THREE.Color('#5e4d33'), 0.6).multiplyScalar(0.8);
  }

  private rnd(): number { this.seed++; return hash2(this.seed, 77); }

  /** Emissores + física, uma vez por quadro. */
  update(frame: RenderFrame): void {
    const dt = this.lastTime < 0 ? 1 / 60 : Math.min(0.1, Math.max(0, frame.time - this.lastTime));
    this.lastTime = frame.time;
    if (frame.paused) return;
    const state: RaceState = frame.state;
    const dc = this.dustColor;
    for (let i = 0; i < state.cars.length && i < 20; i++) {
      const c = state.cars[i];
      const def = carDef(c.carId);
      const sf = c.speed / def.topSpeed;
      // Poeira na grama.
      if (c.skidTicks > 0 && c.speed > 250) {
        for (let k = 0; k < 2; k++) {
          const side = k % 2 === 0 ? -0.16 : 0.16;
          this.dust.emit(c.z - 50 + this.rnd() * 30, c.x + side * (1 + this.rnd()), 0.3, c.speed * (0.5 + this.rnd() * 0.2), (this.rnd() - 0.5) * 0.4 + side * 0.8, 1.2 + this.rnd() * 1.6, 0.6 + this.rnd() * 0.5, 0.5, 1.4, 0.5, dc.r, dc.g, dc.b);
        }
      }
      // Fumaça leve na frenagem forte.
      const decel = (this.prevSpeed[i] - c.speed) / dt;
      if (c.speed > 2500 && decel > def.brake * 0.55 && c.collisionCooldown === 0 && this.rnd() < 0.6) {
        const side = this.rnd() < 0.5 ? -0.14 : 0.14;
        this.dust.emit(c.z - 60, c.x + side, 0.15, c.speed * 0.6, side * 0.3, 0.8, 0.5, 0.5, 1.6, 0.2, 0.75, 0.75, 0.78);
      }
      this.prevSpeed[i] = c.speed;
      // Faíscas na colisão (borda de subida do cooldown).
      if (c.collisionCooldown > this.prevCooldown[i] && c.collisionCooldown >= COLLISION_COOLDOWN_TICKS - 1) {
        for (let k = 0; k < 26; k++) {
          this.sparks.emit(c.z + (this.rnd() - 0.5) * 120, c.x + (this.rnd() - 0.5) * 0.3, 0.3 + this.rnd() * 0.4,
            c.speed * 0.85 + (this.rnd() - 0.5) * 900, (this.rnd() - 0.5) * 1.6, 1.5 + this.rnd() * 5, 0.3 + this.rnd() * 0.4, 0.14, 0.05, 9, 2.2, 1.4, 0.4);
        }
        if (c.seat >= 0 && c.seat < 4) this.shakeAmp[c.seat] = Math.min(1, this.shakeAmp[c.seat] + 0.6 + 0.4 * sf);
      }
      this.prevCooldown[i] = c.collisionCooldown;
      // Rastro do nitro: curto e discreto (a chama em si é o billboard em cars.ts).
      if (c.nitroTicks > 0 && state.phase === 'racing' && this.rnd() < 0.7) {
        const side = this.rnd() < 0.5 ? -0.065 : 0.065;
        this.sparks.emit(c.z - 66, c.x + side, 0.3, c.speed - 900 - this.rnd() * 700, (this.rnd() - 0.5) * 0.2, (this.rnd() - 0.4) * 0.8, 0.18 + this.rnd() * 0.18, 0.06, 0.04, 0.8, 0.45, 1.0, 1.8);
      }
    }
    this.dust.step(dt);
    this.sparks.step(dt);
    for (let s = 0; s < 4; s++) this.shakeAmp[s] *= Math.exp(-4.5 * dt);
  }

  pose(rf: RoadFrame, track: Track, scale: number): void {
    this.dust.pose(rf, track, scale);
    this.sparks.pose(rf, track, scale);
  }

  clear(): void { this.dust.clear(); this.sparks.clear(); this.shakeAmp.fill(0); this.lastTime = -1; }

  /** Deslocamento da câmera pelo chacoalho do assento (zero se desligado nas opções). */
  shake(seat: number, time: number, enabled: boolean, out: Shake): Shake {
    const a = enabled && seat >= 0 && seat < 4 ? this.shakeAmp[seat] : 0;
    out.x = a * Math.sin(time * 47) * 0.14;
    out.y = a * Math.cos(time * 61) * 0.1;
    out.roll = a * Math.sin(time * 37) * 0.02;
    return out;
  }

  dispose(): void { this.dust.dispose(); this.sparks.dispose(); }
}

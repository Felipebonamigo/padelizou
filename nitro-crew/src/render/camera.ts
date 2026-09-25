// Câmeras: perseguição por viewport (atrás e acima do carro, olhando à frente, atraso lateral
// suave, FOV que abre com a velocidade e no nitro, inclinação leve nas curvas, chacoalho só na
// batida) e a câmera automática do fundo dos menus (voa pela pista, mais alta, sem carro).
import * as THREE from 'three';
import { frameYAt, type RoadFrame } from './roadframe';
import type { Shake } from './effects';
import { ROAD_HALF_WIDTH_M } from './units';

const BASE_FOV = 62;
const SPEED_FOV = 13;
const NITRO_FOV = 8;

export class ChaseCamera {
  readonly camera = new THREE.PerspectiveCamera(BASE_FOV, 16 / 9, 0.3, 3000);
  private lagX = 0;
  private fov = BASE_FOV;
  private roll = 0;
  private nitro = 0;
  private lastTime = -1;
  private readonly target = new THREE.Vector3();
  private readonly up = new THREE.Vector3(0, 1, 0);

  /** `carX` em meias larguras, `speedFrac` 0..1,2, `curve` do segmento atual. */
  update(rf: RoadFrame, carX: number, speedFrac: number, curve: number, nitroOn: boolean, time: number, shake: Shake): void {
    const dt = this.lastTime < 0 ? 1 / 60 : Math.min(0.1, Math.max(0, time - this.lastTime));
    this.lastTime = time;
    const k = 1 - Math.exp(-dt * 5.5);
    const xm = carX * ROAD_HALF_WIDTH_M;
    this.lagX += (xm * 0.8 - this.lagX) * k;
    this.nitro += ((nitroOn ? 1 : 0) - this.nitro) * (1 - Math.exp(-dt * 4));
    const targetFov = BASE_FOV + SPEED_FOV * Math.min(1.2, speedFrac) + NITRO_FOV * this.nitro;
    this.fov += (targetFov - this.fov) * (1 - Math.exp(-dt * 3));
    const rollTarget = -curve * 0.012 * Math.min(1, speedFrac);
    this.roll += (rollTarget - this.roll) * (1 - Math.exp(-dt * 3));
    const camY = 2.7 + frameYAt(rf, -7) * 0.7;
    const aheadY = 1.1 + frameYAt(rf, 14) * 0.6;
    const c = this.camera;
    c.position.set(this.lagX + shake.x, camY + shake.y, 7.2);
    this.target.set(this.lagX * 0.55 + xm * 0.35, aheadY, -14);
    const r = this.roll + shake.roll;
    this.up.set(Math.sin(r), Math.cos(r), 0);
    c.up.copy(this.up);
    c.lookAt(this.target);
    if (Math.abs(c.fov - this.fov) > 0.05) { c.fov = this.fov; c.updateProjectionMatrix(); }
  }

  setAspect(aspect: number): void {
    if (Math.abs(this.camera.aspect - aspect) > 1e-4) { this.camera.aspect = aspect; this.camera.updateProjectionMatrix(); }
  }

  reset(): void { this.lagX = 0; this.fov = BASE_FOV; this.roll = 0; this.nitro = 0; this.lastTime = -1; }
}

export class IdleCamera {
  readonly camera = new THREE.PerspectiveCamera(58, 16 / 9, 0.5, 3000);
  private readonly target = new THREE.Vector3();

  update(rf: RoadFrame, time: number): void {
    const sway = Math.sin(time * 0.23);
    const c = this.camera;
    c.position.set(4.5 * sway, 8.5 + frameYAt(rf, -4) * 0.5, 4);
    this.target.set(-2 * sway, 1.5 + frameYAt(rf, 40) * 0.6, -45);
    c.up.set(0, 1, 0);
    c.lookAt(this.target);
  }

  setAspect(aspect: number): void {
    if (Math.abs(this.camera.aspect - aspect) > 1e-4) { this.camera.aspect = aspect; this.camera.updateProjectionMatrix(); }
  }
}

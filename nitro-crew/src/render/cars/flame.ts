// Chama do nitro: atlas de 4 quadros num billboard axial instanciado (o plano gira em torno do eixo da
// chama para encarar a câmera). Veio de render/cars.ts sem mudança.
import * as THREE from 'three';
import { hash2 } from '../noise';
import { canvas2d } from '../textures';

/** Atlas de 4 quadros da chama (base embaixo de cada quadro, ponta em cima): gaussiana que afina, núcleo branco → azul. */
export function flameTexture(): THREE.CanvasTexture {
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
export const FLAME_VERT = /* glsl */ `
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

export const FLAME_FRAG = /* glsl */ `
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

// Renderizador 3D (Three.js). Uma cena só; a cada quadro, para cada viewport, o mundo é
// reposicionado no referencial do carro daquele jogador (RoadFrame) e desenhado por
// scissor. Pós-processamento (bloom só nos emissivos + saída sRGB) por viewport na qualidade
// alta, com MSAA no render target; médio sem pós; baixo sem sombras e com menos pista.
import * as THREE from 'three';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { carDef } from '../core/data/cars';
import { segmentAt } from '../core/track/builder';
import type { Track } from '../core/types';
import type { Quality, RenderFrame, Renderer } from '../game/contracts';
import { ChaseCamera, IdleCamera } from './camera';
import { Cars } from './cars';
import { Effects, type Shake } from './effects';
import { Hud } from './hud';
import { viewportRects, type Rect } from './layout';
import { palette } from './palette';
import { Road } from './road';
import { absoluteHeading, buildRoadFrame, type RoadFrame } from './roadframe';
import { Scenery } from './scenery';
import { Sky } from './sky';
import { Terrain } from './terrain';

const BEHIND = 30;
const AHEAD: Record<Quality, number> = { low: 140, medium: 200, high: 260 };
const MAX_POINTS = BEHIND + AHEAD.high + 1;
/** Velocidade da câmera automática dos menus (unidades de pista por segundo). */
const IDLE_SPEED = 2600;

export interface DebugInfo { calls: number; triangles: number; frameMs: number }

interface ViewportPost { composer: EffectComposer; bloom: UnrealBloomPass; key: string }

export function createRenderer(canvas: HTMLCanvasElement, hudRoot: HTMLElement): Renderer & { debugInfo(): DebugInfo } {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFShadowMap; // PCFSoft foi removido no r186
  renderer.info.autoReset = false;
  renderer.setScissorTest(true);
  const maxAniso = renderer.capabilities.getMaxAnisotropy();

  const scene = new THREE.Scene();
  const sky = new Sky(renderer, scene);
  const road = new Road(MAX_POINTS, Math.min(8, maxAniso));
  const terrain = new Terrain(MAX_POINTS);
  const scenery = new Scenery();
  const cars = new Cars(scene);
  const effects = new Effects();
  scene.add(terrain.group, road.group, scenery.group, effects.group);
  const hud = new Hud(hudRoot);

  // Janelas alocadas na primeira chamada e reutilizadas (zero alocação por quadro).
  const frames: Array<RoadFrame | undefined> = [undefined, undefined, undefined, undefined];
  const cameras = [new ChaseCamera(), new ChaseCamera(), new ChaseCamera(), new ChaseCamera()];
  const idleCamera = new IdleCamera();
  const shake: Shake = { x: 0, y: 0, roll: 0 };

  let width = 1; let height = 1; let dpr = 1;
  let quality: Quality | null = null;
  let trackKey = '';
  const posts: ViewportPost[] = [];
  const debug: DebugInfo = { calls: 0, triangles: 0, frameMs: 0 };
  let disposed = false;

  function ensureTrack(track: Track): void {
    const key = `${track.def.id}:${track.def.scenery}:${track.def.timeOfDay}`;
    if (key === trackKey) return;
    trackKey = key;
    const p = palette(track.def.scenery, track.def.timeOfDay);
    const night = p.light < 0.8;
    sky.setPalette(p, track.def.timeOfDay, key);
    road.setPalette(p, track.def.timeOfDay === 'night', key);
    terrain.setTrack(track, p, key);
    scenery.setNight(night);
    cars.setNight(night);
    effects.setPalette(p);
    effects.clear();
    renderer.toneMappingExposure = sky.exposure;
    for (const c of cameras) c.reset();
    if (quality) sky.setQuality(quality);
  }

  function ensureQuality(q: Quality): void {
    if (q === quality) return;
    quality = q;
    renderer.shadowMap.enabled = q !== 'low';
    sky.setQuality(q);
    // Materiais com sombra precisam recompilar quando o shadow map liga/desliga.
    scene.traverse((o) => { if (o instanceof THREE.Mesh) { const m = o.material as THREE.Material; m.needsUpdate = true; } });
    disposePosts();
  }

  function disposePosts(): void {
    for (const p of posts) { p.composer.dispose(); }
    posts.length = 0;
  }

  function postFor(i: number, rect: Rect, camera: THREE.PerspectiveCamera): ViewportPost {
    const key = `${rect.w}x${rect.h}@${dpr}`;
    const existing = posts[i];
    if (existing && existing.key === key) return existing;
    if (existing) existing.composer.dispose();
    const target = new THREE.WebGLRenderTarget(Math.max(1, Math.round(rect.w * dpr)), Math.max(1, Math.round(rect.h * dpr)), { type: THREE.HalfFloatType, samples: 4 });
    const composer = new EffectComposer(renderer, target);
    composer.setPixelRatio(dpr);
    composer.setSize(rect.w, rect.h);
    composer.addPass(new RenderPass(scene, camera));
    const bloom = new UnrealBloomPass(new THREE.Vector2(rect.w, rect.h), 0.35, 0.4, 0.85);
    composer.addPass(bloom);
    composer.addPass(new OutputPass());
    const post = { composer, bloom, key };
    posts[i] = post;
    return post;
  }

  function setViewport(rect: Rect): void {
    const y = height - rect.y - rect.h;
    renderer.setViewport(rect.x, y, rect.w, rect.h);
    renderer.setScissor(rect.x, y, rect.w, rect.h);
  }

  function poseWorld(rf: RoadFrame, track: Track, z: number, time: number): void {
    const absH = absoluteHeading(track, z);
    sky.update(absH, time);
    road.update(rf, track);
    terrain.update(rf, track, time, sky.sunDirLocal, absH);
    scenery.update(rf, track, time);
  }

  function render(frame: RenderFrame): void {
    if (disposed) return;
    const t0 = performance.now();
    ensureTrack(frame.track);
    ensureQuality(frame.options.quality);
    const q = frame.options.quality;
    const track = frame.track;
    cars.update(frame);
    effects.update(frame);
    const n = Math.max(1, Math.min(4, frame.viewports.length));
    const rects = viewportRects(n, width, height);
    renderer.info.reset();
    for (let i = 0; i < n; i++) {
      const vp = frame.viewports[i];
      const car = frame.state.cars[vp.carIndex];
      if (!car) continue;
      const rf = buildRoadFrame(track, car.z, BEHIND, AHEAD[q], frames[i]);
      frames[i] = rf;
      poseWorld(rf, track, car.z, frame.time);
      cars.pose(rf, frame.state, track, vp.carIndex, frame.viewports, frame.time);
      const cam = cameras[i];
      const rect = rects[i];
      cam.setAspect(rect.w / Math.max(1, rect.h));
      const def = carDef(car.carId);
      const seg = segmentAt(track, car.z);
      effects.shake(vp.seat, frame.time, frame.options.screenShake, shake);
      cam.update(rf, car.x, car.speed / def.topSpeed, seg.curve, car.nitroTicks > 0, frame.time, shake);
      effects.pose(rf, track, (rect.h * dpr) / (2 * Math.tan((cam.camera.fov * Math.PI / 180) / 2)));
      setViewport(rect);
      if (q === 'high') postFor(i, rect, cam.camera).composer.render();
      else renderer.render(scene, cam.camera);
    }
    hud.update(frame, width, height);
    debug.calls = renderer.info.render.calls;
    debug.triangles = renderer.info.render.triangles;
    debug.frameMs = performance.now() - t0;
  }

  function renderIdle(time: number, track: Track): void {
    if (disposed) return;
    const t0 = performance.now();
    ensureTrack(track);
    ensureQuality(quality ?? 'high');
    const z = (time * IDLE_SPEED) % track.length;
    const rf = buildRoadFrame(track, z, BEHIND, AHEAD[quality ?? 'high'], frames[0]);
    frames[0] = rf;
    poseWorld(rf, track, z, time);
    cars.hide();
    effects.clear();
    effects.pose(rf, track, 1);
    idleCamera.setAspect(width / Math.max(1, height));
    idleCamera.update(rf, time);
    const rect: Rect = { x: 0, y: 0, w: width, h: height };
    setViewport(rect);
    renderer.info.reset();
    if (quality === 'high') postFor(0, rect, idleCamera.camera).composer.render();
    else renderer.render(scene, idleCamera.camera);
    hud.hide();
    debug.calls = renderer.info.render.calls;
    debug.triangles = renderer.info.render.triangles;
    debug.frameMs = performance.now() - t0;
  }

  function resize(w: number, h: number, ratio: number): void {
    width = Math.max(1, Math.floor(w)); height = Math.max(1, Math.floor(h)); dpr = Math.max(0.5, Math.min(3, ratio));
    renderer.setPixelRatio(dpr);
    renderer.setSize(width, height, false);
    disposePosts();
  }

  function dispose(): void {
    if (disposed) return;
    disposed = true;
    disposePosts();
    sky.dispose(); road.dispose(); terrain.dispose(); scenery.dispose(); cars.dispose(); effects.dispose(); hud.dispose();
    renderer.dispose();
  }

  return {
    canvas, resize, render, renderIdle, dispose,
    debugInfo: () => ({ ...debug }),
  };
}

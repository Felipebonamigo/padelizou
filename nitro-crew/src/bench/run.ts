// Banco de prova (?bench=1 no navegador; --bench no Electron chega como ?bench=1&uncapped=1…): uma corrida nova por
// cena × nível, 1 tick por quadro, quadros medidos pelos carimbos do rAF. JSON nitro-crew-bench/1 (metrics.ts).
// O nível Ultra (src/bench/ultra.ts) é desenhado em 2560×1440 (ou no `res=` pedido) com a qualidade Alta: enquanto os
// efeitos da L2 não existem, o número dele é a folga da Alta em 1440p, e o relatório diz isso (benchWarnings).
import '../i18n/core';
import './strings';
import { CARS, createRace, getTrack, seatColor, stepRace, type AiBrain, type HumanEntry } from '../core';
import { DEFAULT_SETTINGS, type RenderFrame } from '../game/contracts';
import type { DesktopApi } from '../game/desktop';
import { GAME_VERSION } from '../game/errors';
import { racePaints } from '../game/paints';
import { loadSettings } from '../game/settings';
import { setLanguage, t } from '../i18n';
import { createRenderer } from '../render/renderer';
import {
  BENCH_SCHEMA, benchWarnings, evaluateTargets, summarizeFrames, ultraInfo, validateBenchReport,
  type BenchEnv, type BenchReport, type BenchRun, type BenchStatus, type BenchVerdict,
} from './metrics';
import { parseBenchOptions, renderSizeFor, type BenchOptions, type BenchScene } from './options';
import { renderQualityOf, ultraEffectsActive, type BenchLevel } from './ultra';

declare global { interface Window { ncBench?: BenchStatus } }
export interface BenchDeps { canvas: HTMLCanvasElement; hud: HTMLElement; ui: HTMLElement; desktop: DesktopApi | null }
type BenchRenderer = ReturnType<typeof createRenderer>;

// Nada de trabalho DENTRO do callback do rAF: uma exceção ali não chega ao try/catch. Espera-se o quadro e desenha-se depois.
const nextFrame = () => new Promise<number>((resolve) => { requestAnimationFrame(resolve); });

export async function runBench(search: string, deps: BenchDeps): Promise<void> {
  try {
    const opts = parseBenchOptions(search);
    if (!opts) throw new Error('runBench sem bench=1');
    setLanguage(loadSettings().language); // só leitura (src/game/settings.ts)
    const r = createRenderer(deps.canvas, deps.hud);
    const runs: BenchRun[] = [];
    const total = opts.scenes.length * opts.qualities.length;
    for (const scene of opts.scenes) for (const level of opts.qualities) {
      const progress = t('bench.running', { scene: scene.id, quality: level, i: runs.length + 1, n: total });
      window.ncBench = { done: false, progress };
      deps.ui.textContent = progress;
      runs.push(await measure(r, scene, level, opts));
    }
    const report: BenchReport = { schema: BENCH_SCHEMA, version: GAME_VERSION, date: new Date().toISOString(),
      env: await benchEnv(r, deps.desktop, opts),
      options: { frames: opts.frames, warm: opts.warm, scenes: opts.scenes.map((s) => s.id), qualities: opts.qualities }, runs,
      ultra: ultraInfo(ultraEffectsActive()), verdicts: evaluateTargets(runs) };
    report.warnings = benchWarnings(report);
    const problems = validateBenchReport(report);
    let file: string | null = null;
    if (deps.desktop) {
      file = await deps.desktop.benchWrite(JSON.stringify(report, null, 2));
      if (!file) problems.push('benchWrite: o arquivo não foi gravado');
    }
    window.ncBench = { done: true, report, problems, file };
    showPanel(deps, report, problems, file);
    if (opts.quit && deps.desktop) {
      r.dispose(); // libera os alvos de 1440p antes de fechar: o swiftshader encerrava com SIGTRAP depois de um Ultra
      await deps.desktop.quit();
    }
  } catch (e) {
    console.error(e);
    window.ncBench = { done: true, report: null, problems: [String(e)], file: null, error: String(e) };
    deps.ui.textContent = String(e);
  }
}

async function measure(r: BenchRenderer, scene: BenchScene, level: BenchLevel, opts: BenchOptions): Promise<BenchRun> {
  // Em toda corrida: a tela cheia do Electron chega depois do load. O Ultra (ou o `res=`) desenha num buffer de tamanho
  // fixo (dpr 1), que o CSS estica na janela; os outros seguem a janela (src/game/session.ts, resize).
  const size = renderSizeFor(level, opts.res);
  const dpr = Math.min(2, window.devicePixelRatio || 1);
  if (size) r.resize(size.w, size.h, 1); else r.resize(window.innerWidth, window.innerHeight, dpr);
  const renderW = size ? size.w : Math.round(window.innerWidth * dpr);
  const renderH = size ? size.h : Math.round(window.innerHeight * dpr);
  const quality = renderQualityOf(level);
  const track = getTrack(scene.track);
  // seatColor(i), nunca um hex fixo (tests/assist.test.ts varre src/ atrás de cores de jogador).
  const humans: HumanEntry[] = Array.from({ length: scene.humans }, (_, i) => ({ seat: i, name: `P${i + 1}`, carId: CARS[i % 8].id, teamId: 0, color: seatColor(i) }));
  const state = createRace({ trackId: scene.track, laps: track.def.laps, humans, totalCars: 20, difficulty: 'profissional', manualGear: false,
    assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, seed: 42 }, track);
  // Piloto automático com o cérebro literal de tools/render-harness.html (e do smoke): não importa createBrain, que consome state.rng.
  for (const c of state.cars) if (c.seat >= 0) c.ai = { skill: 0.97, laneX: 0.2 * (c.seat - 1.5), laneUntil: 0, lookahead: 28, aggression: 0.6 } satisfies AiBrain;
  for (let i = 0; i < scene.ticks; i++) stepRace(state, track, []);
  const frame: RenderFrame = {
    state, track,
    viewports: humans.map((h) => ({ seat: h.seat, carIndex: state.cars.findIndex((c) => c.seat === h.seat), color: h.color, name: h.name, messages: [] })),
    options: { quality, showMinimap: DEFAULT_SETTINGS.showMinimap, screenShake: DEFAULT_SETTINGS.screenShake, reduceEffects: DEFAULT_SETTINGS.reduceEffects,
      palette: DEFAULT_SETTINGS.colorPalette, landmarkCaptions: DEFAULT_SETTINGS.landmarkCaptions },
    time: 0, paused: false, coop: scene.humans >= 2, showHud: true, paints: racePaints(state.cars, humans),
  };
  // 1 tick por quadro e `time` = quadro/60, nunca o dt real: senão cada máquina mede um trecho diferente da corrida.
  let n = 0;
  const step = () => { stepRace(state, track, []); frame.time = n / 60; n++; r.render(frame); };
  for (let i = 0; i < opts.warm; i++) { await nextFrame(); step(); }
  // Os máximos saem só dos quadros medidos; o aquecimento não entra.
  const ms: number[] = [];
  let maxCalls = 0; let maxTriangles = 0; let info = r.debugInfo();
  let prev = await nextFrame();
  for (let i = 0; i < opts.frames; i++) {
    step();
    info = r.debugInfo();
    maxCalls = Math.max(maxCalls, info.calls); maxTriangles = Math.max(maxTriangles, info.triangles);
    const ts = await nextFrame(); ms.push(ts - prev); prev = ts;
  }
  return { scene: scene.id, track: scene.track, humans: scene.humans, quality: level, renderQuality: quality, renderW, renderH,
    ...summarizeFrames(ms), calls: info.calls, triangles: info.triangles, maxCalls, maxTriangles, shadows: info.shadows,
    viewports: info.viewports.map((v) => ({ ...v, targetPx: v.targetW * v.targetH })) };
}

async function benchEnv(r: BenchRenderer, desktop: DesktopApi | null, opts: BenchOptions): Promise<BenchEnv> {
  const gl = (r as unknown as { __gl: { getContext(): WebGLRenderingContext } }).__gl.getContext();
  const ext = gl.getExtension('WEBGL_debug_renderer_info');
  // Tipo local: o `navigator.gpu` não está no lib.dom do TypeScript, e dependência nova (@webgpu/types) é proibida.
  const gpu = (navigator as Navigator & { gpu?: { requestAdapter(): Promise<unknown> } }).gpu;
  let webgpu: BenchEnv['webgpu'] = 'sem-api';
  if (gpu) {
    const adapter = await Promise.race([gpu.requestAdapter().catch(() => null), new Promise<null>((res) => { setTimeout(() => res(null), 3000); })]);
    webgpu = adapter ? 'adaptador' : 'sem-adaptador';
  }
  return { userAgent: navigator.userAgent, gl: String(gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER)), webgpu,
    width: window.innerWidth, height: window.innerHeight, dpr: Math.min(2, window.devicePixelRatio || 1), electron: desktop !== null, uncapped: opts.uncapped };
}

function verdictMark(v: BenchVerdict | undefined): string {
  if (!v || v.ok === null) return '';
  return v.ok ? ` · ✓ ${v.targetFps} fps` : ` · ✗ ${v.targetFps} fps`;
}

function showPanel(deps: BenchDeps, report: BenchReport, problems: string[], file: string | null): void {
  const { ui, desktop } = deps;
  ui.textContent = '';
  ui.classList.add('open'); // sem isto os botões não recebem clique: src/ui/styles.css põe pointer-events: none no #ui
  const box = document.createElement('div');
  box.style.cssText = 'position:absolute;left:24px;bottom:24px;max-width:90vw;max-height:80vh;overflow:auto;padding:16px 20px;background:rgba(0,0,0,.8);color:#fff;font:16px/1.4 sans-serif;border-radius:8px';
  const line = (text: string) => { const p = document.createElement('div'); p.textContent = text; box.appendChild(p); };
  line(t('bench.done', { n: report.runs.length }));
  report.runs.forEach((run, i) => {
    line(`${run.scene} · ${run.quality}: ${run.avgFps.toFixed(1)} fps · 1% ${run.low1Fps.toFixed(1)}${verdictMark(report.verdicts?.[i])}`);
  });
  if (problems.length) line(t('bench.problems', { n: problems.length }));
  if (report.warnings?.length) line(t('bench.warnings', { n: report.warnings.length }));
  if (file) line(t('bench.saved', { path: file }));
  const button = (label: string, onClick: () => void) => {
    const b = document.createElement('button');
    b.textContent = label; b.style.cssText = 'margin:12px 12px 0 0;padding:8px 14px;font:inherit;cursor:pointer';
    b.addEventListener('click', onClick); box.appendChild(b);
  };
  if (desktop) {
    button(t('bench.openFolder'), () => { void desktop.openFolder('bench'); });
    button(t('bench.quit'), () => { void desktop.quit(); });
  } else {
    const a = document.createElement('a');
    a.textContent = t('bench.download'); a.download = 'bench.json'; a.style.cssText = 'display:inline-block;margin-top:12px;color:#8cf';
    a.href = URL.createObjectURL(new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' }));
    box.appendChild(a);
  }
  ui.appendChild(box);
}

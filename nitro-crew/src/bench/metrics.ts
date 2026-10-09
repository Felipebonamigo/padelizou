// Banco de prova: o formato do relatório `nitro-crew-bench/1` e as contas puras sobre ele (sem DOM, sem three).
// Quem mede é src/bench/run.ts; quem lê no PC do dono é o JSON (docs/DESEMPENHO.md §4). Os NOMES dos campos são contrato
// para a L2 e para o dono: não mude, só acrescente (campos novos são opcionais, e o `nitro-crew-bench/1` continua o mesmo).
import type { Quality } from '../game/contracts';
import { FLOOR_TARGET_FPS, ULTRA_EFFECTS, ULTRA_TARGET, type BenchLevel } from './ultra';

export const BENCH_SCHEMA = 'nitro-crew-bench/1';

export interface BenchViewport { calls: number; triangles: number; targetW: number; targetH: number; targetPx: number; samples: number; passes: number; postPx: number }

export interface BenchRun {
  scene: string; track: string; humans: number;
  /** O nível pedido: baixa, média, alta ou ultra. */
  quality: BenchLevel;
  frames: number;
  avgFps: number; low1Fps: number; avgMs: number; maxMs: number;
  calls: number; triangles: number; maxCalls: number; maxTriangles: number; shadows: boolean; viewports: BenchViewport[];
  /** A qualidade com que o renderizador desenhou (o Ultra parte da Alta). */
  renderQuality?: Quality;
  /** Tamanho do buffer de desenho (px). Obrigatório no Ultra: a meta de 120 fps só vale a partir de 1440p. */
  renderW?: number;
  renderH?: number;
}

export interface BenchEnv {
  userAgent: string; gl: string; webgpu: 'adaptador' | 'sem-adaptador' | 'sem-api';
  width: number; height: number; dpr: number; electron: boolean; uncapped: boolean;
}

/** O que o Ultra promete e quanto dele existe: o relatório diz se o número é o do Ultra de verdade ou só o da Alta em 1440p. */
export interface BenchUltraInfo {
  target: { fps: number; width: number; height: number; gpu: string };
  effectsPlanned: string[];
  effectsActive: string[];
}

/** Uma linha de "bateu a meta?" por corrida (evaluateTargets). `ok` nulo = sem meta, ou sem como medir. */
export interface BenchVerdict {
  scene: string; quality: BenchLevel; humans: number;
  avgFps: number; low1Fps: number;
  targetFps: number | null; ok: boolean | null; note: string;
}

export interface BenchReport {
  schema: typeof BENCH_SCHEMA; version: string; date: string; env: BenchEnv;
  options: { frames: number; warm: number; scenes: string[]; qualities: BenchLevel[] };
  runs: BenchRun[];
  /** Ultra: meta e efeitos (ausente em relatório de antes dele). */
  ultra?: BenchUltraInfo;
  /** Meta × medido, uma linha por corrida (evaluateTargets); é o que o dono lê primeiro. */
  verdicts?: BenchVerdict[];
  /** Avisos que não invalidam o relatório (benchWarnings). */
  warnings?: string[];
}

/** window.ncBench: o que tools/bench.mjs espera. */
export interface BenchStatus { done: boolean; progress?: string; report?: BenchReport | null; problems?: string[]; file?: string | null; error?: string }
export interface FrameSummary { frames: number; avgMs: number; avgFps: number; low1Fps: number; maxMs: number }

/** Média e 1% low (a média dos piores 1% quadros, no mínimo 1) de uma série de tempos de quadro em ms. */
export function summarizeFrames(ms: readonly number[]): FrameSummary {
  const n = ms.length;
  if (n === 0) return { frames: 0, avgMs: 0, avgFps: 0, low1Fps: 0, maxMs: 0 };
  const avgMs = ms.reduce((s, x) => s + x, 0) / n;
  const worst = [...ms].sort((a, b) => b - a);
  const k = Math.max(1, Math.floor(n / 100));
  const low = worst.slice(0, k).reduce((s, x) => s + x, 0) / k;
  return { frames: n, avgMs, avgFps: 1000 / avgMs, low1Fps: 1000 / low, maxMs: worst[0] };
}

const RUN_NUMBERS = ['frames', 'avgFps', 'low1Fps', 'avgMs', 'maxMs', 'calls', 'triangles', 'maxCalls', 'maxTriangles'] as const;
const VIEWPORT_NUMBERS = ['calls', 'triangles', 'targetW', 'targetH', 'targetPx', 'samples', 'passes', 'postPx'] as const;

/** Problemas de FORMATO do relatório (vazio = bom). Meta de quadros é outra coisa: evaluateTargets. */
export function validateBenchReport(r: BenchReport): string[] {
  const out: string[] = [];
  if (r.schema !== BENCH_SCHEMA) out.push(`schema: esperado ${BENCH_SCHEMA}, veio ${String(r.schema)}`);
  for (const scene of r.options.scenes) for (const q of r.options.qualities) {
    if (!r.runs.some((x) => x.scene === scene && x.quality === q)) out.push(`falta ${scene}/${q}`);
  }
  for (const run of r.runs) {
    const id = `${run.scene}/${run.quality}`;
    for (const f of RUN_NUMBERS) {
      const x = run[f];
      if (!(Number.isFinite(x) && x > 0)) out.push(`${id}: ${f} inválido (${String(x)})`);
    }
    if (run.quality === 'ultra') {
      for (const f of ['renderW', 'renderH'] as const) {
        const x = run[f];
        if (!(typeof x === 'number' && Number.isFinite(x) && x > 0)) out.push(`${id}: ${f} inválido (${String(x)})`);
      }
    }
    if (Number.isFinite(run.avgFps) && Number.isFinite(run.low1Fps) && run.low1Fps > run.avgFps) {
      out.push(`${id}: 1% low ${run.low1Fps} acima da média ${run.avgFps}`);
    }
    if (!Array.isArray(run.viewports)) { out.push(`${id}: viewports ausente`); continue; }
    if (run.viewports.length !== run.humans) { out.push(`${id}: ${run.viewports.length} viewports para ${run.humans} jogadores`); continue; }
    run.viewports.forEach((v, j) => {
      for (const f of VIEWPORT_NUMBERS) {
        const x = v[f];
        if (!(Number.isFinite(x) && x >= 0)) out.push(`${id}: viewport ${j} ${f} inválido (${String(x)})`);
      }
      if (v.targetPx !== v.targetW * v.targetH) out.push(`${id}: viewport ${j} targetPx ${v.targetPx} ≠ ${v.targetW}×${v.targetH}`);
    });
    const calls = run.viewports.reduce((s, v) => s + v.calls, 0);
    if (calls !== run.calls) out.push(`${id}: chamadas ${run.calls} ≠ soma dos viewports ${calls}`);
    const tris = run.viewports.reduce((s, v) => s + v.triangles, 0);
    if (tris !== run.triangles) out.push(`${id}: triângulos ${run.triangles} ≠ soma dos viewports ${tris}`);
  }
  return out;
}

// ───────────────────────────── metas de quadros ─────────────────────────────

const ULTRA_PIXELS = ULTRA_TARGET.width * ULTRA_TARGET.height;

/**
 * "Bateu a meta?" por corrida. Baixa e Média miram o piso (60 fps; a Média com 4 jogadores fica sem meta, porque
 * docs/DESEMPENHO.md §4 deixa 4 jogadores caírem para a Baixa); a Alta não tem meta; o Ultra mira 120 fps e só conta de
 * 1440p para cima — abaixo disso (ou sem a resolução gravada), `ok` fica nulo.
 */
export function evaluateTargets(runs: readonly BenchRun[]): BenchVerdict[] {
  return runs.map((run): BenchVerdict => {
    const base = { scene: run.scene, quality: run.quality, humans: run.humans, avgFps: run.avgFps, low1Fps: run.low1Fps };
    if (run.quality === 'ultra') {
      const px = (run.renderW ?? 0) * (run.renderH ?? 0);
      if (!(px >= ULTRA_PIXELS)) return { ...base, targetFps: ULTRA_TARGET.fps, ok: null, note: `sem valor: a meta do Ultra é a 1440p (desenhou em ${run.renderW ?? '?'}×${run.renderH ?? '?'})` };
      const ok = run.avgFps >= ULTRA_TARGET.fps;
      return { ...base, targetFps: ULTRA_TARGET.fps, ok, note: ok ? `Ultra a ${run.renderW}×${run.renderH}: bate ${ULTRA_TARGET.fps} fps` : `Ultra a ${run.renderW}×${run.renderH}: falta ${(ULTRA_TARGET.fps - run.avgFps).toFixed(1)} fps` };
    }
    if (run.quality === 'high') return { ...base, targetFps: null, ok: null, note: 'sem meta (a Alta não tem piso)' };
    if (run.quality === 'medium' && run.humans > 2) return { ...base, targetFps: null, ok: null, note: 'sem meta (4 jogadores podem cair para a Baixa)' };
    const ok = run.avgFps >= FLOOR_TARGET_FPS;
    return { ...base, targetFps: FLOOR_TARGET_FPS, ok, note: ok ? `piso de ${FLOOR_TARGET_FPS} fps` : `falta ${(FLOOR_TARGET_FPS - run.avgFps).toFixed(1)} fps para o piso` };
  });
}

/** Taxas de atualização comuns de monitor: se TODAS as corridas colam em uma delas, o teto de quadros não foi desligado. */
const REFRESH_RATES = [30, 60, 72, 75, 90, 100, 120, 144, 165, 240] as const;

/** Avisos que não invalidam o relatório mas mudam como lê-lo. */
export function benchWarnings(r: BenchReport): string[] {
  const out: string[] = [];
  if (r.runs.length >= 2) {
    for (const hz of REFRESH_RATES) {
      if (r.runs.every((x) => x.avgFps >= hz * 0.97 && x.avgFps <= hz * 1.02)) {
        out.push(`teto de quadros em ${hz} fps: todas as corridas colaram nele, então o limite do monitor não foi desligado e o número não mede a folga (no Electron use --bench; no navegador, desligue o vsync)`);
        break;
      }
    }
  }
  if (r.ultra && r.runs.some((x) => x.quality === 'ultra')) {
    const { effectsPlanned: planned, effectsActive: active } = r.ultra;
    if (active.length < planned.length) {
      out.push(`ultra: ${active.length} de ${planned.length} efeitos implementados; o número do Ultra é o da Alta desenhada em 1440p (a folga antes dos efeitos), não o do Ultra final`);
    }
  }
  return out;
}

/** O bloco `ultra` do relatório, a partir do registro (src/bench/ultra.ts). */
export function ultraInfo(active: readonly string[]): BenchUltraInfo {
  return { target: { ...ULTRA_TARGET }, effectsPlanned: ULTRA_EFFECTS.map((e) => e.id), effectsActive: [...active] };
}

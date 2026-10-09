// Banco de prova: o plano (cenas × níveis) lido da query da página. Não importa nada do núcleo — os dados são literais.
import type { Quality } from '../game/contracts';
import { ULTRA_TARGET, type BenchLevel } from './ultra';

export interface BenchScene { id: string; track: string; humans: number; ticks: number }
export interface BenchSize { w: number; h: number }
export interface BenchOptions {
  frames: number; warm: number; scenes: BenchScene[]; qualities: BenchLevel[]; uncapped: boolean; quit: boolean;
  /** Tamanho de desenho forçado (`res=LARGURAxALTURA`); nulo = a janela (e, no Ultra, 2560×1440). */
  res: BenchSize | null;
}

/** As qualidades do jogo. */
export const BENCH_QUALITIES: readonly Quality[] = ['low', 'medium', 'high'];
/** As qualidades e o Ultra (pedido do dono, 09/10/2026): o plano padrão do bench mede todos. */
export const BENCH_LEVELS: readonly BenchLevel[] = [...BENCH_QUALITIES, 'ultra'];

/** Todas medidas depois da contagem (210 ticks), com o mesmo trecho de pista em cada qualidade. */
export const BENCH_SCENES: readonly BenchScene[] = [
  { id: 'copa-1p', track: 'copacabana', humans: 1, ticks: 300 },
  { id: 'copa-2p', track: 'copacabana', humans: 2, ticks: 300 },
  { id: 'copa-4p', track: 'copacabana', humans: 4, ticks: 300 },
  { id: 'sampa-1p', track: 'sampa_noite', humans: 1, ticks: 300 },
  { id: 'sampa-4p', track: 'sampa_noite', humans: 4, ticks: 300 },
  { id: 'foz-1p', track: 'foz_do_iguacu', humans: 1, ticks: 300 },
];

const FRAMES = { def: 600, min: 1, max: 20000 };
const WARM = { def: 60, min: 0, max: 2000 };
const RES_RE = /^(\d{3,5})x(\d{3,5})$/;

function intIn(q: URLSearchParams, key: 'frames' | 'warm', r: { def: number; min: number; max: number }): number {
  const v = q.get(key);
  if (v === null) return r.def;
  const n = Number(v);
  if (!Number.isInteger(n) || n < r.min || n > r.max) throw new Error(`${key} inválido: ${v} (inteiro de ${r.min} a ${r.max})`);
  return n;
}

/** `?bench=1&frames=&warm=&cenas=a,b&q=low,ultra&res=2560x1440&uncapped=1&sair=1`; sem `bench=1`, nulo. Valor ruim lança com o nome. */
export function parseBenchOptions(search: string): BenchOptions | null {
  const q = new URLSearchParams(search);
  if (q.get('bench') !== '1') return null;
  const frames = intIn(q, 'frames', FRAMES);
  const warm = intIn(q, 'warm', WARM);
  const scenes: BenchScene[] = [];
  const cenas = q.get('cenas');
  if (cenas === null) scenes.push(...BENCH_SCENES);
  else for (const id of cenas.split(',')) {
    const s = BENCH_SCENES.find((x) => x.id === id);
    if (!s) throw new Error(`cena desconhecida: ${id}`);
    scenes.push(s);
  }
  const qualities: BenchLevel[] = [];
  const qs = q.get('q');
  if (qs === null) qualities.push(...BENCH_LEVELS);
  else for (const v of qs.split(',')) {
    const level = BENCH_LEVELS.find((x) => x === v);
    if (!level) throw new Error(`qualidade desconhecida: ${v}`);
    qualities.push(level);
  }
  let res: BenchSize | null = null;
  const rv = q.get('res');
  if (rv !== null) {
    const m = RES_RE.exec(rv);
    const w = m ? Number(m[1]) : 0;
    const h = m ? Number(m[2]) : 0;
    if (!m || w < 320 || h < 200 || w > 7680 || h > 4320) throw new Error(`res inválido: ${rv} (LARGURAxALTURA de 320x200 a 7680x4320, ex.: 2560x1440)`);
    res = { w, h };
  }
  return { frames, warm, scenes, qualities, uncapped: q.get('uncapped') === '1', quit: q.get('sair') === '1', res };
}

/** Tamanho de desenho de um nível: `res` manda; o Ultra sem `res` desenha em 2560×1440; os outros seguem a janela (nulo). */
export function renderSizeFor(level: BenchLevel, res: BenchSize | null): BenchSize | null {
  if (res) return res;
  return level === 'ultra' ? { w: ULTRA_TARGET.width, h: ULTRA_TARGET.height } : null;
}
